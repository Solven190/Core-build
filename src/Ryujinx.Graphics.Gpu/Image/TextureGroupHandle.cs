using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Synchronization;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// A tracking handle for a texture group, which represents a range of views in a storage texture.
    /// Retains a list of overlapping texture views, a modified flag, and tracking for each
    /// CPU VA range that the views cover.
    /// Also tracks copy dependencies for the handle - references to other handles that must be kept
    /// in sync with this one before use.
    /// </summary>
    class TextureGroupHandle : ISyncActionHandler, IDisposable
    {
        private const int FlushBalanceIncrement = 6;
        private const int FlushBalanceWriteCost = 1;
        private const int FlushBalanceThreshold = 7;
        private const int FlushBalanceMax = 60;
        private const int FlushBalanceMin = -10;

        private readonly TextureGroup _group;
        private int _bindCount;
        private readonly int _firstLevel;
        private readonly int _firstLayer;

        // Sync state for texture flush.

        /// <summary>
        /// The sync number last registered.
        /// </summary>
        private ulong _registeredSync;
        private ulong _registeredBufferSync = ulong.MaxValue;
        private ulong _registeredBufferGuestSync = ulong.MaxValue;

        /// <summary>
        /// The sync number when the texture was last modified by GPU.
        /// </summary>
        private ulong _modifiedSync;

        /// <summary>
        /// Whether a tracking action is currently registered or not. (0/1)
        /// </summary>
        private int _actionRegistered;

        /// <summary>
        /// Whether a sync action is currently registered or not.
        /// </summary>
        private bool _syncActionRegistered;

        /// <summary>
        /// Determines the balance of synced writes to flushes.
        /// Used to determine if the texture should always write data to a persistent buffer for flush.
        /// </summary>
        private int _flushBalance;

        /// <summary>
        /// The byte offset from the start of the storage of this handle.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// The size in bytes covered by this handle.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// The base slice index for this handle.
        /// </summary>
        public int BaseSlice { get; }

        /// <summary>
        /// The number of slices covered by this handle.
        /// </summary>
        public int SliceCount { get; }

        /// <summary>
        /// The textures which this handle overlaps with.
        /// </summary>
        public List<Texture> Overlaps { get; }

        /// <summary>
        /// The CPU memory tracking handles that cover this handle.
        /// </summary>
        public RegionHandle[] Handles { get; }

        /// <summary>
        /// True if a texture overlapping this handle has been modified. Is set false when the flush action is called.
        /// </summary>
        public bool Modified { get; set; }

        /// <summary>
        /// Dependencies to handles from other texture groups.
        /// </summary>
        public List<TextureDependency> Dependencies { get; }

        /// <summary>
        /// A flag indicating that a copy is required from one of the dependencies.
        /// </summary>
        public bool NeedsCopy => DeferredCopy != null;

        /// <summary>
        /// A data copy that must be acknowledged the next time this handle is used.
        /// </summary>
        public TextureGroupHandle DeferredCopy { get; set; }

        /// <summary>
        /// Indicates whether the pending deferred copy must preserve the exact raw bytes
        /// instead of using a regular texture copy.
        /// </summary>
        public bool DeferredCopyRaw { get; set; }

        /// <summary>
        /// Create a new texture group handle, representing a range of views in a storage texture.
        /// </summary>
        /// <param name="group">The TextureGroup that the handle belongs to</param>
        /// <param name="offset">The byte offset from the start of the storage of the handle</param>
        /// <param name="size">The size in bytes covered by the handle</param>
        /// <param name="views">All views of the storage texture, used to calculate overlaps</param>
        /// <param name="firstLayer">The first layer of this handle in the storage texture</param>
        /// <param name="firstLevel">The first level of this handle in the storage texture</param>
        /// <param name="baseSlice">The base slice index of this handle</param>
        /// <param name="sliceCount">The number of slices this handle covers</param>
        /// <param name="handles">The memory tracking handles that cover this handle</param>
        public TextureGroupHandle(TextureGroup group,
                                  int offset,
                                  ulong size,
                                  IEnumerable<Texture> views,
                                  int firstLayer,
                                  int firstLevel,
                                  int baseSlice,
                                  int sliceCount,
                                  RegionHandle[] handles)
        {
            _group = group;
            _firstLayer = firstLayer;
            _firstLevel = firstLevel;

            Offset = offset;
            Size = (int)size;
            Overlaps = [];
            Dependencies = [];

            BaseSlice = baseSlice;
            SliceCount = sliceCount;

            if (views != null)
            {
                RecalculateOverlaps(group, views);
            }

            Handles = handles;

            if (group.Storage.Info.IsLinear)
            {
                // Linear textures are presumed to be used for readback initially.
                _flushBalance = FlushBalanceThreshold + FlushBalanceIncrement;
            }

            foreach (RegionHandle handle in handles)
            {
                handle.RegisterDirtyEvent(DirtyAction);
            }
        }

        /// <summary>
        /// Handles a memory tracking region becoming dirty.
        /// This notifies all overlapping textures that their memory must be synchronized
        /// and discards any pending deferred copy state.
        /// </summary>
        private void DirtyAction()
        {
            // Notify all textures that belong to this handle.

            _group.Storage.SignalGroupDirty();

            lock (Overlaps)
            {
                foreach (Texture overlap in Overlaps)
                {
                    overlap.SignalGroupDirty();
                }
            }

            ClearDeferredCopy();
        }

        /// <summary>
        /// Discards all data for this handle.
        /// This clears all dirty flags and pending copies from other handles.
        /// </summary>
        public void DiscardData()
        {
            ClearDeferredCopy();

            foreach (RegionHandle handle in Handles)
            {
                if (handle.Dirty)
                {
                    handle.Reprotect();
                }
            }
        }

        /// <summary>
        /// Calculate a list of which views overlap this handle.
        /// </summary>
        /// <param name="group">The parent texture group, used to find a view's base CPU VA offset</param>
        /// <param name="views">The views to search for overlaps</param>
        public void RecalculateOverlaps(TextureGroup group, IEnumerable<Texture> views)
        {
            // Overlaps can be accessed from the memory tracking signal handler, so access must be atomic.
            lock (Overlaps)
            {
                int endOffset = Offset + Size;

                Overlaps.Clear();

                foreach (Texture view in views)
                {
                    int viewOffset = group.FindOffset(view);
                    if (viewOffset < endOffset && Offset < viewOffset + (int)view.Size)
                    {
                        Overlaps.Add(view);
                    }
                }
            }
        }

        /// <summary>
        /// Determine if the next sync will copy into the flush buffer.
        /// </summary>
        /// <returns>True if it will copy, false otherwise</returns>
        private bool NextSyncCopies()
        {
            return _flushBalance - FlushBalanceWriteCost > FlushBalanceThreshold;
        }

        /// <summary>
        /// Alters the flush balance by the given value. Should increase significantly with each sync, decrease with each write.
        /// A flush balance higher than the threshold will cause a texture to repeatedly copy to a flush buffer on each use.
        /// </summary>
        /// <param name="modifier">Value to add to the existing flush balance</param>
        /// <returns>True if the new balance is over the threshold, false otherwise</returns>
        private bool ModifyFlushBalance(int modifier)
        {
            int result;
            int existingBalance;
            do
            {
                existingBalance = _flushBalance;
                result = Math.Max(FlushBalanceMin, Math.Min(FlushBalanceMax, existingBalance + modifier));
            }
            while (Interlocked.CompareExchange(ref _flushBalance, result, existingBalance) != existingBalance);

            return result > FlushBalanceThreshold;
        }

        /// <summary>
        /// Adds a single texture view as an overlap if its range overlaps.
        /// </summary>
        /// <param name="offset">The offset of the view in the group</param>
        /// <param name="view">The texture to add as an overlap</param>
        public void AddOverlap(int offset, Texture view)
        {
            // Overlaps can be accessed from the memory tracking signal handler, so access must be atomic.

            if (OverlapsWith(offset, (int)view.Size))
            {
                lock (Overlaps)
                {
                    Overlaps.Add(view);
                }
            }
        }

        /// <summary>
        /// Removes a single texture view as an overlap if its range overlaps.
        /// </summary>
        /// <param name="offset">The offset of the view in the group</param>
        /// <param name="view">The texture to add as an overlap</param>
        public void RemoveOverlap(int offset, Texture view)
        {
            // Overlaps can be accessed from the memory tracking signal handler, so access must be atomic.

            if (OverlapsWith(offset, (int)view.Size))
            {
                lock (Overlaps)
                {
                    Overlaps.Remove(view);
                }
            }
        }

        /// <summary>
        /// Registers a sync action to happen for this handle, and an interim flush action on the tracking handle.
        /// </summary>
        /// <param name="context">The GPU context to register a sync action on</param>
        private void RegisterSync(GpuContext context)
        {
            if (!_syncActionRegistered)
            {
                _modifiedSync = context.SyncNumber;
                context.RegisterSyncAction(this, true);
                _syncActionRegistered = true;
            }

            if (Interlocked.Exchange(ref _actionRegistered, 1) == 0)
            {
                _group.RegisterAction(this);
            }
        }

        /// <summary>
        /// Signal that this handle has been modified to any existing dependencies, and set the modified flag.
        /// </summary>
        /// <param name="context">The GPU context to register a sync action on</param>
        public void SignalModified(GpuContext context)
        {
            Modified = true;

            // If this handle has any copy dependencies, notify the other handle that a copy needs to be performed.

            foreach (TextureDependency dependency in Dependencies)
            {
                dependency.SignalModified();
            }

            RegisterSync(context);
        }

        /// <summary>
        /// Signal that this handle has either started or ended being modified.
        /// </summary>
        /// <param name="bound">True if this handle is being bound, false if unbound</param>
        /// <param name="context">The GPU context to register a sync action on</param>
        public void SignalModifying(bool bound, GpuContext context)
        {
            SignalModified(context);

            if (!bound && _syncActionRegistered && NextSyncCopies())
            {
                // On unbind, textures that flush often should immediately create sync so their result can be obtained as soon as possible.

                context.CreateHostSyncIfNeeded(HostSyncFlags.Force);
            }

            // Note: Bind count currently resets to 0 on inherit for safety, as the handle <-> view relationship can change.
            _bindCount = Math.Max(0, _bindCount + (bound ? 1 : -1));
        }

        /// <summary>
        /// Synchronize dependent textures, if any of them have deferred a copy from this texture.
        /// </summary>
        public void SynchronizeDependents()
        {
            foreach (TextureDependency dependency in Dependencies)
            {
                TextureGroupHandle otherHandle = dependency.Other.Handle;

                if (otherHandle.DeferredCopy == this)
                {
                    otherHandle._group.Storage.SynchronizeMemory();
                }
            }
        }

        /// <summary>
        /// Wait for the latest sync number that the texture handle was written to,
        /// removing the modified flag if it was reached, or leaving it set if it has not yet been created.
        /// </summary>
        /// <param name="context">The GPU context used to wait for sync</param>
        /// <returns>True if the texture data can be read from the flush buffer</returns>
        public bool Sync(GpuContext context)
        {
            // Currently assumes the calling thread is a guest thread.

            bool inBuffer = _registeredBufferGuestSync != ulong.MaxValue;
            ulong sync = inBuffer ? _registeredBufferGuestSync : _registeredSync;

            long diff = (long)(context.SyncNumber - sync);

            ModifyFlushBalance(FlushBalanceIncrement);

            if (diff > 0)
            {
                context.Renderer.WaitSync(sync);

                if ((long)(_modifiedSync - sync) > 0)
                {
                    // Flush the data in a previous state. Do not remove the modified flag - it will be removed to ignore following writes.
                    return inBuffer;
                }

                Modified = false;

                return inBuffer;
            }

            // If the difference is <= 0, no data is not ready yet. Flush any data we can without waiting or removing modified flag.
            return false;
        }

        /// <summary>
        /// Clears the action registered variable, indicating that the tracking action should be
        /// re-registered on the next modification.
        /// </summary>
        public void ClearActionRegistered()
        {
            Interlocked.Exchange(ref _actionRegistered, 0);
        }

        /// <summary>
        /// Action to perform before a sync number is registered after modification.
        /// This action will copy the texture data to the flush buffer if this texture
        /// flushes often enough, which is determined by the flush balance.
        /// </summary>
        /// <inheritdoc/>
        public bool SyncPreAction(bool syncpoint)
        {
            if (syncpoint || NextSyncCopies())
            {
                if (ModifyFlushBalance(0) && _registeredBufferSync != _modifiedSync)
                {
                    _group.FlushIntoBuffer(this);
                    _registeredBufferSync = _modifiedSync;
                }
            }

            return true;
        }

        /// <summary>
        /// Action to perform when a sync number is registered after modification.
        /// This action will register a read tracking action on the memory tracking handle so that a flush from CPU can happen.
        /// </summary>
        /// <inheritdoc/>
        public bool SyncAction(bool syncpoint)
        {
            // The storage will need to signal modified again to update the sync number in future.
            _group.Storage.SignalModifiedDirty();

            bool lastInBuffer = _registeredBufferSync == _modifiedSync;

            if (!lastInBuffer)
            {
                _registeredBufferSync = ulong.MaxValue;
            }

            lock (Overlaps)
            {
                foreach (Texture texture in Overlaps)
                {
                    texture.SignalModifiedDirty();
                }
            }

            // Register region tracking for CPU? (again)

            _registeredSync = _modifiedSync;
            _syncActionRegistered = false;

            if (Interlocked.Exchange(ref _actionRegistered, 1) == 0)
            {
                _group.RegisterAction(this);
            }

            if (syncpoint)
            {
                _registeredBufferGuestSync = _registeredBufferSync;
            }

            // If the last modification is in the buffer, keep this sync action alive until it sees a syncpoint.
            return syncpoint || !lastInBuffer;
        }

        private void ClearDeferredCopy()
        {
            DeferredCopy = null;
            DeferredCopyRaw = false;
        }

        /// <summary>
        /// Defers a copy from another texture group handle until this handle is next synchronized.
        /// </summary>
        /// <param name="copyFrom">The texture group handle containing the source data</param>
        /// <param name="rawCopy">True if the deferred copy must preserve the exact raw bytes; false to use a regular texture copy</param>
        public void DeferCopy(TextureGroupHandle copyFrom, bool rawCopy = false)
        {
            Modified = false;
            DeferredCopy = copyFrom;
            DeferredCopyRaw = rawCopy;

            _group.Storage.SignalGroupDirty();

            foreach (Texture overlap in Overlaps)
            {
                overlap.SignalGroupDirty();
            }
        }

        /// <summary>
        /// Creates a two-way copy dependency between this handle and another handle.
        /// </summary>
        /// <param name="other">The other handle participating in the dependency</param>
        /// <param name="copyToOther">True if a pending copy from this handle should also be propagated to the other handle's existing dependencies; otherwise, false</param>
        /// <param name="rawCopy">True if the dependency must use exact raw byte copies; false to use regular texture copies</param>
        public void CreateCopyDependency(TextureGroupHandle other, bool copyToOther = false, bool rawCopy = false)
        {
            // Does this dependency already exist?
            foreach (TextureDependency existing in Dependencies)
            {
                if (existing.Other.Handle == other)
                {
                    // Do not need to create it again. May need to set the dirty flag.
                    return;
                }
            }

            _group.HasCopyDependencies = true;
            other._group.HasCopyDependencies = true;

            TextureDependency dependency = new(this, rawCopy);
            TextureDependency otherDependency = new(other, rawCopy);

            dependency.Other = otherDependency;
            otherDependency.Other = dependency;

            Dependencies.Add(dependency);
            other.Dependencies.Add(otherDependency);

            if (rawCopy)
            {
                return;
            }

            // Recursively create dependency:
            // All of this handle's dependencies must depend on the other.
            foreach (TextureDependency existing in Dependencies.ToArray())
            {
                if (existing != dependency && existing.Other.Handle != other)
                {
                    existing.Other.Handle.CreateCopyDependency(other);
                }
            }

            // All of the other handle's dependencies must depend on this.
            foreach (TextureDependency existing in other.Dependencies.ToArray())
            {
                if (existing != otherDependency && existing.Other.Handle != this)
                {
                    existing.Other.Handle.CreateCopyDependency(this);

                    if (copyToOther && Modified)
                    {
                        existing.Other.Handle.DeferCopy(this);
                    }
                }
            }
        }

        /// <summary>
        /// Remove a dependency from this handle's dependency list.
        /// </summary>
        /// <param name="dependency">The dependency to remove</param>
        public void RemoveDependency(TextureDependency dependency)
        {
            Dependencies.Remove(dependency);
        }

        /// <summary>
        /// Check if any of this handle's memory tracking handles are dirty.
        /// </summary>
        /// <returns>True if at least one of the handles is dirty</returns>
        private bool CheckDirty()
        {
            return Array.Exists(Handles, handle => handle.Dirty);
        }

        /// <summary>
        /// Copies texture data from the provided handle to this handle,
        /// or fulfills the pending deferred copy when no source handle is provided.
        /// Depending on the dependency mode, the operation uses either a regular texture copy
        /// or an exact raw byte copy.
        /// </summary>
        /// <param name="context">The GPU context used to register synchronization for modified handles</param>
        /// <param name="fromHandle">The handle to copy from, or null to use and acknowledge the pending deferred copy</param>
        /// <returns>True if the copy was performed; otherwise, false</returns>
        public bool Copy(GpuContext context, TextureGroupHandle fromHandle = null)
        {
            bool result = false;
            bool shouldCopy = false;
            bool rawCopy = false;

            if (fromHandle == null)
            {
                fromHandle = DeferredCopy;
                rawCopy = DeferredCopyRaw;

                if (fromHandle != null)
                {
                    // Only copy if the copy texture is still modified.
                    // DeferredCopy will be set to null if new data is written from CPU (see the DirtyAction method).
                    // It will also set as unmodified if a copy is deferred to it.

                    shouldCopy = true;

                    if (fromHandle._bindCount == 0)
                    {
                        // Repeat the copy in future if the bind count is greater than 0.
                        ClearDeferredCopy();
                    }
                }
            }
            else
            {
                // Copies happen directly when initializing a copy dependency.
                // If dirty, do not copy. Its data no longer matters, and this handle should also be dirty.
                // Also, only direct copy if the data in this handle is not already modified (can be set by copies from modified handles).
                shouldCopy = !fromHandle.CheckDirty() && (fromHandle.Modified || !Modified);
            }

            if (shouldCopy)
            {
                Texture from = fromHandle._group.Storage;
                Texture to = _group.Storage;

                if (from.ScaleFactor != to.ScaleFactor)
                {
                    to.PropagateScale(from);
                }

                if (rawCopy)
                {
                    PinnedSpan<byte> pinned = from.HostTexture.GetData(fromHandle._firstLayer, fromHandle._firstLevel);
                    try
                    {
                        ReadOnlySpan<byte> data = pinned.Get();
                        long targetSize = (long)to.Width * to.Height * to.Info.GetDepth() * to.Info.FormatInfo.BytesPerPixel;

                        if (targetSize <= 0 || targetSize > int.MaxValue || data.Length != targetSize)
                        {
                            return false;
                        }

                        to.HostTexture.SetData(MemoryOwner<byte>.RentCopy(data), _firstLayer, _firstLevel);
                    }
                    finally
                    {
                        pinned.Dispose();
                    }
                }
                else
                {
                    from.HostTexture.CopyTo(
                        to.HostTexture,
                        fromHandle._firstLayer,
                        _firstLayer,
                        fromHandle._firstLevel,
                        _firstLevel);
                }

                if (fromHandle.Modified)
                {
                    Modified = true;

                    RegisterSync(context);
                }

                result = true;
            }

            return result;
        }

        /// <summary>
        /// Check if this handle has a dependency to a given texture group.
        /// </summary>
        /// <param name="group">The texture group to check for</param>
        /// <returns>True if there is a dependency, false otherwise</returns>
        public bool HasDependencyTo(TextureGroup group)
        {
            foreach (TextureDependency dep in Dependencies)
            {
                if (dep.Other.Handle._group == group)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Inherit modified flags and dependencies from another texture handle.
        /// </summary>
        /// <param name="old">The texture handle to inherit from</param>
        /// <param name="withCopies">Whether the handle should inherit copy dependencies or not</param>
        public void Inherit(TextureGroupHandle old, bool withCopies)
        {
            Modified |= old.Modified;

            if (withCopies)
            {
                foreach (TextureDependency dependency in old.Dependencies.ToArray())
                {
                    CreateCopyDependency(dependency.Other.Handle);

                    if (dependency.Other.Handle.DeferredCopy == old)
                    {
                        dependency.Other.Handle.DeferredCopy = this;
                    }
                }

                DeferredCopy = old.DeferredCopy;
            }
        }

        /// <summary>
        /// Check if this region overlaps with another.
        /// </summary>
        /// <param name="address">Base address</param>
        /// <param name="size">Size of the region</param>
        /// <returns>True if overlapping, false otherwise</returns>
        public bool OverlapsWith(int offset, int size)
        {
            return Offset < offset + size && offset < Offset + Size;
        }

        /// <summary>
        /// Dispose this texture group handle, removing all its dependencies and disposing its memory tracking handles.
        /// </summary>
        public void Dispose()
        {
            foreach (RegionHandle handle in Handles)
            {
                handle.Dispose();
            }

            foreach (TextureDependency dependency in Dependencies.ToArray())
            {
                dependency.Other.Handle.RemoveDependency(dependency.Other);
            }
        }
    }
}
