﻿using System;
using System.Runtime.CompilerServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory.Mapped;
using VulkanLibrary.Managed.Utilities;

namespace VulkanLibrary.Managed.Memory.Pool
{
    public class VulkanMemoryPool : IDeviceOwned, IDisposable
    {
        /// <inheritdoc cref="IInstanceOwned.Instance"/>
        public Instance Instance => Device.Instance;

        /// <inheritdoc cref="IPhysicalDeviceOwned.PhysicalDevice"/>
        public PhysicalDevice PhysicalDevice => Device.PhysicalDevice;

        /// <inheritdoc cref="IDeviceOwned.Device"/>
        public Device Device { get; }

        private readonly MemoryPool _pool;
        private DeviceMemory _memory;
        private MappedMemory _mapped;

        /// <summary>
        /// Total capacity
        /// </summary>
        public ulong Capacity => _pool.Capacity;
        
        /// <summary>
        /// Remaining free space.
        /// </summary>
        public ulong FreeSpace => _pool.FreeSpace;

        /// <summary>
        /// Is this a mapped memory pool.
        /// </summary>
        public bool Mapped => _mapped != null;

        /// <summary>
        /// Allocates a new memory pool.
        /// </summary>
        /// <param name="dev">Device to allocate on</param>
        /// <param name="blockSize">Memory block size</param>
        /// <param name="memoryType">Memory type</param>
        /// <param name="blockCount">Number of blocks to allocate</param>
        /// <param name="mapped">Provide mapped memory</param>
        public VulkanMemoryPool(Device dev, ulong blockSize, uint memoryType, ulong blockCount, bool mapped)
        {
            Device = dev;
            var bitAlignment = (uint) System.Math.Ceiling(System.Math.Log(blockSize) / System.Math.Log(2));
            blockSize = (1UL << (int) bitAlignment);
            _pool = new MemoryPool(blockSize * blockCount, bitAlignment);
            _memory = new DeviceMemory(dev, blockSize * blockCount, memoryType);
            _mapped = mapped ? new MappedMemory(_memory, 0, _memory.Capacity, 0) : null;
        }

        /// <summary>
        /// Allocates a memory object of the given size
        /// </summary>
        /// <param name="size">Size</param>
        /// <returns>memory handle</returns>
        /// <exception cref="OutOfMemoryException">Not enough space in pool</exception>
        public MemoryHandle Allocate(ulong size)
        {
            return new MemoryHandle(this, _pool.Allocate(size));
        }

        /// <summary>
        /// Frees the given memory handle
        /// </summary>
        /// <param name="handle">handle</param>
        public void Free(MemoryHandle handle)
        {
            handle.FreeFor(this);
        }

        /// <summary>
        /// Represents a handle to pooled memory.
        /// </summary>
        public struct MemoryHandle : IMappedPooledMemory
        {
            private readonly MemoryPool.Memory _handle;
            
            /// <summary>
            /// Mapped memory handle, or null if this handle isn't mapped.
            /// </summary>
            public IMappedMemory MappedMemory { get; }

            internal MemoryHandle(VulkanMemoryPool pool, MemoryPool.Memory handle)
            {
                _handle = handle;
                MappedMemory = pool._mapped != null ? new ProxyMemory(pool._mapped, handle.Offset, handle.Size) : null;
            }
            
            /// <summary>
            /// Size of this handle.
            /// </summary>
            public ulong Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.Size; }
            }
            
            /// <summary>
            /// Offset in memory for this handle.
            /// </summary>
            public ulong Offset
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.Offset; }
            }

            internal void FreeFor(VulkanMemoryPool pool)
            {
                pool._pool.Free(_handle);
                MappedMemory.Dispose();
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            _mapped?.Dispose();
            _mapped = null;
            _memory.Dispose();
            _memory = null;
        }
    }
}