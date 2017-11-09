using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Semaphore
    {
        public Semaphore(Device dev)
        {
            Device = dev;
            unsafe
            {
                var info = new VkSemaphoreCreateInfo()
                {
                    SType = VkStructureType.SemaphoreCreateInfo,
                    Flags = 0
                };
                Handle = dev.Handle.CreateSemaphore(&info, Instance.AllocationCallbacks);
            }
        }
    }
}