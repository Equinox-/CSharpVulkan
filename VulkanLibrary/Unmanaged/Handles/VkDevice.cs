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
                    var pin = GCHandle.Alloc(props, GCHandleType.Pinned);
                    try
                    {
                        var arrayPtr = count > 0 ? Marshal.UnsafeAddrOfPinnedArrayElement(props, 0).ToPointer() : (void*) 0;
                        VkException.Check(vkGetSwapchainImagesKHR(this, swapchain, &count, (VkImage*) arrayPtr));
                    }
                    finally
                    {
                        pin.Free();
                    }
                } while (props.Length != count);
                return props;
            }
        }
    }
}