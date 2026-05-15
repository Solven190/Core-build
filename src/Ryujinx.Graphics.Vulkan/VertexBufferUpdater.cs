using System;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Ryujinx.Graphics.Vulkan
{
    internal class VertexBufferUpdater : IDisposable
    {
        private readonly VulkanRenderer _gd;

        private uint _baseBinding;
        private uint _count;

        private readonly NativeArray<VkBuffer> _buffers;
        private readonly NativeArray<ulong> _offsets;
        private readonly NativeArray<ulong> _sizes;
        private readonly NativeArray<ulong> _strides;

        private readonly Auto<DisposableBuffer>[] _bufferAutos;
        private readonly int[] _bufferOffsetsForGet;
        private readonly int[] _bufferSizesForGet;

        public VertexBufferUpdater(VulkanRenderer gd)
        {
            _gd = gd;

            _buffers = new NativeArray<VkBuffer>(Constants.MaxVertexBuffers);
            _offsets = new NativeArray<ulong>(Constants.MaxVertexBuffers);
            _sizes = new NativeArray<ulong>(Constants.MaxVertexBuffers);
            _strides = new NativeArray<ulong>(Constants.MaxVertexBuffers);

            _bufferAutos = new Auto<DisposableBuffer>[Constants.MaxVertexBuffers];
            _bufferOffsetsForGet = new int[Constants.MaxVertexBuffers];
            _bufferSizesForGet = new int[Constants.MaxVertexBuffers];
        }

        public void BindVertexBuffer(CommandBufferScoped cbs, uint binding, Auto<DisposableBuffer> autoBuffer, int offset, int size, ulong stride)
        {
            if (_count == 0)
            {
                _baseBinding = binding;
            }
            else if (_baseBinding + _count != binding)
            {
                Commit(cbs);
                _baseBinding = binding;
            }

            int index = (int)_count;

            _bufferAutos[index] = autoBuffer;
            _bufferOffsetsForGet[index] = offset;
            _bufferSizesForGet[index] = size;
            _offsets[index] = (ulong)offset;
            _sizes[index] = (ulong)size;
            _strides[index] = stride;

            _count++;
        }

        public unsafe void Commit(CommandBufferScoped cbs)
        {
            if (_count != 0)
            {
                for (int i = 0; i < _count; i++)
                {
                    _buffers[i] = _bufferAutos[i].Get(cbs, _bufferOffsetsForGet[i], _bufferSizesForGet[i]).Value;
                    _bufferAutos[i] = null;
                }

                if (_gd.Capabilities.SupportsExtendedDynamicState)
                {
                    _gd.ExtendedDynamicStateApi.CmdBindVertexBuffers2(
                        cbs.CommandBuffer,
                        _baseBinding,
                        _count,
                        _buffers.Pointer,
                        _offsets.Pointer,
                        _sizes.Pointer,
                        _strides.Pointer);
                }
                else
                {
                    _gd.Api.CmdBindVertexBuffers(cbs.CommandBuffer, _baseBinding, _count, _buffers.Pointer, _offsets.Pointer);
                }

                _count = 0;
            }
        }

        public void Dispose()
        {
            _buffers.Dispose();
            _offsets.Dispose();
            _sizes.Dispose();
            _strides.Dispose();
        }
    }
}
