using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Memory
{
    public interface IDedicatedMemoryOwner
    {
        bool SetOwnerOn(ref VkMemoryDedicatedAllocateInfoKHR info);
    }
}