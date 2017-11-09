using System;
using System.Runtime.InteropServices;
using System.Threading;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Memory.Mapped;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Unmanaged;
using Buffer = VulkanLibrary.Managed.Handles.Buffer;

namespace VulkanLibrary.Managed.Buffers
{
    public class PooledValueBuffer<T> : Buffer where T : struct
    {
        private uint _dirtyMin, _dirtyMax;
        private readonly ulong _itemSize;
        private readonly T[] _data;
        private readonly VulkanMemoryPools.MemoryHandle _memory;
        public IMappedMemory Memory => _memory.MappedMemory;

        /// <summary>
        /// Is this memory coherent
        /// </summary>
        public bool Coherent { get; }

        /// <summary>
        /// Number of elements in this buffer
        /// </summary>
        public uint Length => (uint) _data.LongLength;

        public PooledValueBuffer(Device device, VkBufferUsageFlag usage, VkBufferCreateFlag flags,
            bool coherent, params T[] values) : base(device, usage, flags,
            (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength)
        {
            Coherent = coherent;
            _data = values;
            _itemSize = (ulong) Marshal.SizeOf<T>();
            var pool = Size >= VulkanMemoryPools.BlockSizeForPool(VulkanMemoryPools.Pool.LargeMappedBufferPool)
                ? VulkanMemoryPools.Pool.LargeMappedBufferPool
                : VulkanMemoryPools.Pool.SmallMappedBufferPool;
            var reqs = MemoryRequirements;
            if (coherent)
                reqs.HostCoherent = MemoryRequirementLevel.Required;
            reqs.HostVisible = MemoryRequirementLevel.Required;
            var type = reqs.FindMemoryType(PhysicalDevice);

            _memory = Device.MemoryPool.Allocate(type, pool, reqs.TypeRequirements.Size);
            BindMemory(_memory.BackingMemory, _memory.Offset);
            Coherent = _memory.BackingMemory.MemoryType.HostCoherent;
            _dirtyMin = 0;
            _dirtyMax = (uint) _data.Length;
            Commit();
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

        /// <summary>
        /// Writes this buffer to the GPU
        /// </summary>
        public void Commit()
        {
            uint min, max;
            lock (this)
            {
                min = _dirtyMin;
                _dirtyMin = uint.MaxValue;
                max = _dirtyMax;
                _dirtyMax = 0;
            }
            if (min >= max)
                return;
            var addrMin = _itemSize * min;
            var addrCount = _itemSize * (max - min);
            unsafe
            {
                var handle = GCHandle.Alloc(_data);
                try
                {
                    var ptrCpu =
                        new UIntPtr((ulong) Marshal.UnsafeAddrOfPinnedArrayElement(_data, 0).ToInt64() + addrMin)
                            .ToPointer();
                    var ptrGpu = new UIntPtr((ulong) _memory.MappedMemory.Handle.ToInt64() + addrMin).ToPointer();
                    System.Buffer.MemoryCopy(ptrCpu, ptrGpu, _memory.MappedMemory.Size - addrMin, addrCount);
                    if (!Coherent)
                        _memory.MappedMemory.FlushRange(addrMin, addrCount);
                }
                finally
                {
                    handle.Free();
                }
            }
        }

        /// <summary>
        /// Reads this buffer from the GPU
        /// </summary>
        public void Read()
        {
            unsafe
            {
                var handle = GCHandle.Alloc(_data);
                try
                {
                    var ptrCpu =
                        new UIntPtr((ulong) Marshal.UnsafeAddrOfPinnedArrayElement(_data, 0).ToInt64())
                            .ToPointer();
                    var ptrGpu = new UIntPtr((ulong) _memory.MappedMemory.Handle.ToInt64()).ToPointer();
                    var size = _itemSize * (ulong) _data.LongLength;
                    if (!Coherent)
                        _memory.MappedMemory.InvalidateRange(0, size);
                    System.Buffer.MemoryCopy(ptrGpu, ptrCpu, size, size);
                }
                finally
                {
                    handle.Free();
                }
            }
        }
    }
}