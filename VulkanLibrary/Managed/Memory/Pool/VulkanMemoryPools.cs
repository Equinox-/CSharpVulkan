using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory.Mapped;
using VulkanLibrary.Managed.Utilities;

namespace VulkanLibrary.Managed.Memory.Pool
{
    public class VulkanMemoryPools : IDeviceOwned
    {
        /// <summary>
        /// Pool identity
        /// </summary>
        public enum Pool : uint
        {
            SmallMappedBufferPool = 0,
            LargeMappedBufferPool,
            TexturePool,
            Count
        }

        private const ulong PoolBlockCount = 128;

        private static ulong BlockSizeForPool(Pool p)
        {
            switch (p)
            {
                case Pool.SmallMappedBufferPool:
                    return 1024;
                case Pool.LargeMappedBufferPool:
                    return 1024 * 1024;
                case Pool.TexturePool:
                    // Blocks big enough to store one texture channel
                    return 2048 * 2048;
                case Pool.Count:
                default:
                    throw new ArgumentOutOfRangeException(nameof(p), p, null);
            }
        }

        /// <inheritdoc cref="IInstanceOwned.Instance"/>
        public Instance Instance => Device.Instance;

        /// <inheritdoc cref="IPhysicalDeviceOwned.PhysicalDevice"/>
        public PhysicalDevice PhysicalDevice => Device.PhysicalDevice;

        /// <inheritdoc cref="IDeviceOwned.Device"/>
        public Device Device { get; }

        private readonly List<VulkanMemoryPool>[,] _poolsByType;

        public VulkanMemoryPools(Device dev)
        {
            Device = dev;

            _poolsByType = new List<VulkanMemoryPool>[dev.PhysicalDevice.MemoryTypes.Count, (int) Pool.Count];
        }

        private List<VulkanMemoryPool> PoolForType(MemoryType type, Pool poolType)
        {
            var cval = _poolsByType[(int) type.TypeIndex, (int) poolType];
            if (cval != null)
                return cval;
            return _poolsByType[(int) type.TypeIndex, (int) poolType] = new List<VulkanMemoryPool>();
        }

        /// <summary>
        /// Allocates a pooled memory handle of the given size, on the given pool.
        /// </summary>
        /// <param name="type">Required memory type</param>
        /// <param name="poolType">Pool type</param>
        /// <param name="size">Size of allocated region</param>
        /// <returns>Memory handle</returns>
        public PooledMemoryHandle Allocate(MemoryType type, Pool poolType, ulong size)
        {
            var pools = PoolForType(type, poolType);
            foreach (var pool in pools)
                if (pool.FreeSpace >= size)
                {
                    try
                    {
                        return new PooledMemoryHandle(pool, pool.Allocate(size));
                    }
                    catch (OutOfMemoryException e)
                    {
                        continue;
                    }
                }
            var blockCount = PoolBlockCount;
            var blockSize = BlockSizeForPool(poolType);
            blockCount = System.Math.Max((ulong) System.Math.Ceiling(size / (double) blockSize) * 4UL, blockCount);
            var npool = new VulkanMemoryPool(Device, blockSize, type.TypeIndex, blockCount, type.HostVisible);
            pools.Add(npool);
            return new PooledMemoryHandle(npool, npool.Allocate(size));
        }

        /// <summary>
        /// Frees the given pooled memory block.
        /// </summary>
        /// <param name="mem">block to free</param>
        public void Free(PooledMemoryHandle mem)
        {
            mem.Free();
        }

        /// <summary>
        /// Handle for multi-pool allocated memory.
        /// </summary>
        /// <inheritdoc cref="IMappedPooledMemory"/>
        public struct PooledMemoryHandle : IMappedPooledMemory, IDisposable
        {
            private readonly VulkanMemoryPool _pool;
            private VulkanMemoryPool.MemoryHandle _handle;

            internal PooledMemoryHandle(VulkanMemoryPool pool, VulkanMemoryPool.MemoryHandle handle)
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

            /// <inheritdoc cref="IMappedPooledMemory.MappedMemory"/>
            public IMappedMemory MappedMemory
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.MappedMemory;  }
            }

            internal void Free()
            {
                _pool.Free(_handle);
            }

            /// <summary>
            /// Utility method for <see cref="VulkanMemoryPools.Free"/>
            /// </summary>
            /// <inheritdoc/>
            public void Dispose()
            {
                Free();
            }
        }
    }
}