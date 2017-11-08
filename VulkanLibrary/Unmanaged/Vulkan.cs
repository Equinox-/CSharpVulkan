using System;
using System.Runtime.InteropServices;

namespace VulkanLibrary.Unmanaged
{
    public partial class Vulkan
    {
        /// <summary>
        /// To query the available instance extensions, call:
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
                        var pin = GCHandle.Alloc(props, GCHandleType.Pinned);
                        try
                        {
                            var arrayPtr = count > 0 ? Marshal.UnsafeAddrOfPinnedArrayElement(props, 0).ToPointer() : (void*) 0;
                            VkException.Check(vkEnumerateInstanceExtensionProperties(layerNamePtr, &count, (VkExtensionProperties*) arrayPtr));
                        }
                        finally
                        {
                            pin.Free();
                        }
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
        /// To query the available layers, call:
        /// </summary>
        /// <returns>array of <see cref="VkLayerProperties"/> structures</returns>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfHostMemory"></exception>
        /// <exception cref="VulkanLibrary.Unmanaged.VkErrorOutOfDeviceMemory"></exception>
        public static VkLayerProperties[] EnumerateLayerProperties()
        {
            unsafe
            {
                VkLayerProperties[] props;
                uint count = 0;
                do
                {
                    props = new VkLayerProperties[count];
                    var pin = GCHandle.Alloc(props, GCHandleType.Pinned);
                    try
                    {
                        var arrayPtr = count > 0 ? Marshal.UnsafeAddrOfPinnedArrayElement(props, 0).ToPointer() : (void*) 0;
                        VkException.Check(vkEnumerateInstanceLayerProperties(&count, (VkLayerProperties*) arrayPtr));
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