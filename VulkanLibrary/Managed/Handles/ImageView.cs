using System;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class ImageView
    {
        public Image Image { get; }

        public ImageView(Image image, VkImageSubresourceRange range, VkImageViewType type, VkFormat? format = null,
            VkComponentMapping? swizzle = null)
        {
            Device = image.Device;
            Image = image;

            unsafe
            {
                var info = new VkImageViewCreateInfo()
                {
                    SType = VkStructureType.ImageViewCreateInfo,
                    Flags = 0,
                    PNext = IntPtr.Zero,
                    Format = format ?? image.Format,
                    Components = swizzle ?? new VkComponentMapping()
                    {
                        R = VkComponentSwizzle.Identity,
                        G = VkComponentSwizzle.Identity,
                        B = VkComponentSwizzle.Identity,
                        A = VkComponentSwizzle.Identity
                    },
                    Image = image.Handle,
                    ViewType = type,
                    SubresourceRange = range
                };
                Handle = Device.Handle.CreateImageView(&info, Instance.AllocationCallbacks);
            }
        }
    }
}