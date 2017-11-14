using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Buffers
{
    public class ValueBufferGpu<T> : ValueBuffer<T> where T : struct
    {
        private readonly BufferTransfer _bufferFlusher;

        private static MemoryType FindDeviceMemory(PhysicalDevice dev, ulong capacity)
        {
            var req = new MemoryRequirements()
            {
                DeviceLocal = MemoryRequirementLevel.Required,
                TypeRequirements = new VkMemoryRequirements() {Size = capacity}
            };
            return req.FindMemoryType(dev);
        }

        public ValueBufferGpu(BufferTransfer flush, VkBufferUsageFlag usage,
            VkBufferCreateFlag create, params T[] values) : base(flush.Device, usage, create,
            FindDeviceMemory(flush.PhysicalDevice,  (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength), values)
        {
            _bufferFlusher = flush;
            Commit();
        }

        protected override unsafe void WriteGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes)
        {
            _bufferFlusher.DeferredTransfer(BackingBuffer, gpuOffset + Offset, ptrCpu, countBytes);
        }

        protected override unsafe void ReadGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes)
        {
            throw new System.NotImplementedException();
        }
    }
}