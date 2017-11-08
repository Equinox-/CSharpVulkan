using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Image : IDedicatedMemoryOwner
    {
        /// <summary>
        /// Format of this image
        /// </summary>
        public VkFormat Format { get; }

        /// <summary>
        /// Size of this image
        /// </summary>
        public VkExtent3D Dimensions { get; }

        /// <summary>
        /// Size of this image
        /// </summary>
        public VkExtent2D Dimensions2D => new VkExtent2D() {Width = Dimensions.Width, Height = Dimensions.Height};

        /// <summary>
        /// Requirements for this image's memory
        /// </summary>
        public MemoryRequirements MemoryRequirements
        {
            get
            {
                AssertValid();
                if (!Device.ExtensionEnabled(VkExtension.KhrGetMemoryRequirements2))
                {
                    var requirements = Device.Handle.GetImageMemoryRequirements(Handle);
                    return new MemoryRequirements()
                    {
                        TypeRequirements = requirements
                    };
                }
                unsafe
                {
                    var useDedicated = Device.ExtensionEnabled(VkExtension.KhrDedicatedAllocation);
                    var dedInfo = new VkMemoryDedicatedRequirementsKHR()
                    {
                        SType = VkStructureType.MemoryDedicatedRequirementsKhr,
                        PNext = (void*) 0
                    };
                    var memReq2 = new VkImageMemoryRequirementsInfo2KHR()
                    {
                        SType = VkStructureType.ImageMemoryRequirementsInfo2Khr,
                        PNext = useDedicated ? &dedInfo : (void*) 0,
                        Image = Handle
                    };
                    var reqs2 = Device.Handle.GetImageMemoryRequirements2KHR(&memReq2);
                    var result = new MemoryRequirements()
                    {
                        TypeRequirements = reqs2.MemoryRequirements
                    };
                    if (!useDedicated ||
                        (!dedInfo.RequiresDedicatedAllocation && !dedInfo.PrefersDedicatedAllocation))
                        return result;
                    result.DedicatedMemory.Value = this;
                    result.DedicatedMemory.RequirementLevel = dedInfo.RequiresDedicatedAllocation
                        ? MemoryRequirementLevel.Required
                        : MemoryRequirementLevel.Preferred;
                    return result;
                }
            }
        }

        public Image(Device dev, VkFormat format, VkImageType type, VkExtent3D size, uint mipLevels, uint arrayLayers,
            VkImageTiling tiling, VkSampleCountFlag samples, VkImageUsageFlag usage,
            VkImageLayout layout, VkImageCreateFlag flags, VkSharingMode sharing = VkSharingMode.Exclusive,
            uint[] sharedQueueFamily = null)
        {
            Device = dev;
            Format = format;
            Dimensions = size;
            unsafe
            {
#if DEBUG
                var properties = Device.PhysicalDevice.Handle.GetPhysicalDeviceImageFormatProperties(format, type,
                    tiling, usage, flags);
#endif
                if (sharing == VkSharingMode.Concurrent)
                    Debug.Assert(sharedQueueFamily != null);
                if (sharedQueueFamily == null)
                    sharedQueueFamily = new uint[0];
                fixed (uint* sharedPtr = &sharedQueueFamily[0])
                {
                    var info = new VkImageCreateInfo()
                    {
                        SType = VkStructureType.ImageCreateInfo,
                        Format = format,
                        ImageType = type,
                        Tiling = tiling,
                        Extent = size,
                        ArrayLayers = arrayLayers,
                        MipLevels = mipLevels,
                        Flags = flags,
                        InitialLayout = layout,
                        PNext = (void*) 0,
                        PQueueFamilyIndices = sharedPtr,
                        SharingMode = sharing
                    };
                    Handle = Device.Handle.CreateImage(&info, Instance.AllocationCallbacks);
                }
            }
        }

        protected Image(Device dev, VkImage handle, VkFormat format, VkExtent3D size)
        {
            Device = dev;
            Format = format;
            Dimensions = size;
            Handle = handle;
        }

        protected void BindMemory(DeviceMemory mem, ulong offset)
        {
            Device.Handle.BindImageMemory(Handle, mem.Handle, offset);
        }

        /// <inheritdoc cref="IDedicatedMemoryOwner.SetOwnerOn" />
        public bool SetOwnerOn(ref VkMemoryDedicatedAllocateInfoKHR info)
        {
            AssertValid();
            info.Image = Handle;
            info.Buffer = VkBuffer.Null;
            return true;
        }
    }
}