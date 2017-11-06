using System;
using System.Runtime.InteropServices;

namespace VulkanLibrary.Unmanaged
{
    public partial class Vulkan
    {
        public static VkExtensionProperties[] EnumerateExtensionProperties(string layerName)
        {
            unsafe
            {
                var layerNamePtr = (byte*) 0;
                try
                {
                    if (layerName != null)
                        layerNamePtr = (byte*) Marshal.StringToHGlobalAnsi(layerName).ToPointer();
                    VkExtensionProperties[] props = null;
                    uint extensionCount = 0;
                    do
                    {
                        props = new VkExtensionProperties[extensionCount];
                        fixed (VkExtensionProperties* propPtr = &props[0])
                        {
                            vkEnumerateInstanceExtensionProperties(layerNamePtr, &extensionCount, propPtr);
                        }
                    } while (props.Length != extensionCount);
                    return props;
                }
                finally
                {
                    if (layerNamePtr != (byte*) 0)
                        Marshal.FreeHGlobal(new IntPtr(layerNamePtr));
                }
            }
        }

        public static VkLayerProperties[] EnumerateLayerProperties()
        {
            unsafe
            {
                VkLayerProperties[] props = null;
                uint extensionCount = 0;
                do
                {
                    props = new VkLayerProperties[extensionCount];
                    fixed (VkLayerProperties* propPtr = &props[0])
                    {
                        vkEnumerateInstanceLayerProperties(&extensionCount, propPtr);
                    }
                } while (props.Length != extensionCount);
                return props;
            }
        }
    }
}