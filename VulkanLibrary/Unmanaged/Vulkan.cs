using System;
using System.Runtime.InteropServices;

namespace VulkanLibrary.Unmanaged
{
    public partial class Vulkan
    {
       
        /// <summary>
        /// To query the extensions available call:
        /// </summary>
        /// <param name="layerName">is either `NULL` or a pointer to a null-terminated UTF-8 string naming the layer to retrieve extensions from.</param>
        /// <returns>array of <see cref="VkExtensionProperties"/> structures</returns>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorLayerNotPresent"></exception>
        public static VkExtensionProperties[] EnumerateExtensionProperties(string layerName)
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
                            VkException.Check(vkEnumerateInstanceExtensionProperties(layerNamePtr, &count, pptr));
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
        /// To enumerate instance layers, call:
        /// </summary>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        /// <returns>array of <see cref="VkLayerProperties"/> structures</returns>
        public static VkLayerProperties[] EnumerateLayerProperties()
        {
            unsafe
            {
                VkLayerProperties[] props;
                uint count = 0;
                do
                {
                    props = new VkLayerProperties[count];
                    fixed (VkLayerProperties* pptr = props)
                        VkException.Check(vkEnumerateInstanceLayerProperties(&count, pptr));
                } while (props.Length != count);

                return props;
            }
        }
    }
}