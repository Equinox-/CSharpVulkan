using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Queue
    {
        /// <summary>
        /// The index of this queue's family.
        /// </summary>
        public uint FamilyIndex { get; }

        /// <summary>
        /// The properties of this queue's family.
        /// </summary>
        public VkQueueFamilyProperties FamilyProperties { get; }

        internal Queue(Device device, uint family, uint index)
        {
            Device = device;
            Handle = device.Handle.GetDeviceQueue(family, index);
            FamilyIndex = family;
            FamilyProperties = Device.PhysicalDevice.Handle.GetQueueFamilyProperties()[(int) family];
        }
    }
}