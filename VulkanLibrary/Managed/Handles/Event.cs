using System;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Event
    {
        public Event(Device dev)
        {
            Device = dev;
            unsafe
            {
                var info = new VkEventCreateInfo()
                {
                    SType = VkStructureType.EventCreateInfo,
                    PNext = IntPtr.Zero,
                    Flags = 0
                };
                Handle = dev.Handle.CreateEvent(&info, Instance.AllocationCallbacks);
            }
        }

        public void Set()
        {
            AssertValid();
            Device.Handle.SetEvent(Handle);
        }

        public void Reset()
        {
            AssertValid();
            Device.Handle.ResetEvent(Handle);
        }

        public static bool SupportedBy(Queue q)
        {
            return (q.FamilyProperties.QueueFlags & (VkQueueFlag.Compute | VkQueueFlag.Graphics)) != 0;
        }
    }
}