namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// One side of a two-way dependency between one texture view and another.
    /// Contains a reference to the handle owning the dependency, and the other dependency.
    /// </summary>
    class TextureDependency
    {
        /// <summary>
        /// The handle that owns this dependency.
        /// </summary>
        public TextureGroupHandle Handle;

        /// <summary>
        /// The other dependency linked to this one, which belongs to another handle.
        /// </summary>
        public TextureDependency Other;

        /// <summary>
        /// Indicates whether this dependency requires an exact raw byte copy
        /// instead of a regular texture copy.
        /// </summary>
        public readonly bool RawCopy;

        /// <summary>
        /// Creates a new texture dependency.
        /// </summary>
        /// <param name="handle">The handle that owns the dependency</param>
        /// <param name="rawCopy">True if copies performed for this dependency must preserve the exact raw bytes; false to use a regular texture copy</param>
        public TextureDependency(TextureGroupHandle handle, bool rawCopy = false)
        {
            Handle = handle;
            RawCopy = rawCopy;
        }

        /// <summary>
        /// Signals that the owner of this dependency has been modified,
        /// causing the other dependency's handle to defer a copy from it.
        /// The dependency's copy mode is propagated to the deferred copy.
        /// </summary>
        public void SignalModified()
        {
            Other.Handle.DeferCopy(Handle, RawCopy);
        }
    }
}
