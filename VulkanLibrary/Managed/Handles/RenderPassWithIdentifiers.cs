﻿using System.Collections.Generic;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class RenderPassWithIdentifiers<TAttachment, TPass> : RenderPass
    {
        private readonly Dictionary<TAttachment, uint> _attachmentToId;
        private readonly TAttachment[] _idToAttachment;
        private readonly Dictionary<TPass, uint> _passToId;
        private readonly TPass[] _idToPass;

        public uint PassCount => (uint) _idToPass.Length;

        public uint AttachmentCount => (uint) _idToAttachment.Length;

        public TPass Pass(uint id)
        {
            return _idToPass[id];
        }

        public uint PassId(TPass pass)
        {
            return _passToId[pass];
        }

        public TAttachment Attachment(uint id)
        {
            return _idToAttachment[id];
        }

        public uint AttachmentId(TAttachment attachment)
        {
            return _attachmentToId[attachment];
        }

        public RenderPassWithIdentifiers(Device dev, VkRenderPassCreateInfo info,
            TAttachment[] attachments, TPass[] passes) : base(dev, info)
        {
            _idToAttachment = attachments;
            _attachmentToId = new Dictionary<TAttachment, uint>(attachments.Length);
            for (var i = 0; i < attachments.Length; i++)
                _attachmentToId[attachments[i]] = (uint) i;

            _idToPass = passes;
            _passToId = new Dictionary<TPass, uint>(passes.Length);
            for (var i = 0; i < passes.Length; i++)
                _passToId[passes[i]] = (uint) i;
        }

        public new GraphicsPipelineBuilder PipelineBuilder(TPass subpass, PipelineLayout layout)
        {
            return new GraphicsPipelineBuilder(this, PassId(subpass), layout);
        }

        public new FramebufferBuilder<TAttachment> FramebufferBuilder(VkExtent2D size, uint layers = 1)
        {
            return new FramebufferBuilder<TAttachment>(this, size, AttachmentId, layers);
        }
    }
}