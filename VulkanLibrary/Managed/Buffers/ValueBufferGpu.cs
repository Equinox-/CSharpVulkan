using System;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Buffers
{
    public class ValueBufferGpu<T> : ValueBuffer<T> where T : struct
    {
        private readonly DeferredTransfer _deferredFlusher;
        private readonly uint _targetQueueFamily;

        private static MemoryType FindDeviceMemory(PhysicalDevice dev, ulong capacity)
        {
            var req = new MemoryRequirements()
            {
                DeviceLocal = MemoryRequirementLevel.Required,
                TypeRequirements = new VkMemoryRequirements() {Size = capacity}
            };
            return req.FindMemoryType(dev);
        }

        public ValueBufferGpu(DeferredTransfer flush, VkBufferUsageFlag usage,
            VkBufferCreateFlag create, uint targetQueueFamily, params T[] values) : base(flush.Device, usage, create,
            FindDeviceMemory(flush.PhysicalDevice,  (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength), values)
        {
            _deferredFlusher = flush;
            _targetQueueFamily = targetQueueFamily;
            CommitEverything();
        }
        
        public ValueBufferGpu(DeferredTransfer flush, VkBufferUsageFlag usage,
            VkBufferCreateFlag create, uint targetQueueFamily, Action callback, params T[] values) : base(flush.Device, usage, create,
            FindDeviceMemory(flush.PhysicalDevice,  (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength), values)
        {
            _deferredFlusher = flush;
            _targetQueueFamily = targetQueueFamily;
            Commit(callback);
        }

        protected override unsafe void WriteGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes, Action callback)
        {
            _deferredFlusher.Transfer(this, gpuOffset, ptrCpu, countBytes, _targetQueueFamily, callback);
        }

        protected override unsafe void ReadGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes, Action callback)
        {
            throw new System.NotImplementedException();
        }
    }
}