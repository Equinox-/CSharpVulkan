using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class RenderPassBuilder : IDisposable
    {
        private readonly Device _dev;
        private readonly List<VkAttachmentDescription> _attachments = new List<VkAttachmentDescription>();
        private readonly List<VkSubpassDescription> _subpasses = new List<VkSubpassDescription>();
        private readonly List<VkSubpassDependency> _dependencies = new List<VkSubpassDependency>();
        private uint _maxReferencedAttachment;
        private readonly List<GCHandle> _pins = new List<GCHandle>();

        internal RenderPassBuilder(Device dev)
        {
            _dev = dev;
        }

        /// <inheritdoc/>
        public class AttachmentBuilder : AttachmentBuilderBase<AttachmentBuilder>
        {
            private readonly RenderPassBuilder _builder;

            internal AttachmentBuilder(RenderPassBuilder builder, VkFormat format, VkImageLayout finalLayout)
            {
                _builder = builder;
                _desc = new VkAttachmentDescription()
                {
                    Format = format,
                    FinalLayout = finalLayout,
                    InitialLayout = VkImageLayout.Undefined,
                    Flags = 0,
                    LoadOp = VkAttachmentLoadOp.DontCare,
                    StoreOp = VkAttachmentStoreOp.DontCare,
                    StencilLoadOp = VkAttachmentLoadOp.DontCare,
                    StencilStoreOp = VkAttachmentStoreOp.DontCare
                };
            }

            /// <summary>
            /// Commits this attachment to the render pass builder
            /// </summary>
            /// <returns>Render pass builder</returns>
            public RenderPassBuilder Commit()
            {
                _builder._attachments.Add(_desc);
                return _builder;
            }
        }

        /// <summary>
        /// Starts building a new attachment for this render pass
        /// </summary>
        /// <param name="format">attachment's format</param>
        /// <param name="finalLayout">attachment's final image layout</param>
        /// <returns>attachment builder</returns>
        public AttachmentBuilder WithAttachment(VkFormat format, VkImageLayout finalLayout)
        {
            return new AttachmentBuilder(this, format, finalLayout);
        }

        /// <inheritdoc/>
        public class SubpassBuilder : RenderPassBuilderBase.ISubpassBuilder<SubpassBuilder, uint>
        {
            private readonly RenderPassBuilder _builder;
            private VkSubpassDescription _desc;
            private readonly List<VkAttachmentReference> _inputAttachmentReferences = new List<VkAttachmentReference>();
            private readonly List<VkAttachmentReference> _colorAttachmentReferences = new List<VkAttachmentReference>();

            private readonly List<VkAttachmentReference> _resolveAttachmentReferences =
                new List<VkAttachmentReference>();

            private VkAttachmentReference? _depthStencilAttachment = null;
            private readonly List<uint> _preservedAttachments = new List<uint>();

            internal SubpassBuilder(RenderPassBuilder builder)
            {
                _builder = builder;
                _desc = new VkSubpassDescription()
                {
                    PipelineBindPoint = VkPipelineBindPoint.Graphics,
                    Flags = 0
                };
            }

            /// <inheritdoc/>
            public SubpassBuilder PreserveAttachments(params uint[] attachments)
            {
                _preservedAttachments.AddRange(attachments);
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder InputAttachment(uint attachment, VkImageLayout layout)
            {
                _inputAttachmentReferences.Add(new VkAttachmentReference() {Attachment = attachment, Layout = layout});
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder ResolveAttachment(uint attachment, VkImageLayout layout)
            {
                _resolveAttachmentReferences.Add(new VkAttachmentReference()
                {
                    Attachment = attachment,
                    Layout = layout
                });
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder ColorAttachment(uint attachment, VkImageLayout layout)
            {
                _colorAttachmentReferences.Add(new VkAttachmentReference() {Attachment = attachment, Layout = layout});
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder DepthStencilAttachment(uint attachment, VkImageLayout layout)
            {
                Debug.Assert(!_depthStencilAttachment.HasValue);
                _depthStencilAttachment = new VkAttachmentReference() {Attachment = attachment, Layout = layout};
                return this;
            }

            /// <summary>
            /// Commits this subpass into the render pass builder
            /// </summary>
            /// <returns>The render pass builder</returns>
            public RenderPassBuilder Commit()
            {
                unsafe
                {
                    _builder._maxReferencedAttachment =
                        Math.Max(_builder._maxReferencedAttachment, _preservedAttachments.Max());
                    if (_preservedAttachments.Count > 0)
                    {
                        var array = _preservedAttachments.ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.PreserveAttachmentCount = (uint) array.Length;
                        _desc.PPreserveAttachments =
                            (uint*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        _builder._pins.Add(pin);
                    }
                    else
                    {
                        _desc.PPreserveAttachments = (uint*) 0;
                        _desc.PreserveAttachmentCount = 0;
                    }

                    _builder._maxReferencedAttachment = Math.Max(_builder._maxReferencedAttachment,
                        _inputAttachmentReferences.Max(x => x.Attachment));
                    if (_inputAttachmentReferences.Count > 0)
                    {
                        var array = _inputAttachmentReferences.ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.InputAttachmentCount = (uint) array.Length;
                        _desc.PInputAttachments =
                            (VkAttachmentReference*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        _builder._pins.Add(pin);
                    }
                    else
                    {
                        _desc.PInputAttachments = (VkAttachmentReference*) 0;
                        _desc.InputAttachmentCount = 0;
                    }

                    _builder._maxReferencedAttachment = Math.Max(_builder._maxReferencedAttachment,
                        _colorAttachmentReferences.Max(x => x.Attachment));
                    if (_colorAttachmentReferences.Count > 0)
                    {
                        var array = _colorAttachmentReferences.ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.ColorAttachmentCount = (uint) array.Length;
                        _desc.PColorAttachments =
                            (VkAttachmentReference*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        _builder._pins.Add(pin);
                    }
                    else
                    {
                        _desc.PColorAttachments = (VkAttachmentReference*) 0;
                        _desc.ColorAttachmentCount = 0;
                    }

                    if (_depthStencilAttachment.HasValue)
                    {
                        _builder._maxReferencedAttachment = Math.Max(_builder._maxReferencedAttachment,
                            _depthStencilAttachment.Value.Attachment);
                        var pin = GCHandle.Alloc(_depthStencilAttachment.Value, GCHandleType.Pinned);
                        _desc.PDepthStencilAttachment = (VkAttachmentReference*) pin.AddrOfPinnedObject();
                        _builder._pins.Add(pin);
                    }
                    else
                    {
                        _desc.PDepthStencilAttachment = (VkAttachmentReference*) 0;
                    }

                    _builder._maxReferencedAttachment = Math.Max(_builder._maxReferencedAttachment,
                        _resolveAttachmentReferences.Max(x => x.Attachment));
                    if (_resolveAttachmentReferences.Count > 0)
                    {
                        Debug.Assert(_resolveAttachmentReferences.Count == _colorAttachmentReferences.Count);
                        var array = _resolveAttachmentReferences.ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.PColorAttachments =
                            (VkAttachmentReference*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        _builder._pins.Add(pin);
                    }
                    else
                    {
                        _desc.PResolveAttachments = (VkAttachmentReference*) 0;
                    }
                }
                _builder._subpasses.Add(_desc);
                return _builder;
            }
        }

        /// <summary>
        /// Starts building a new subpass for this render pass
        /// </summary>
        /// <returns>the subpass builder</returns>
        public SubpassBuilder WithSubpass()
        {
            return new SubpassBuilder(this);
        }

        /// <inheritdoc/>
        public class DependencyBuilder : RenderPassBuilderBase.DependencyBuilderBase<DependencyBuilder>
        {
            private readonly RenderPassBuilder _builder;

            internal DependencyBuilder(RenderPassBuilder builder, uint srcPass, uint dstPass)
            {
                Debug.Assert(srcPass != Vulkan.SubpassExternal || dstPass != Vulkan.SubpassExternal);
                if (srcPass != Vulkan.SubpassExternal && dstPass != Vulkan.SubpassExternal)
                    Debug.Assert(srcPass <= dstPass);
                _builder = builder;
                _desc = new VkSubpassDependency()
                {
                    SrcSubpass = srcPass,
                    DstSubpass = dstPass,
                    DependencyFlags = 0,
                    DstAccessMask = 0,
                    DstStageMask = 0,
                    SrcAccessMask = 0,
                    SrcStageMask = 0
                };
            }

            /// <summary>
            /// Commits this dependency into the render pass builder
            /// </summary>
            /// <returns>render pass builder</returns>
            public RenderPassBuilder Commit()
            {
                _builder._dependencies.Add(_desc);
                return _builder;
            }
        }

        /// <summary>
        /// Starts building a dependency between the given subpasses
        /// </summary>
        /// <param name="src">source "depended on" subpass</param>
        /// <param name="dst">destination "dependent" subpass</param>
        /// <returns>the dependency builder</returns>
        public DependencyBuilder Dependency(uint src, uint dst)
        {
            return new DependencyBuilder(this, src, dst);
        }

        /// <summary>
        /// Builds a new render pass and disposes this builder.
        /// </summary>
        /// <returns>the new render pass</returns>
        public RenderPass Build()
        {
            Debug.Assert(_dependencies.All(x =>
                (x.SrcSubpass < _subpasses.Count || x.SrcSubpass == Vulkan.SubpassExternal) &&
                (x.DstSubpass < _subpasses.Count || x.DstSubpass == Vulkan.SubpassExternal)));
            Debug.Assert(_maxReferencedAttachment < _attachments.Count);

            var arrayAttach = _attachments.ToArray();
            var pinAttach = arrayAttach.Length > 0
                ? GCHandle.Alloc(arrayAttach, GCHandleType.Pinned)
                : default(GCHandle);
            var arraySubpass = _subpasses.ToArray();
            var pinSubpass = arraySubpass.Length > 0
                ? GCHandle.Alloc(arraySubpass, GCHandleType.Pinned)
                : default(GCHandle);
            var arrayDeps = _dependencies.ToArray();
            var pinDeps = arrayDeps.Length > 0 ? GCHandle.Alloc(arrayDeps, GCHandleType.Pinned) : default(GCHandle);
            try
            {
                unsafe
                {
                    var info = new VkRenderPassCreateInfo()
                    {
                        SType = VkStructureType.RenderPassCreateInfo,
                        Flags = 0,
                        PNext = (void*) 0,
                        AttachmentCount = (uint) arrayAttach.Length,
                        PAttachments = arrayAttach.Length > 0
                            ? (VkAttachmentDescription*) Marshal.UnsafeAddrOfPinnedArrayElement(arrayAttach, 0)
                                .ToPointer()
                            : (VkAttachmentDescription*) 0,
                        SubpassCount = (uint) arraySubpass.Length,
                        PSubpasses = arraySubpass.Length > 0
                            ? (VkSubpassDescription*) Marshal.UnsafeAddrOfPinnedArrayElement(arraySubpass, 0)
                                .ToPointer()
                            : (VkSubpassDescription*) 0,
                        DependencyCount = (uint) arrayDeps.Length,
                        PDependencies = arrayDeps.Length > 0
                            ? (VkSubpassDependency*) Marshal.UnsafeAddrOfPinnedArrayElement(arrayDeps, 0)
                                .ToPointer()
                            : (VkSubpassDependency*) 0,
                    };
                    return new RenderPass(_dev, info);
                }
            }
            finally
            {
                if (arrayAttach.Length > 0)
                    pinAttach.Free();
                if (arraySubpass.Length > 0)
                    pinSubpass.Free();
                if (arrayDeps.Length > 0)
                    pinDeps.Free();
                Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var pin in _pins)
                pin.Free();
            _pins.Clear();
        }

        public class AttachmentBuilderBase<TBuilder> where TBuilder : AttachmentBuilderBase<TBuilder>
        {
            protected VkAttachmentDescription _desc;

            /// <summary>
            /// Specify the initial layout for this attachment 
            /// </summary>
            /// <param name="initial">Initial layout</param>
            /// <returns>this</returns>
            public TBuilder InitialLayout(VkImageLayout initial)
            {
                _desc.InitialLayout = initial;
                return (TBuilder) this;
            }

            /// <summary>
            /// Specifies if the attachment may alias physical memory of another attachment in the same render pass.
            /// <see cref="VkAttachmentDescriptionFlag.MayAlias"/>
            /// </summary>
            /// <param name="flag">may alias</param>
            /// <returns>this</returns>
            public TBuilder MayAlias(bool flag)
            {
                if (!flag)
                    _desc.Flags = (_desc.Flags & ~VkAttachmentDescriptionFlag.MayAlias);
                else
                    _desc.Flags |= VkAttachmentDescriptionFlag.MayAlias;
                return (TBuilder) this;
            }

            /// <summary>
            /// Specifies the load operation of this attachment.
            /// </summary>
            /// <param name="op">Load operation</param>
            /// <param name="stencilOp">Stencil load operation, or null if use <c>op</c></param>
            /// <returns>this</returns>
            public TBuilder LoadOp(VkAttachmentLoadOp op, VkAttachmentLoadOp? stencilOp = null)
            {
                if (!stencilOp.HasValue)
                    stencilOp = op;
                _desc.LoadOp = op;
                _desc.StencilLoadOp = stencilOp.Value;
                return (TBuilder) this;
            }

            /// <summary>
            /// Specifies the store operation of this attachment.
            /// </summary>
            /// <param name="op">Store operation</param>
            /// <param name="stencilOp">Stencil store operation, or null if use <c>op</c></param>
            /// <returns>this</returns>
            public TBuilder StoreOp(VkAttachmentStoreOp op, VkAttachmentStoreOp? stencilOp = null)
            {
                if (!stencilOp.HasValue)
                    stencilOp = op;
                _desc.StoreOp = op;
                _desc.StencilStoreOp = stencilOp.Value;
                return (TBuilder) this;
            }
        }
    }
}