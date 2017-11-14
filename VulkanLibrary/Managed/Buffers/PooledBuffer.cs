using System.Diagnostics;
using VulkanLibrary.Managed.Buffers.Pool;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Buffers
{
    public class PooledBuffer : VulkanHandle<BufferPools.MemoryHandle>, IDeviceOwned, IPooledMemory
    {
        /// <inheritdoc/>
        public Instance Instance => Handle.BackingBuffer?.Instance;
        /// <inheritdoc/>
        public PhysicalDevice PhysicalDevice => Handle.BackingBuffer?.PhysicalDevice;
        /// <inheritdoc/>
        public Device Device => Handle.BackingBuffer?.Device;
        /// <summary>
        /// Buffer data backing this pool
        /// </summary>
        public PooledMemoryBuffer BackingBuffer => Handle.BackingBuffer;

        /// <summary>
        /// 
        /// </summary>
        public ulong Size => Handle.Size;

        public ulong Offset => Handle.Offset;

        public PooledBuffer(Device dev, MemoryType memType, ulong capacity, VkBufferUsageFlag usage, VkBufferCreateFlag create = 0)
        {
            Handle = dev.BufferPools.Allocate(memType, usage, create, capacity);
        }

        public override void AssertValid()
        {
            base.AssertValid();
            BackingBuffer.AssertValid();
        }
        
        protected override void Free()
        {
            Handle.Free();
            Handle = default(BufferPools.MemoryHandle);
        }
    }
}