using VulkanLibrary.Unmanaged;

namespace VulkanLibrary
{
    public static class CastExtensions
    {
        public static VkClearValue AsClearValue(this VkClearColorValue val)
        {
            return new VkClearValue() {Color = val};
        }

        public static VkClearValue AsClearValue(this VkClearDepthStencilValue val)
        {
            return new VkClearValue() {DepthStencil = val};
        }
    }
}