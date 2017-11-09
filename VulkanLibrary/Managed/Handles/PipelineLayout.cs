using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class PipelineLayout
    {   
        public PipelineLayout(Device dev, VkPipelineLayoutCreateInfo info)
        {
            Device = dev;
            unsafe
            {
                Handle = dev.Handle.CreatePipelineLayout(&info, Instance.AllocationCallbacks);
            }
        }
    }
}