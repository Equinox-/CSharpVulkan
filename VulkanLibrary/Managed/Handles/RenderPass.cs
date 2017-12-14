using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

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
        public RenderPass(Device dev, VkRenderPass handle)
        {
            Device = dev;
            Handle = handle;
        }

        public GraphicsPipelineBuilder PipelineBuilder(uint subpass, PipelineLayout layout)
        {
            return new GraphicsPipelineBuilder(this, subpass, layout);
        }

        public FramebufferBuilder<uint> FramebufferBuilder(VkExtent2D size, uint layers = 1)
        {
            return new FramebufferBuilder<uint>(this, size, (x) => x, layers);
        }
    }
}