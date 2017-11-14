using System.Collections.Generic;
using System.Diagnostics;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class CommandPool
    {
        public CommandPool(Device dev, uint queueFamily, VkCommandPoolCreateFlag flags = 0)
        {
            Device = dev;
            unsafe
            {
                var info = new VkCommandPoolCreateInfo()
                {
                    SType = VkStructureType.CommandPoolCreateInfo,
                    PNext = (void*) 0,
                    Flags = flags,
                    QueueFamilyIndex = queueFamily
                };
                Handle = dev.Handle.CreateCommandPool(&info, Instance.AllocationCallbacks);
            }
        }

        public CommandBuffer BuildCommandBuffer(VkCommandBufferLevel level)
        {
            return new CommandBuffer(this, level);
        }
    }
}