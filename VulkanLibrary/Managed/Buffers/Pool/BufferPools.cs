using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory.Mapped;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Buffers.Pool
{
    public class BufferPools : IDeviceOwned
    {
        // 32MB in 128KB chunks
        private const ulong PoolBlockCount = 256;

        /// <inheritdoc cref="IInstanceOwned.Instance"/>
        public Instance Instance => Device.Instance;

        /// <inheritdoc cref="IPhysicalDeviceOwned.PhysicalDevice"/>
        public PhysicalDevice PhysicalDevice => Device.PhysicalDevice;

        /// <inheritdoc cref="IDeviceOwned.Device"/>
        public Device Device { get; }

        private readonly List<BufferPool>[,,] _poolsByType;

        public BufferPools(Device dev)
        {
            Device = dev;

            var maxUsage =
                ((VkBufferUsageFlag[]) Enum.GetValues(typeof(VkBufferUsageFlag))).Aggregate(0, (a, b) => a | (int) b) +
                1;
            var maxCreation =
                ((VkBufferCreateFlag[]) Enum.GetValues(typeof(VkBufferCreateFlag)))
                .Aggregate(0, (a, b) => a | (int) b) + 1;
            _poolsByType = new List<BufferPool>[dev.PhysicalDevice.MemoryTypes.Count, maxUsage, maxCreation];
        }

        private ulong BlockSizeForUsage(VkBufferUsageFlag flag)
        {
            if ((flag & VkBufferUsageFlag.UniformBuffer) != 0)
                return Extensions.LeastCommonMultiple(128, PhysicalDevice.Limits.MinUniformBufferOffsetAlignment);
            if ((flag & VkBufferUsageFlag.VertexBuffer) != 0)
                return 1024 * 128; // 4k VertexTextured
            if ((flag & VkBufferUsageFlag.IndexBuffer) != 0)
                return 1024 * 32; // 8k uint indices  
            return 1024 * 16;
        }

        private List<BufferPool> PoolForUsage(MemoryType type, VkBufferUsageFlag usage, VkBufferCreateFlag create)
        {
            var cval = _poolsByType[type.TypeIndex, (int) usage, (int) create];
            if (cval != null)
                return cval;
            return _poolsByType[type.TypeIndex, (int) usage, (int) create] = new List<BufferPool>();
        }

        /// <summary>
        /// Dumps information about the pools to a string.
        /// </summary>
        /// <returns>info</returns>
        public string DumpStatistics()
        {
            var sb = new StringBuilder();
            for (var type = 0; type < _poolsByType.GetLength(0); type++)
            for (var usage = 0; usage < _poolsByType.GetLength(1); usage++)
            for (var create = 0; create < _poolsByType.GetLength(2); create++)
            {
                var pools = _poolsByType[type, usage, create];
                if (pools == null || pools.Count == 0)
                    continue;
                sb.AppendLine($"Type {type}, Usage {(VkBufferUsageFlag) usage}, Creation {(VkBufferCreateFlag) create}");
                foreach (var pool in pools)
                    sb.AppendLine(
                        $" - {pool.FreeSpace}/{pool.Capacity}\t({100 * (double) pool.FreeSpace / pool.Capacity:F2} % free");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Allocates a shared buffer of the given size.
        /// </summary>
        /// <param name="type">Required memory type</param>
        /// <param name="usage">Buffer usage</param>
        /// <param name="create">Buffer create flags</param>
        /// <param name="size">Size of buffer</param>
        /// <returns>Memory handle</returns>
        [MethodImpl(MethodImplOptions.Synchronized)] // TODO better sync with rwlock
        public MemoryHandle Allocate(MemoryType type, VkBufferUsageFlag usage, VkBufferCreateFlag create, ulong size)
        {
            var pools = PoolForUsage(type, usage, create);
            foreach (var pool in pools)
                if (pool.TryAllocate(size, out var tmp))
                    return new MemoryHandle(pool, tmp);
            // Create a new pool
            var blockSize = BlockSizeForUsage(usage);
            var blockCount = System.Math.Max((ulong) System.Math.Ceiling(size / (double) blockSize) * 4UL, PoolBlockCount);
            var families = Device.Queues.Select(x => x.FamilyIndex).Distinct().ToArray();
            var npool = new BufferPool(Device, blockSize, blockCount, usage, create, type, families.Length > 1 ? families : new uint[0]);
            pools.Add(npool);
            return new MemoryHandle(npool, npool.Allocate(size));
        }

        /// <summary>
        /// Frees the given pooled memory block.
        /// </summary>
        /// <param name="mem">block to free</param>
        public void Free(MemoryHandle mem)
        {
            mem.Free();
        }

        /// <summary>
        /// Handle for multi-pool allocated memory.
        /// </summary>
        /// <inheritdoc cref="IPooledMappedMemory"/>
        public struct MemoryHandle : IPooledMappedMemory, IDisposable
        {
            private readonly BufferPool _pool;
            private BufferPool.MemoryHandle _handle;

            internal MemoryHandle(BufferPool pool, BufferPool.MemoryHandle handle)
            {
                _handle = handle;
                _pool = pool;
            }

            /// <inheritdoc cref="IPooledMemory.Size"/>
            public ulong Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.Size; }
            }

            /// <inheritdoc cref="IPooledMemory.Offset"/>
            public ulong Offset
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.Offset; }
            }

            /// <inheritdoc cref="IPooledMappedMemory.MappedMemory"/>
            public IMappedMemory MappedMemory
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.MappedMemory; }
            }

            /// <inheritdoc cref="IPooledDeviceMemory.BackingMemory"/>
            public DeviceMemory BackingMemory
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.BackingMemory; }
            }
            
            /// <summary>
            /// Buffer backing this buffer handle
            /// </summary>
            public PooledMemoryBuffer BackingBuffer
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.BackingBuffer; }
            }

            internal void Free()
            {
                _pool.Free(_handle);
            }

            /// <summary>
            /// Utility method for <see cref="BufferPools.Free"/>
            /// </summary>
            /// <inheritdoc/>
            public void Dispose()
            {
                Free();
            }
        }
    }
}