using System;
using System.Runtime.CompilerServices;
using VulkanLibrary.Managed.Buffers.Pool;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Memory.Mapped;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Buffers.Pool
{
    public class BufferPool : IDeviceOwned, IDisposable
    {
        /// <inheritdoc cref="IInstanceOwned.Instance"/>
        public Instance Instance => Device.Instance;

        /// <inheritdoc cref="IPhysicalDeviceOwned.PhysicalDevice"/>
        public PhysicalDevice PhysicalDevice => Device.PhysicalDevice;

        /// <inheritdoc cref="IDeviceOwned.Device"/>
        public Device Device { get; }

        private readonly MemoryPool _pool;
        private PooledMemoryBuffer _buffer;

        /// <summary>
        /// Total capacity
        /// </summary>
        public ulong Capacity => _pool.Capacity;

        /// <summary>
        /// Remaining free space.
        /// </summary>
        public ulong FreeSpace => _pool.FreeSpace;

        /// <summary>
        /// Allocates a new memory pool.
        /// </summary>
        /// <param name="dev">Device to allocate on</param>
        /// <param name="blockSize">Memory block size</param>
        /// <param name="blockCount">Number of blocks to allocate</param>
        /// <param name="usage">buffer usage</param>
        /// <param name="flags">buffer creation flags</param>
        /// <param name="memoryType">Memory to use</param>
        /// <param name="sharedQueueFamilies">Concurrency mode, or exclusive if empty</param>
        public BufferPool(Device dev, ulong blockSize, ulong blockCount, VkBufferUsageFlag usage,
            VkBufferCreateFlag flags, MemoryType memoryType, params uint[] sharedQueueFamilies)
        {
            Device = dev;
            var bitAlignment = (uint) System.Math.Ceiling(System.Math.Log(blockSize) / System.Math.Log(2));
            blockSize = (1UL << (int) bitAlignment);
            _pool = new MemoryPool(blockSize * blockCount, bitAlignment);
            _buffer = new PooledMemoryBuffer(dev, usage, flags, new MemoryRequirements()
                {
                    TypeRequirements = new VkMemoryRequirements() {MemoryTypeBits = 1u << (int) memoryType.TypeIndex}
                },
                blockSize * blockCount, sharedQueueFamilies);
        }

        /// <summary>
        /// Allocates a memory object of the given size
        /// </summary>
        /// <param name="size">Size</param>
        /// <returns>memory handle</returns>
        /// <exception cref="OutOfMemoryException">Not enough space in pool</exception>
        [MethodImpl(MethodImplOptions.Synchronized)] // TODO better sync with rwlock
        public MemoryHandle Allocate(ulong size)
        {
            return new MemoryHandle(this, _pool.Allocate(size));
        }

        /// <summary>
        /// Frees the given memory handle
        /// </summary>
        /// <param name="handle">handle</param>
        [MethodImpl(MethodImplOptions.Synchronized)] // TODO better sync with rwlock
        public void Free(MemoryHandle handle)
        {
            handle.FreeFor(this);
        }

        /// <summary>
        /// Represents a handle to a pooled buffer.
        /// </summary>
        /// <inheritdoc cref="IPooledMappedMemory" />
        public struct MemoryHandle : IPooledMappedMemory
        {
            private readonly MemoryPool.Memory _handle;

            /// <summary>
            /// Buffer backing this buffer handle
            /// </summary>
            public PooledMemoryBuffer BackingBuffer { get; }

            /// <inheritdoc/>
            public DeviceMemory BackingMemory { get; }

            /// <inheritdoc/>
            public IMappedMemory MappedMemory { get; }

            internal MemoryHandle(BufferPool pool, MemoryPool.Memory handle)
            {
                _handle = handle;
                BackingBuffer = pool._buffer;
                BackingMemory = pool._buffer.Memory.BackingMemory;
                MappedMemory = pool._buffer.Memory.MappedMemory != null
                    ? new ProxyMemory(pool._buffer.Memory.MappedMemory, handle.Offset, handle.Size)
                    : null;
            }

            /// <inheritdoc />
            public ulong Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.Size; }
            }

            /// <inheritdoc />
            public ulong Offset
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return _handle.Offset; }
            }

            internal void FreeFor(BufferPool pool)
            {
                MappedMemory?.Dispose();
                pool._pool.Free(_handle);
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            _buffer.Dispose();
            _buffer = null;
        }
    }
}