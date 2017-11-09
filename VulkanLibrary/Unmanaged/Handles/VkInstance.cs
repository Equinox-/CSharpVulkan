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
                    var pin = GCHandle.Alloc(props, GCHandleType.Pinned);
                    try
                    {
                        var arrayPtr = count > 0 ? Marshal.UnsafeAddrOfPinnedArrayElement(props, 0).ToPointer() : (void*) 0;
                        VkException.Check(vkEnumeratePhysicalDevices(this, &count, (VkPhysicalDevice*) arrayPtr));
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