using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using VulkanLibrary.Managed.Images;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class SwapchainKHR
    {
        private VkSwapchainCreateInfoKHR _info;

        public VkExtent2D Dimensions => _info.ImageExtent;
        public VkFormat Format => _info.ImageFormat;
        public VkColorSpaceKHR ColorSpace => _info.ImageColorSpace;

        /// <summary>
        /// Present mode of this swapchain 
        /// </summary>
        public VkPresentModeKHR PresentMode => _info.PresentMode;

        private readonly List<SwapchainImage> _swapchainImages = new List<SwapchainImage>();

        /// <summary>
        /// Images in this swapchain
        /// </summary>
        public IReadOnlyList<SwapchainImage> SwapchainImages => _swapchainImages;

        private readonly uint[] _sharingQueueInfo;

        public SwapchainKHR(SurfaceKHR surface, Device device, uint minImageCount,
            uint layerCount, VkImageUsageFlag usageFlag,
            VkFormat imageFormat, VkColorSpaceKHR colorSpace,
            VkExtent2D dimensions,
            VkCompositeAlphaFlagBitsKHR compositeAlpha,
            VkPresentModeKHR presentMode,
            bool clipped = true,
            VkSurfaceTransformFlagBitsKHR transform = VkSurfaceTransformFlagBitsKHR.IdentityBitKhr,
            VkSharingMode sharing = VkSharingMode.Exclusive, uint[] sharedQueueFamily = null,
            SwapchainKHR oldSwapchain = null)
        {
            SurfaceKHR = surface;
            Device = device;

            {
                var surfSupported = false;
                foreach (var family in Device.Queues.Select(x => x.FamilyIndex).Distinct())
                    if (PhysicalDevice.Handle.GetPhysicalDeviceSurfaceSupportKHR(family, SurfaceKHR.Handle))
                    {
                        surfSupported = true;
                        break;
                    }
                if (!surfSupported)
                    throw new NotSupportedException($"No queues on device support the surface");
            }

            _sharingQueueInfo = sharedQueueFamily;

            unsafe
            {
                if (sharing == VkSharingMode.Concurrent)
                    Debug.Assert(sharedQueueFamily != null);
                var hasPinnedSharedQueue = sharedQueueFamily != null && sharedQueueFamily.Length > 0;
                var pinnedSharedQueueFamily = hasPinnedSharedQueue
                    ? GCHandle.Alloc(sharedQueueFamily, GCHandleType.Pinned)
                    : default(GCHandle);
                try
                {
                    var info = new VkSwapchainCreateInfoKHR()
                    {
                        SType = VkStructureType.SwapchainCreateInfoKhr,
                        PNext = default(void*),
                        Flags = 0, // reserved VkSwapchainCreateFlagBitsKHR
                        Surface = surface.Handle,
                        MinImageCount = minImageCount,
                        ImageFormat = imageFormat,
                        ImageColorSpace = colorSpace,
                        ImageExtent = dimensions,
                        ImageArrayLayers = layerCount,
                        ImageUsage = usageFlag,
                        ImageSharingMode = sharing,
                        QueueFamilyIndexCount = (uint) (sharedQueueFamily?.Length ?? 0),
                        PQueueFamilyIndices = hasPinnedSharedQueue
                            ? (uint*) Marshal.UnsafeAddrOfPinnedArrayElement(sharedQueueFamily, 0).ToPointer()
                            : (uint*) 0,
                        PreTransform = transform,
                        CompositeAlpha = compositeAlpha,
                        PresentMode = presentMode,
                        Clipped = clipped,
                        OldSwapchain = oldSwapchain?.Handle ?? VkSwapchainKHR.Null
                    };
                    _info = info;
                    Handle = Device.Handle.CreateSwapchainKHR(&info, Instance.AllocationCallbacks);
                    oldSwapchain?.Dispose();
                }
                finally
                {
                    if (hasPinnedSharedQueue)
                        pinnedSharedQueueFamily.Free();
                }
            }

            var images = Device.Handle.GetSwapchainImagesKHR(Handle);
            _swapchainImages.Clear();
            _swapchainImages.Capacity = images.Length;
            foreach (var img in images)
                _swapchainImages.Add(new SwapchainImage(this, img));
        }

        /// <summary>
        /// Creates a new swapchain with identical settings and the given dimension, then destroys this swapchain.
        /// </summary>
        /// <param name="dimensions">New dimensions</param>
        /// <returns>New swapchain</returns>
        public SwapchainKHR Recreate(VkExtent2D dimensions)
        {
            return new SwapchainKHR(SurfaceKHR, Device, _info.MinImageCount,
                _info.ImageArrayLayers, _info.ImageUsage, _info.ImageFormat, _info.ImageColorSpace,
                dimensions, _info.CompositeAlpha, _info.PresentMode, _info.Clipped,
                _info.PreTransform, _info.ImageSharingMode, _sharingQueueInfo);
        }
    }
}