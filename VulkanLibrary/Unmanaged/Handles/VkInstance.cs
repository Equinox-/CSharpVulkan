using System;
using System.Runtime.InteropServices;

namespace VulkanLibrary.Unmanaged.Handles
{
    public partial struct VkInstance
    {
        /// <summary>
        /// To retrieve a list of physical device objects representing the physical devices installed in the system, call:
        /// </summary>
        /// <param name="pPhysicalDevices">is either `NULL` or a pointer to an array of <see cref="VkPhysicalDevice"/> handles.</param>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorInitializationFailed"></exception>
        public VkPhysicalDevice[] EnumeratePhysicalDevices()
        {
            unsafe
            {
                VkPhysicalDevice[] props;
                uint count = 0;
                do
                {
                    props = new VkPhysicalDevice[count];
                    fixed (VkPhysicalDevice* pptr = props)
                        VkException.Check(vkEnumeratePhysicalDevices(this, &count, pptr));
                } while (props.Length != count);

                return props;
            }
        }
    }
}