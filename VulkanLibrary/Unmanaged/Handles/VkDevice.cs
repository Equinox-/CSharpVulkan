using System;
using System.Runtime.InteropServices;

namespace VulkanLibrary.Unmanaged.Handles
{
    public partial struct VkDevice
    {
        /// <summary>
        /// To obtain the array of presentable images associated with a swapchain, call:
        /// </summary>
        /// <param name="swapchain">is the swapchain to query.</param>
        /// <returns>an array of <see cref="VkImage"/> handles.</returns>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        [ExtensionRequired(VkExtension.KhrSwapchain)]
        public VkImage[] GetSwapchainImagesKHR(VkSwapchainKHR swapchain)
        {
            unsafe
            {
                VkImage[] props;
                uint count = 0;
                do
                {
                    props = new VkImage[count];
                    fixed (VkImage* pptr = props)
                        VkException.Check(vkGetSwapchainImagesKHR(this, swapchain, &count, pptr));
                } while (props.Length != count);
                return props;
            }
        }
    }
}