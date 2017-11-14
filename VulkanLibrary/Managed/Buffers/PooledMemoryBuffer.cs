using System;
using VulkanLibrary.Managed.Buffers.Pool;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Buffers
{
    public class PooledMemoryBuffer : Handles.Buffer
    {
        private DeviceMemoryPools.MemoryHandle _memory;
        
        public DeviceMemoryPools.MemoryHandle Memory => _memory;
        
        public PooledMemoryBuffer(Device dev, VkBufferUsageFlag usage, VkBufferCreateFlag flags, MemoryRequirements req, 
            ulong size, params uint[] sharedQueueFamilies) : 
            base(dev, usage, flags, size, sharedQueueFamilies)
        {
            
            var pool = Size >= DeviceMemoryPools.BlockSizeForPool(DeviceMemoryPools.Pool.LargeBufferPool)
                ? DeviceMemoryPools.Pool.LargeBufferPool
                : DeviceMemoryPools.Pool.SmallBufferPool;
            var reqs = MemoryRequirements.Union(MemoryRequirements, req);
            var type = reqs.FindMemoryType(PhysicalDevice);
            _memory = Device.MemoryPool.Allocate(type, pool, reqs.TypeRequirements.Size);
        }

        public override void AssertValid()
        {
            base.AssertValid();
            _memory.BackingMemory.AssertValid();
        }

        protected override void Free()
        {
            base.Free();
            _memory.Free();
            _memory = default(DeviceMemoryPools.MemoryHandle);
        }
    }
}