using System;
using System.Collections.Generic;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class DescriptorPool
    {
        public DescriptorPool(Device dev, VkDescriptorPoolCreateFlag flags, uint maxSets,
            Dictionary<VkDescriptorType, uint> countByType)
        {
            unsafe
            {
                var pools = stackalloc VkDescriptorPoolSize[countByType.Count];
                var i = 0;
                foreach (var kv in countByType)
                    pools[i++] = new VkDescriptorPoolSize()
                    {
                        Type = kv.Key,
                        DescriptorCount = kv.Value
                    };
                Device = dev;
                var info = new VkDescriptorPoolCreateInfo()
                {
                    SType = VkStructureType.DescriptorPoolCreateInfo,
                    Flags = flags,
                    MaxSets = maxSets,
                    PNext = IntPtr.Zero,
                    PoolSizeCount = (uint) countByType.Count,
                    PPoolSizes = pools
                };
                Handle = dev.Handle.CreateDescriptorPool(&info, dev.Instance.AllocationCallbacks);
            }
        }

        public DescriptorPool(Device dev, VkDescriptorPoolCreateFlag flags, uint maxSets,
            Span<VkDescriptorPoolSize> pools)
        {
            unsafe
            {
                Device = dev;
                fixed (VkDescriptorPoolSize* pPools = &pools.DangerousGetPinnableReference())
                {
                    var info = new VkDescriptorPoolCreateInfo()
                    {
                        SType = VkStructureType.DescriptorPoolCreateInfo,
                        Flags = flags,
                        MaxSets = maxSets,
                        PNext = IntPtr.Zero,
                        PoolSizeCount = (uint) pools.Length,
                        PPoolSizes = pPools
                    };
                    Handle = dev.Handle.CreateDescriptorPool(&info, dev.Instance.AllocationCallbacks);
                }
            }
        }
    }
}