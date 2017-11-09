using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Pipeline
    {
        public VkPipelineBindPoint PipelineType { get; }
        
        public Pipeline(Device dev, VkPipelineBindPoint type, VkPipeline pipeline)
        {
            PipelineType = type;
            Device = dev;
            Handle = pipeline;
        }
    }
}