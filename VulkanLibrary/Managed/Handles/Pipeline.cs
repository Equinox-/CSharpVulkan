using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Pipeline
    {
        public VkPipelineBindPoint PipelineType { get; }
        
        public PipelineLayout Layout { get; }
        
        public Pipeline(Device dev, VkPipelineBindPoint type, PipelineLayout layout, VkPipeline pipeline)
        {
            PipelineType = type;
            Device = dev;
            Layout = layout;
            Handle = pipeline;
        }
    }
}