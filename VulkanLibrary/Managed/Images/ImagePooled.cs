using System;
using System.Diagnostics;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Images
{
    /// <summary>
    /// Represents an image allocated from pooled memory.
    /// </summary>
    public class ImagePooled : Image
    {
        /// <summary>
        /// Gets the memory handle this 
        /// </summary>
        public DeviceMemoryPools.MemoryHandle Memory { get; private set; }

        public ImagePooled(Device dev, VkFormat format, VkImageType type, VkExtent3D size, uint mipLevels,
            uint arrayLayers, VkImageTiling tiling, VkSampleCountFlag samples, VkImageUsageFlag usage,
            VkImageLayout layout, VkImageCreateFlag flags, VkSharingMode sharing, uint[] sharedQueueFamily = null) :
            base(dev, format, type, size, mipLevels, arrayLayers, tiling, samples, usage, layout, flags, sharing,
                sharedQueueFamily)
        {
            var memReq = MemoryRequirements;
            var memType = memReq.FindMemoryType(dev.PhysicalDevice);
            Memory = dev.MemoryPool.Allocate(memType, DeviceMemoryPools.Pool.TexturePool, memReq.TypeRequirements.Size);
            BindMemory(Memory.BackingMemory, Memory.Offset);
        }

        public override void AssertValid()
        {
            base.AssertValid();
            Debug.Assert(Memory.BackingMemory != null);
            Memory.BackingMemory?.AssertValid();
            Debug.Assert(Memory.Size != 0);
        }

        protected override void Free()
        {
            base.Free();
            Memory.Free();
            Memory = default(DeviceMemoryPools.MemoryHandle);
        }
    }
}