using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class RenderPassWithIdentifiersBuilder<TAttachment, TPass>
    {
        private readonly Device _dev;

        private readonly List<TAttachment> _attachmentOrder = new List<TAttachment>();

        private readonly Dictionary<TAttachment, VkAttachmentDescription> _attachmentDescriptions =
            new Dictionary<TAttachment, VkAttachmentDescription>();

        private readonly Dictionary<TPass, SubpassBuilder> _subpassBuilders = new Dictionary<TPass, SubpassBuilder>();
        private readonly List<DependencyBuilder> _dependencyBuilders = new List<DependencyBuilder>();

        internal RenderPassWithIdentifiersBuilder(Device dev)
        {
            _dev = dev;
        }

        /// <inheritdoc/>
        public class AttachmentBuilder : RenderPassBuilderBase.AttachmentBuilderBase<AttachmentBuilder>
        {
            private readonly RenderPassWithIdentifiersBuilder<TAttachment, TPass> _builder;
            private readonly TAttachment _id;

            internal AttachmentBuilder(RenderPassWithIdentifiersBuilder<TAttachment, TPass> builder, TAttachment id,
                VkFormat format, VkImageLayout finalLayout, VkSampleCountFlag samples)
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
                    StencilStoreOp = VkAttachmentStoreOp.DontCare,
                    Samples = samples
                };
            }

            /// <summary>
            /// Commits this attachment to the render pass builder
            /// </summary>
            /// <returns>Render pass builder</returns>
            public RenderPassWithIdentifiersBuilder<TAttachment, TPass> Commit()
            {
                _builder._attachmentDescriptions.Add(_id, _desc);
                return _builder;
            }
        }

        /// <summary>
        /// Starts building a new attachment for this render pass
        /// </summary>
        /// <param name="id">identifier for the attachment</param>
        /// <param name="format">attachment's format</param>
        /// <param name="finalLayout">attachment's final image layout</param>
        /// <param name="samples">attachment's sample count</param>
        /// <returns>attachment builder</returns>
        public AttachmentBuilder WithAttachment(TAttachment id, VkFormat format, VkImageLayout finalLayout,
            VkSampleCountFlag samples = VkSampleCountFlag.Count1)
        {
            Debug.Assert(!_attachmentOrder.Contains(id));
            _attachmentOrder.Add(id);
            return new AttachmentBuilder(this, id, format, finalLayout, samples);
        }

        private struct LazyAttachmentReference
        {
            public TAttachment Id;
            public VkImageLayout Layout;
        }

        /// <inheritdoc/>
        public class SubpassBuilder : RenderPassBuilderBase.ISubpassBuilder<SubpassBuilder, TAttachment>
        {
            private readonly RenderPassWithIdentifiersBuilder<TAttachment, TPass> _builder;
            private readonly TPass _id;
            private VkSubpassDescription _desc;

            private readonly List<LazyAttachmentReference> _inputAttachmentReferences =
                new List<LazyAttachmentReference>();

            private readonly List<LazyAttachmentReference> _colorAttachmentReferences =
                new List<LazyAttachmentReference>();

            private readonly List<LazyAttachmentReference> _resolveAttachmentReferences =
                new List<LazyAttachmentReference>();

            private LazyAttachmentReference? _depthStencilAttachment = null;
            private readonly List<TAttachment> _preservedAttachments = new List<TAttachment>();

            internal SubpassBuilder(RenderPassWithIdentifiersBuilder<TAttachment, TPass> builder, TPass id)
            {
                _id = id;
                _builder = builder;
                _desc = new VkSubpassDescription()
                {
                    PipelineBindPoint = VkPipelineBindPoint.Graphics,
                    Flags = 0
                };
            }

            /// <inheritdoc/>
            public SubpassBuilder PreserveAttachments(params TAttachment[] attachments)
            {
                _preservedAttachments.AddRange(attachments);
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder InputAttachment(TAttachment attachment, VkImageLayout layout)
            {
                _inputAttachmentReferences.Add(new LazyAttachmentReference() {Id = attachment, Layout = layout});
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder ResolveAttachment(TAttachment attachment, VkImageLayout layout)
            {
                _resolveAttachmentReferences.Add(new LazyAttachmentReference()
                {
                    Id = attachment,
                    Layout = layout
                });
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder ColorAttachment(TAttachment attachment,
                VkImageLayout layout = VkImageLayout.ColorAttachmentOptimal)
            {
                _colorAttachmentReferences.Add(new LazyAttachmentReference() {Id = attachment, Layout = layout});
                return this;
            }

            /// <inheritdoc/>
            public SubpassBuilder DepthStencilAttachment(TAttachment attachment, VkImageLayout layout)
            {
                Debug.Assert(!_depthStencilAttachment.HasValue);
                _depthStencilAttachment = new LazyAttachmentReference() {Id = attachment, Layout = layout};
                return this;
            }

            internal VkSubpassDescription Build(List<GCHandle> pins, Dictionary<TAttachment, uint> attachmentTable)
            {
                VkAttachmentReference LazyResolver(LazyAttachmentReference x) => new VkAttachmentReference()
                {
                    Attachment = attachmentTable[x.Id],
                    Layout = x.Layout
                };

                unsafe
                {
                    if (_preservedAttachments.Count > 0)
                    {
                        var array = _preservedAttachments.Select(x => attachmentTable[x]).ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.PreserveAttachmentCount = (uint) array.Length;
                        _desc.PPreserveAttachments =
                            (uint*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        pins.Add(pin);
                    }
                    else
                    {
                        _desc.PPreserveAttachments = (uint*) 0;
                        _desc.PreserveAttachmentCount = 0;
                    }

                    if (_inputAttachmentReferences.Count > 0)
                    {
                        var array = _inputAttachmentReferences.Select(LazyResolver).ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.InputAttachmentCount = (uint) array.Length;
                        _desc.PInputAttachments =
                            (VkAttachmentReference*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        pins.Add(pin);
                    }
                    else
                    {
                        _desc.PInputAttachments = (VkAttachmentReference*) 0;
                        _desc.InputAttachmentCount = 0;
                    }

                    if (_colorAttachmentReferences.Count > 0)
                    {
                        var array = _colorAttachmentReferences.Select(LazyResolver).ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.ColorAttachmentCount = (uint) array.Length;
                        _desc.PColorAttachments =
                            (VkAttachmentReference*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        pins.Add(pin);
                    }
                    else
                    {
                        _desc.PColorAttachments = (VkAttachmentReference*) 0;
                        _desc.ColorAttachmentCount = 0;
                    }

                    if (_depthStencilAttachment.HasValue)
                    {
                        var pin = GCHandle.Alloc(LazyResolver(_depthStencilAttachment.Value), GCHandleType.Pinned);
                        _desc.PDepthStencilAttachment = (VkAttachmentReference*) pin.AddrOfPinnedObject();
                        pins.Add(pin);
                    }
                    else
                    {
                        _desc.PDepthStencilAttachment = (VkAttachmentReference*) 0;
                    }

                    if (_resolveAttachmentReferences.Count > 0)
                    {
                        Debug.Assert(_resolveAttachmentReferences.Count == _colorAttachmentReferences.Count);
                        var array = _resolveAttachmentReferences.Select(LazyResolver).ToArray();
                        var pin = GCHandle.Alloc(array, GCHandleType.Pinned);
                        _desc.PColorAttachments =
                            (VkAttachmentReference*) Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToPointer();
                        pins.Add(pin);
                    }
                    else
                    {
                        _desc.PResolveAttachments = (VkAttachmentReference*) 0;
                    }
                }
                return _desc;
            }

            /// <summary>
            /// Commits this subpass to the render pass builder
            /// </summary>
            /// <returns>the render pass builder</returns>
            public RenderPassWithIdentifiersBuilder<TAttachment, TPass> Commit()
            {
                _builder._subpassBuilders.Add(_id, this);
                return _builder;
            }
        }

        /// <summary>
        /// Starts building a new subpass for this render pass
        /// </summary>
        /// <returns>the subpass</returns>
        public SubpassBuilder WithSubpass(TPass id)
        {
            return new SubpassBuilder(this, id);
        }

        /// <inheritdoc/>
        public class DependencyBuilder : RenderPassBuilderBase.DependencyBuilderBase<DependencyBuilder>
        {
            private readonly RenderPassWithIdentifiersBuilder<TAttachment, TPass> _builder;
            internal readonly TPass SrcPass, DstPass;
            internal readonly bool SrcExternal, DstExternal;
            private VkSubpassDependency _desc;

            internal DependencyBuilder(RenderPassWithIdentifiersBuilder<TAttachment, TPass> builder, TPass srcPass,
                TPass dstPass, bool srcExternal,
                bool dstExternal)
            {
                SrcPass = srcPass;
                DstPass = dstPass;
                SrcExternal = srcExternal;
                DstExternal = dstExternal;
                Debug.Assert(!srcExternal || !dstExternal);
                _builder = builder;
                _desc = new VkSubpassDependency()
                {
                    DependencyFlags = 0,
                    DstAccessMask = 0,
                    DstStageMask = VkPipelineStageFlag.AllCommands,
                    SrcAccessMask = 0,
                    SrcStageMask = VkPipelineStageFlag.AllCommands
                };
            }

            internal VkSubpassDependency Build(Dictionary<TPass, uint> lookup)
            {
                _desc.SrcSubpass = SrcExternal ? Vulkan.SubpassExternal : lookup[SrcPass];
                _desc.DstSubpass = DstExternal ? Vulkan.SubpassExternal : lookup[DstPass];
                return _desc;
            }

            /// <summary>
            /// Commits this dependency to the render pass builder
            /// </summary>
            /// <returns>the render pass builder</returns>
            public RenderPassWithIdentifiersBuilder<TAttachment, TPass> Commit()
            {
                _builder._dependencyBuilders.Add(this);
                return _builder;
            }
        }

        /// <summary>
        /// Creates a new dependency where <c>dst</c> depends on <c>src</c>
        /// </summary>
        /// <param name="src">Depended upon</param>
        /// <param name="dst">Dependent</param>
        /// <returns>dependency builder</returns>
        public DependencyBuilder Dependency(TPass src, TPass dst)
        {
            return new DependencyBuilder(this, src, dst, false, false);
        }

        /// <summary>
        /// Creates a new dependency where <c>dst</c> depends on the external pipeline.
        /// </summary>
        /// <param name="dst">Dependent</param>
        /// <returns>dependency builder</returns>
        public DependencyBuilder DependencyOnExternal(TPass dst)
        {
            return new DependencyBuilder(this, default(TPass), dst, true, false);
        }

        /// <summary>
        /// Creates a new dependency where the external pipeline depends on <c>src</c>
        /// </summary>
        /// <param name="src">Depended upon</param>
        /// <returns>dependency builder</returns>
        public DependencyBuilder DependencyByExternal(TPass src)
        {
            return new DependencyBuilder(this, src, default(TPass), false, true);
        }

        /// <summary>
        /// Builds a new render pass
        /// </summary>
        /// <returns>the new render pass</returns>
        public RenderPassWithIdentifiers<TAttachment, TPass> Build()
        {
            Dictionary<TPass, int> passInFactory = new Dictionary<TPass, int>();
            foreach (var pass in _subpassBuilders)
                passInFactory.Add(pass.Key, 0);
            foreach (var dep in _dependencyBuilders)
                if (!dep.DstExternal && !dep.SrcExternal)
                    passInFactory[dep.DstPass]++;
            var passComparer = EqualityComparer<TPass>.Default;
            List<TPass> passOrder = new List<TPass>();
            while (passOrder.Count < _subpassBuilders.Count)
            {
                var insert = passInFactory.Where(x => x.Value == 0).Select(x => x.Key).ToList();
                if (insert.Count == 0)
                    throw new Exception($"Circular dependency detected in {string.Join(", ", passInFactory)}.");
                passOrder.AddRange(insert);
                foreach (var k in insert)
                foreach (var dep in _dependencyBuilders.Where(x =>
                    !x.SrcExternal && !x.DstExternal && passComparer.Equals(x.SrcPass, k)))
                    passInFactory[dep.DstPass]--;
            }
            var attachmentToId = _attachmentOrder.Select((x, i) => new KeyValuePair<TAttachment, uint>(x, (uint) i))
                .ToDictionary(a => a.Key, b => b.Value);

            var passToId = passOrder.Select((x, i) => new KeyValuePair<TPass, uint>(x, (uint) i))
                .ToDictionary(a => a.Key, b => b.Value);

            var pins = new List<GCHandle>();
            var attachmentDesc = new VkAttachmentDescription[_attachmentOrder.Count];
            var passDesc = new VkSubpassDescription[passOrder.Count];
            var dependencyDesc = new VkSubpassDependency[_dependencyBuilders.Count];
            try
            {
                foreach (var attachment in attachmentToId)
                    attachmentDesc[attachment.Value] = _attachmentDescriptions[attachment.Key];
                foreach (var pass in passToId)
                    passDesc[pass.Value] = _subpassBuilders[pass.Key].Build(pins, attachmentToId);
                var i = 0;
                foreach (var dep in _dependencyBuilders)
                    dependencyDesc[i++] = dep.Build(passToId);
                Debug.Assert(i == dependencyDesc.Length);
                Array.Sort(dependencyDesc,
                    (a, b) => a.SrcSubpass != Vulkan.SubpassExternal
                        ? a.SrcSubpass.CompareTo(b.SrcSubpass)
                        : a.DstSubpass.CompareTo(b.DstSubpass));
                
                unsafe
                {
                    fixed (VkAttachmentDescription* attachPtr = attachmentDesc)
                    fixed (VkSubpassDescription* passPtr = passDesc)
                    fixed (VkSubpassDependency* depPtr = dependencyDesc)
                    {
                        var info = new VkRenderPassCreateInfo()
                        {
                            SType = VkStructureType.RenderPassCreateInfo,
                            Flags = 0,
                            PNext = (void*) 0,
                            AttachmentCount = (uint) attachmentDesc.Length,
                            PAttachments = attachPtr,
                            SubpassCount = (uint) passDesc.Length,
                            PSubpasses = passPtr,
                            DependencyCount = (uint) dependencyDesc.Length,
                            PDependencies = depPtr,
                        };
                        return new RenderPassWithIdentifiers<TAttachment, TPass>(_dev, info, _attachmentOrder.ToArray(),
                            passOrder.ToArray());
                    }
                }
            }
            finally
            {
                foreach (var pin in pins)
                    pin.Free();
            }
        }
    }
}