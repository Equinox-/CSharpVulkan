using System;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class DescriptorSet
    {
        public DescriptorSet(DescriptorPool pool, DescriptorSetLayout layout)
        {
            unsafe
            {
                var layoutHandle = layout.Handle;
                var info = new VkDescriptorSetAllocateInfo()
                {
                    SType = VkStructureType.DescriptorSetAllocateInfo,
                    PNext = IntPtr.Zero,
                    DescriptorPool = pool.Handle,
                    DescriptorSetCount = 1,
                    PSetLayouts = &layoutHandle
                };
                VkDescriptorSet result;
                VkException.Check(VkDevice.vkAllocateDescriptorSets(pool.Device.Handle, &info, &result));
                DescriptorPool = pool;
                Handle = result;
            }
        }
    }
}