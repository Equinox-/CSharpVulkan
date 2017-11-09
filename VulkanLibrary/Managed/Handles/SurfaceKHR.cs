using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class SurfaceKHR
    {
        public SurfaceKHR(Instance instance, VkSurfaceKHR res)
        {
            Instance = instance;
            Handle = res;
        }

        public VkSurfaceCapabilitiesKHR Capabilities(PhysicalDevice dev)
        {
            AssertValid();
            return dev.Handle.GetPhysicalDeviceSurfaceCapabilitiesKHR(Handle);
        }
    }
}