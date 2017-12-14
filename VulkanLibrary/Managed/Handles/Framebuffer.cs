using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Framebuffer
    {
        /// <summary>
        /// Size of this framebuffer
        /// </summary>
        public VkExtent2D Size { get; }

        public Framebuffer(Device dev, VkFramebufferCreateInfo info)
        {
            Device = dev;
            Size = new VkExtent2D() {Width = info.Width, Height = info.Height};
            unsafe
            {
                Handle = dev.Handle.CreateFramebuffer(&info, Instance.AllocationCallbacks);
            }
        }

        public Framebuffer(RenderPass pass, VkExtent2D size, uint layers, IEnumerable<ImageView> views)
        {
            Size = size;
            Device = pass.Device;
            var imageArray = views.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray();
            unsafe
            {
                fixed (VkImageView* view = imageArray)
                {
                    var info = new VkFramebufferCreateInfo()
                    {
                        SType = VkStructureType.FramebufferCreateInfo,
                        Flags = 0,
                        PNext = IntPtr.Zero,
                        RenderPass = pass.Handle,
                        Width = size.Width,
                        Height = size.Height,
                        Layers = layers,
                        AttachmentCount = (uint) imageArray.Length,
                        PAttachments = view
                    };
                    Handle = Device.Handle.CreateFramebuffer(&info, Instance.AllocationCallbacks);
                }
            }
        }
    }
}