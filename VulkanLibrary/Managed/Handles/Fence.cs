using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Fence
    {
        public Fence(Device dev, VkFenceCreateFlag flags)
        {
            Device = dev;
            unsafe
            {
                var info = new VkFenceCreateInfo()
                {
                    SType = VkStructureType.FenceCreateInfo,
                    PNext = (void*) 0,
                    Flags = flags
                };
                Handle = dev.Handle.CreateFence(&info, Instance.AllocationCallbacks);
            }
        }

        /// <summary>
        /// Has this fence been signaled.
        /// </summary>
        public bool IsSet
        {
            get
            {
                AssertValid();
                var res = VkException.Check(VkDevice.vkGetFenceStatus(Device.Handle, Handle));
                return res == VkResult.Success;
            }
        }

        /// <summary>
        /// Resets this fence
        /// </summary>
        public void Reset()
        {
            unsafe
            {
                var handle = Handle;
                VkException.Check(VkDevice.vkResetFences(Device.Handle, 1, &handle));
            }
        }

        /// <summary>
        /// Waits for this fence.
        /// </summary>
        /// <param name="timeout">Timeout in ns</param>
        /// <returns><see cref="VkResult.Success"/> or <see cref="VkResult.Timeout"/></returns>
        public VkResult WaitFor(ulong timeout)
        {
            unsafe
            {
                var handle = Handle;
                return VkException.Check(VkDevice.vkWaitForFences(Device.Handle, 1, &handle, true, timeout));
            }
        }
    }
}