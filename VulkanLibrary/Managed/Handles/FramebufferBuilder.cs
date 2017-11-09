using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class FramebufferBuilder<TAttachment>
    {
        private readonly RenderPass _pass;
        private readonly Func<TAttachment, uint> _attachmentMap;
        private readonly Dictionary<TAttachment, ImageView> _images = new Dictionary<TAttachment, ImageView>();
        private readonly VkExtent2D _size;
        private readonly uint _layers;

        public FramebufferBuilder(RenderPass pass, VkExtent2D size, Func<TAttachment, uint> map, uint layers = 1)
        {
            _pass = pass;
            _size = size;
            _attachmentMap = map;
            _layers = layers;
        }

        public FramebufferBuilder<TAttachment> Attach(TAttachment a, ImageView img)
        {
            _images.Add(a, img);
            return this;
        }

        public Framebuffer Build()
        {
            var images = new ImageView[_images.Count];
            foreach (var kv in _images)
                images[_attachmentMap(kv.Key)] = kv.Value;
            return new Framebuffer(_pass, _size, _layers, images);
        }
    }
}