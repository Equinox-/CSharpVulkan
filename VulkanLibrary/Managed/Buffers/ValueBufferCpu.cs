using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Memory.Mapped;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Unmanaged;
using Buffer = VulkanLibrary.Managed.Handles.Buffer;

namespace VulkanLibrary.Managed.Buffers
{
    public class ValueBufferCpu<T> : ValueBuffer<T> where T : struct
    {
        /// <summary>
        /// Is this memory coherent
        /// </summary>
        public bool Coherent { get; }

        private static MemoryType FindHostMemory(PhysicalDevice dev, bool requiresCoherent, ulong capacity)
        {
            var req = new MemoryRequirements()
            {
                HostVisible = MemoryRequirementLevel.Required,
                HostCoherent = requiresCoherent ? MemoryRequirementLevel.Required : MemoryRequirementLevel.Preferred,
                TypeRequirements = new VkMemoryRequirements() {Size = capacity}
            };
            return req.FindMemoryType(dev);
        }

        public ValueBufferCpu(Device device, VkBufferUsageFlag usage, VkBufferCreateFlag flags,
            bool coherent, params T[] values) : base(device, usage, flags,
            FindHostMemory(device.PhysicalDevice, coherent, (ulong) Marshal.SizeOf<T>() * (ulong) values.LongLength), values)
        {
            Coherent = BackingBuffer.Memory.BackingMemory.MemoryType.HostCoherent;
            CommitEverything();
        }

        protected override unsafe void WriteGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes, Action callback)
        {
            var ptrGpu = new UIntPtr((ulong) BackingBuffer.Memory.MappedMemory.Handle.ToInt64() + gpuOffset)
                .ToPointer();
            System.Buffer.MemoryCopy(ptrCpu, ptrGpu, BackingBuffer.Memory.MappedMemory.Size - gpuOffset,
                countBytes);
            if (!Coherent)
                BackingBuffer.Memory.MappedMemory.FlushRange(gpuOffset, countBytes);
            callback?.Invoke();
        }

        protected override unsafe void ReadGpuMemory(void* ptrCpu, ulong gpuOffset, ulong countBytes, Action callback)
        {
            var ptrGpu = new UIntPtr((ulong) BackingBuffer.Memory.MappedMemory.Handle.ToInt64()).ToPointer();
            if (!Coherent)
                BackingBuffer.Memory.MappedMemory.InvalidateRange(0, countBytes);
            System.Buffer.MemoryCopy(ptrGpu, ptrCpu, countBytes, countBytes);
            callback?.Invoke();
        }
    }
}