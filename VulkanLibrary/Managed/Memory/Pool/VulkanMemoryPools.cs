﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
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

        public static ulong BlockSizeForPool(Pool p)
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
        /// Dumps information about the pools to a string.
        /// </summary>
        /// <returns>info</returns>
        public string DumpStatistics()
        {
            var sb = new StringBuilder();
            for (var type = 0; type < _poolsByType.Length; type++)
                foreach (var poolType in (Pool[]) Enum.GetValues(typeof(Pool)))
                {
                    var pools = _poolsByType[type, (int) poolType];
                    if (pools == null || pools.Count == 0)
                        continue;
                    sb.AppendLine($"Memory type {0}, Pool type {poolType}");
                    foreach (var pool in pools)
                        sb.AppendLine(
                            $" - {pool.FreeSpace}/{pool.Capacity}\t({100 * (double) pool.FreeSpace / pool.Capacity:F2} % free");
                }
            return sb.ToString();
        }

        /// <summary>
        /// Allocates a pooled memory handle of the given size, on the given pool.
        /// </summary>
        /// <param name="type">Required memory type</param>
        /// <param name="poolType">Pool type</param>
        /// <param name="size">Size of allocated region</param>
        /// <returns>Memory handle</returns>
        public MemoryHandle Allocate(MemoryType type, Pool poolType, ulong size)
        {
            var pools = PoolForType(type, poolType);
            foreach (var pool in pools)
                if (pool.FreeSpace >= size)
                {
                    try
                    {
                        return new MemoryHandle(pool, pool.Allocate(size));
                    }
                    catch (OutOfMemoryException)
                    {
                        // continue onto the next pool
                    }
                }
            // Create a new pool
            var blockCount = PoolBlockCount;
            var blockSize = BlockSizeForPool(poolType);
            blockCount = System.Math.Max((ulong) System.Math.Ceiling(size / (double) blockSize) * 4UL, blockCount);
            var npool = new VulkanMemoryPool(Device, blockSize, type.TypeIndex, blockCount, type.HostVisible);
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
            private readonly VulkanMemoryPool _pool;
            private VulkanMemoryPool.MemoryHandle _handle;

            internal MemoryHandle(VulkanMemoryPool pool, VulkanMemoryPool.MemoryHandle handle)
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