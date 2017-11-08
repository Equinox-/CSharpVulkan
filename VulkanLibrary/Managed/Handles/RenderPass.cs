using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class RenderPass
    {
        public RenderPass(Device dev, VkRenderPassCreateInfo info)
        {
            Device = dev;
            unsafe
            {
                Handle = dev.Handle.CreateRenderPass(&info, Instance.AllocationCallbacks);
            }
        }
    }
}