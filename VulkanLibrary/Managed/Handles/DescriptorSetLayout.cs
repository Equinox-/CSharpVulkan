using System.Collections.Generic;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class DescriptorSetLayout
    {
        private readonly Dictionary<uint, DescriptorData> _descriptorData;

        public readonly struct DescriptorData
        {
            public readonly VkDescriptorType Type;
            public readonly uint Count;
            public readonly VkShaderStageFlag Stages;

            internal DescriptorData(VkDescriptorSetLayoutBinding b)
            {
                Type = b.DescriptorType;
                Count = b.DescriptorCount;
                Stages = b.StageFlags;
            }
        }

        public IEnumerable<KeyValuePair<uint, DescriptorData>> Descriptors => _descriptorData;
        public DescriptorData DescriptorFor(uint binding) => _descriptorData[binding];

        public DescriptorSetLayout(Device dev, VkDescriptorSetLayoutCreateInfo info)
        {
            Device = dev;
            _descriptorData = new Dictionary<uint, DescriptorData>((int) info.BindingCount);
            unsafe
            {
                for (var i = 0; i < info.BindingCount; i++)
                    _descriptorData.Add(info.PBindings[i].Binding, new DescriptorData(info.PBindings[i]));
                Handle = dev.Handle.CreateDescriptorSetLayout(&info, dev.Instance.AllocationCallbacks);
            }
        }
    }
}