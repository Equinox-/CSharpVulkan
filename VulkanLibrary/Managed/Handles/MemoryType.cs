using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class MemoryType
    {
        public readonly VkMemoryPropertyFlag Flags;
        public readonly uint TypeIndex;
        public readonly MemoryHeap Heap;

        internal MemoryType(uint typeIndex, MemoryHeap heap, VkMemoryPropertyFlag flags)
        {
            TypeIndex = typeIndex;
            Heap = heap;
            Flags = flags;
        }

        public bool DeviceLocal => (Flags & VkMemoryPropertyFlag.DeviceLocal) != 0;

        public bool HostVisible => (Flags & VkMemoryPropertyFlag.HostVisible) != 0;

        public bool HostCoherent => (Flags & VkMemoryPropertyFlag.HostCoherent) != 0;

        public bool HostCached => (Flags & VkMemoryPropertyFlag.HostCached) != 0;

        public bool LazilyAllocated => (Flags & VkMemoryPropertyFlag.LazilyAllocated) != 0;
    }

    public class MemoryHeap
    {
        public readonly uint HeapIndex;
        public readonly ulong Size;
        public readonly VkMemoryHeapFlag Flags;

        internal MemoryHeap(uint heapIndex, VkMemoryHeap desc)
        {
            HeapIndex = heapIndex;
            Size = desc.Size;
            Flags = (VkMemoryHeapFlag) desc.Flags;
        }
        
        public bool DeviceLocal => (Flags & VkMemoryHeapFlag.DeviceLocal) != 0;
    }
}