using System.Diagnostics;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public static class RenderPassBuilderBase
    {
        /// <summary>
        /// Helper used for building <see cref="VkSubpassDescription"/>
        /// </summary>
        /// <typeparam name="TBuilder">Builder type</typeparam>
        /// <typeparam name="TAttachment">Attachment ID type</typeparam>
        public interface ISubpassBuilder<out TBuilder, in TAttachment>
            where TBuilder : ISubpassBuilder<TBuilder, TAttachment>
        {
            /// <summary>
            /// Attachments should have their content preserved.
            /// <seealso cref="VkSubpassDescription.PPreserveAttachments"/>
            /// </summary>
            /// <param name="attachments">Attachments to preserve</param>
            /// <returns>this</returns>
            TBuilder PreserveAttachments(params TAttachment[] attachments);

            /// <summary>
            /// Sets the given attachment as an input attachment.
            /// <seealso cref="VkSubpassDescription.PInputAttachments"/>
            /// </summary>
            /// <param name="attachment">Attachment to use as input</param>
            /// <param name="layout">Layout of attachment</param>
            /// <returns>this</returns>
            TBuilder InputAttachment(TAttachment attachment, VkImageLayout layout);

            /// <summary>
            /// Sets the given attachment as a resolve attachment.
            /// <seealso cref="VkSubpassDescription.PResolveAttachments"/>
            /// </summary>
            /// <param name="attachment">Attachment to use as resolve</param>
            /// <param name="layout">Layout of attachment</param>
            /// <returns>this</returns>
            TBuilder ResolveAttachment(TAttachment attachment, VkImageLayout layout);

            /// <summary>
            /// Sets the given attachment as a color attachment.
            /// <seealso cref="VkSubpassDescription.PColorAttachments"/>
            /// </summary>
            /// <param name="attachment">Attachment to use as color</param>
            /// <param name="layout">Layout of attachment</param>
            /// <returns>this</returns>
            TBuilder ColorAttachment(TAttachment attachment, VkImageLayout layout);

            /// <summary>
            /// Sets the given attachment as the depth stencil attachment.
            /// <seealso cref="VkSubpassDescription.PDepthStencilAttachment"/>
            /// </summary>
            /// <param name="attachment">Attachment to use as depth/stencil</param>
            /// <param name="layout">Layout of attachment</param>
            /// <returns>this</returns>
            TBuilder DepthStencilAttachment(TAttachment attachment, VkImageLayout layout);
        }

        /// <summary>
        /// Helper class used for building <see cref="VkSubpassDependency"/>
        /// </summary>
        /// <typeparam name="TBuilder">Builder type</typeparam>
        public class DependencyBuilderBase<TBuilder> where TBuilder : DependencyBuilderBase<TBuilder>
        {
            protected VkSubpassDependency _desc;


            /// <summary>
            /// Sets the flags for this dependency.
            /// <seealso cref="VkSubpassDependency.DependencyFlags"/>
            /// </summary>
            /// <param name="flags">Dependency flags</param>
            /// <returns>this</returns>
            public TBuilder Flags(VkDependencyFlag flags)
            {
                _desc.DependencyFlags = flags;
                return (TBuilder) this;
            }

            /// <summary>
            /// Sets the source access mask for this dependency.
            /// <seealso cref="VkSubpassDependency.SrcAccessMask"/>
            /// </summary>
            /// <param name="src">source access mask</param>
            /// <returns>this</returns>
            public TBuilder SrcAccess(VkAccessFlag src)
            {
                _desc.SrcAccessMask = src;
                return (TBuilder) this;
            }

            /// <summary>
            /// Sets the destination access mask for this dependency.
            /// <seealso cref="VkSubpassDependency.DstAccessMask"/>
            /// </summary>
            /// <param name="dst">destination access mask</param>
            /// <returns>this</returns>
            public TBuilder DstAccess(VkAccessFlag dst)
            {
                _desc.DstAccessMask = dst;
                return (TBuilder) this;
            }

            /// <summary>
            /// Sets the source and destination access masks for this dependency.
            /// <seealso cref="VkSubpassDependency.SrcAccessMask"/>
            /// <seealso cref="VkSubpassDependency.DstAccessMask"/>
            /// </summary>
            /// <param name="src">source access mask</param>
            /// <param name="dst">destination access mask</param>
            /// <returns>this</returns>
            public TBuilder Access(VkAccessFlag src, VkAccessFlag dst)
            {
                return SrcAccess(src).DstAccess(dst);
            }

            /// <summary>
            /// Sets the source stage mask for this dependency.
            /// <seealso cref="VkSubpassDependency.SrcStageMask"/>
            /// </summary>
            /// <param name="src">source stage mask</param>
            /// <returns>this</returns>
            public TBuilder SrcStage(VkPipelineStageFlag src)
            {
                _desc.SrcStageMask = src;
                Debug.Assert(_desc.SrcSubpass == Vulkan.SubpassExternal ||
                             (_desc.SrcStageMask & VkPipelineStageFlag.Host) == 0);
                return (TBuilder) this;
            }

            /// <summary>
            /// Sets the destination stage mask for this dependency.
            /// <seealso cref="VkSubpassDependency.DstStageMask"/>
            /// </summary>
            /// <param name="dst">destination stage mask</param>
            /// <returns>this</returns>
            public TBuilder DstStage(VkPipelineStageFlag dst)
            {
                _desc.DstStageMask = dst;
                Debug.Assert(_desc.DstSubpass == Vulkan.SubpassExternal ||
                             (_desc.DstStageMask & VkPipelineStageFlag.Host) == 0);
                return (TBuilder) this;
            }

            /// <summary>
            /// Sets the source and destination stage masks for this dependency.
            /// <seealso cref="VkSubpassDependency.SrcStageMask"/>
            /// <seealso cref="VkSubpassDependency.DstStageMask"/>
            /// </summary>
            /// <param name="src">source stage mask</param>
            /// <param name="dst">destination stage mask</param>
            /// <returns>this</returns>
            public TBuilder Stage(VkPipelineStageFlag src, VkPipelineStageFlag dst)
            {
                return SrcStage(src).DstStage(dst);
            }
        }
    }
}