using System;
using System.Diagnostics;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Images
{
    public class SwapchainImage : Image
    {
        /// <summary>
        /// Swapchain this image belongs to
        /// </summary>
        public SwapchainKHR Swapchain { get; private set; }

        public SwapchainImage(SwapchainKHR swapchain, VkImage handle)
            : base(swapchain.Device, handle, swapchain.Format,
                new VkExtent3D() {Width = swapchain.Dimensions.Width, Height = swapchain.Dimensions.Height, Depth = 1}, 1, 1)
        {
            Swapchain = swapchain;
        }

        public override void AssertValid()
        {
            base.AssertValid();
            Swapchain.AssertValid();
        }

        protected override void Free()
        {
            // Do _not_ call base.Free().  Swapchain images are disposed automatically.
            Handle = VkImage.Null;
            Swapchain = null;
        }
    }
}