using System;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Buffers
{
    public class ValueBufferGpu<T> : ValueBuffer<T> where T : struct
    {
        private readonly DeferredTransfer _deferredFlusher;

        private static MemoryType FindDeviceMemory(PhysicalDevice dev, ulong capacity)
        {
            var req = new MemoryRequirements()
            {
                DeviceLocal = MemoryRequirementLevel.Required,
                TypeRequirements = new VkMemoryRequirements() {Size = capacity}
            };
            return req.FindMemoryType(dev);
        }

        public ValueBufferGpu(DeferredTransfer flush, VkBufferUsageFlag usage, VkBufferCreateFlag create, T[] values) :
            base(flush.Device, usage, create,
                FindDeviceMemory(flush.PhysicalDevice, (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength), values)
        {
            _deferredFlusher = flush;
        }

        protected override unsafe void WriteGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes, DeferredTransfer.TransferArguments? args)
        {
            _deferredFlusher.Transfer(this, gpuOffset, ptrCpu, countBytes, args);
        }

        protected override unsafe void ReadGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes, DeferredTransfer.TransferArguments? args)
        {
            throw new System.NotImplementedException();
        }
    }
}