namespace VulkanLibrary.Unmanaged.Handles
{
    public partial struct VkPhysicalDevice
    {
        public VkQueueFamilyProperties[] GetQueueFamilyProperties()
        {
            unsafe
            {
                VkQueueFamilyProperties[] props = null;
                uint extensionCount = 0;
                do
                {
                    props = new VkQueueFamilyProperties[extensionCount];
                    fixed (VkQueueFamilyProperties* propPtr = &props[0])
                    {
                        vkGetPhysicalDeviceQueueFamilyProperties(this, &extensionCount, propPtr);
                    }
                } while (props.Length != extensionCount);
                return props;
            }
        }
    }
}