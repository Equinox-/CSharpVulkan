using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace VulkanLibrary.Unmanaged.Handles
{
    public partial struct VkPhysicalDevice
    {
        /// <summary>
        /// To query properties of queues available on a physical device, call:
        /// </summary>
        /// <returns>array of <see cref="VkQueueFamilyProperties"/> structures</returns>
        public VkQueueFamilyProperties[] GetQueueFamilyProperties()
        {
            unsafe
            {
                VkQueueFamilyProperties[] props;
                uint count = 0;
                do
                {
                    props = new VkQueueFamilyProperties[count];
                    fixed (VkQueueFamilyProperties* pptr = props)
                        vkGetPhysicalDeviceQueueFamilyProperties(this, &count, pptr);
                } while (props.Length != count);

                return props;
            }
        }

        /// <summary>
        /// To query the supported presentation modes for a surface, call:
        /// </summary>
        /// <param name="surface">is the surface that will be associated with the swapchain.</param>
        /// <returns>an array of <c>VkPresentModeKHR</c> values, indicating the supported presentation modes.</returns>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorSurfaceLostKhr"></exception>
        [ExtensionRequired(VkExtension.KhrSurface)]
        public VkPresentModeKHR[] GetPhysicalDeviceSurfacePresentModesKHR(VkSurfaceKHR surface)
        {
            unsafe
            {
                VkPresentModeKHR[] props;
                uint count = 0;
                do
                {
                    props = new VkPresentModeKHR[count];
                    fixed (VkPresentModeKHR* pptr = props)
                        VkException.Check(vkGetPhysicalDeviceSurfacePresentModesKHR(this, surface, &count, pptr));
                } while (props.Length != count);

                return props;
            }
        }

        /// <summary>
        /// To query the supported swapchain format-color space pairs for a surface, call:
        /// </summary>
        /// <param name="surface">is the surface that will be associated with the swapchain.</param>
        /// <returns>array of <see cref="VkSurfaceFormatKHR"/> structures.</returns>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorSurfaceLostKhr"></exception>
        [ExtensionRequired(VkExtension.KhrSurface)]
        public VkSurfaceFormatKHR[] GetPhysicalDeviceSurfaceFormatsKHR(VkSurfaceKHR surface)
        {
            unsafe
            {
                VkSurfaceFormatKHR[] props;
                uint count = 0;
                do
                {
                    props = new VkSurfaceFormatKHR[count];
                    fixed (VkSurfaceFormatKHR* pptr = props)
                        VkException.Check(vkGetPhysicalDeviceSurfaceFormatsKHR(this, surface, &count, pptr));
                } while (props.Length != count);

                return props;
            }
        }

        /// <summary>
        /// To query the extensions available to a given physical device, call:
        /// </summary>
        /// <param name="layerName">is either `NULL` or a pointer to a null-terminated UTF-8 string naming the layer to retrieve extensions from.</param>
        /// <returns>array of <see cref="VkExtensionProperties"/> structures</returns>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorLayerNotPresent"></exception>
        public VkExtensionProperties[] EnumerateExtensionProperties(string layerName)
        {
            unsafe
            {
                var layerNamePtr = (byte*) 0;
                try
                {
                    if (layerName != null)
                        layerNamePtr = (byte*) Marshal.StringToHGlobalAnsi(layerName).ToPointer();
                    VkExtensionProperties[] props;
                    uint count = 0;
                    do
                    {
                        props = new VkExtensionProperties[count];
                        fixed (VkExtensionProperties* pptr = props)
                            VkException.Check(vkEnumerateDeviceExtensionProperties(this, layerNamePtr, &count, pptr));
                    } while (props.Length != count);

                    return props;
                }
                finally
                {
                    if (layerNamePtr != (byte*) 0)
                        Marshal.FreeHGlobal(new IntPtr(layerNamePtr));
                }
            }
        }

        /// <summary>
        /// To enumerate device layers, call:
        /// </summary>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <returns>array of <see cref="VkLayerProperties"/> structures</returns>
        public VkLayerProperties[] EnumerateLayerProperties()
        {
            unsafe
            {
                VkLayerProperties[] props;
                uint count = 0;
                do
                {
                    props = new VkLayerProperties[count];
                    fixed (VkLayerProperties* pptr = props)
                        VkException.Check(vkEnumerateDeviceLayerProperties(this, &count, pptr));
                } while (props.Length != count);

                return props;
            }
        }
    }
}