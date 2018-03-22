using System;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Buffers
{
    public interface IValueBuffer
    {
        /// <summary>
        /// Flushes the dirty region of the buffer to the GPU
        /// </summary>
        /// <param name="arguments">transfer arguments</param>
        void Commit(DeferredTransfer.TransferArguments? arguments = null);

        /// <summary>
        /// Flushes this entire buffer to the GPU
        /// </summary>
        /// <param name="signal">flush finished</param>
        /// <param name="arguments">transfer arguments</param>
        void CommitEverything(DeferredTransfer.TransferArguments? arguments = null);

        /// <summary>
        /// Flushes a portion of this buffer to the GPU
        /// </summary>
        /// <param name="min">Minimum index, inclusive</param>
        /// <param name="max">Maximum index, exclusive</param>
        /// <param name="arguments">transfer arguments</param>
        void CommitRange(uint min, uint max, DeferredTransfer.TransferArguments? arguments = null);

        /// <summary>
        /// Reads this buffer from the GPU
        /// </summary>
        /// <param name="arguments">transfer arguments</param>
        void Read(DeferredTransfer.TransferArguments? arguments = null);
    }

    public abstract class ValueBuffer<T> : PooledBuffer, IValueBuffer where T : struct
    {
        private uint _dirtyMin, _dirtyMax;
        private readonly ulong _itemSize;
        private readonly T[] _data;

        // ReSharper disable once ConvertToAutoPropertyWhenPossible
        /// <summary>
        /// A view into the internal data where write access isn't tracked
        /// </summary>
        public T[] UntrackedData => _data;

        /// <summary>
        /// Number of elements in this buffer
        /// </summary>
        public uint Count => (uint) _data.LongLength;

        protected ValueBuffer(Device device, VkBufferUsageFlag usage, VkBufferCreateFlag flags, MemoryType mem,
            params T[] values) : base(device, mem,
            (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength, usage, flags)
        {
            _data = values;
            _itemSize = (ulong) Marshal.SizeOf<T>();
            _dirtyMin = 0;
            _dirtyMax = (uint) _data.Length;
            ;
        }

        public T this[uint i]
        {
            get => _data[i];
            set
            {
                _data[i] = value;
                lock (this)
                {
                    if (_dirtyMin > _dirtyMax)
                    {
                        _dirtyMin = i;
                        _dirtyMax = i + 1;
                    }
                    else
                    {
                        _dirtyMin = Math.Min(_dirtyMin, i);
                        _dirtyMax = Math.Max(_dirtyMax, i + 1);
                    }
                }
            }
        }

        protected abstract unsafe void WriteGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes,
            DeferredTransfer.TransferArguments? arguments);

        protected abstract unsafe void ReadGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes,
            DeferredTransfer.TransferArguments? arguments);

        public void Commit(DeferredTransfer.TransferArguments? args = null)
        {
            uint min, max;
            lock (this)
            {
                min = _dirtyMin;
                _dirtyMin = uint.MaxValue;
                max = _dirtyMax;
                _dirtyMax = 0;
            }

            CommitRange(min, max, args);
        }

        public void CommitEverything(DeferredTransfer.TransferArguments? args = null)
        {
            lock (this)
            {
                _dirtyMin = uint.MaxValue;
                _dirtyMax = 0;
            }

            CommitRange(0, Count, args);
        }

        public void CommitRange(uint min, uint max, DeferredTransfer.TransferArguments? args = null)
        {
            if (min >= max)
                return;
            var addrMin = _itemSize * min;
            var addrCount = _itemSize * (max - min);
            unsafe
            {
                var handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
                try
                {
                    var ptrCpu =
                        new UIntPtr((ulong) Marshal.UnsafeAddrOfPinnedArrayElement(_data, 0).ToInt64() + addrMin)
                            .ToPointer();
                    WriteGpuMemory(ptrCpu, addrMin, addrCount, args);
                }
                finally
                {
                    handle.Free();
                }
            }
        }

        public void Read(DeferredTransfer.TransferArguments? args = null)
        {
            unsafe
            {
                var handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
                try
                {
                    var ptrCpu =
                        new UIntPtr((ulong) Marshal.UnsafeAddrOfPinnedArrayElement(_data, 0).ToInt64())
                            .ToPointer();
                    var size = _itemSize * (ulong) _data.LongLength;
                    ReadGpuMemory(ptrCpu, 0, size, args);
                }
                finally
                {
                    handle.Free();
                }
            }
        }
    }
}