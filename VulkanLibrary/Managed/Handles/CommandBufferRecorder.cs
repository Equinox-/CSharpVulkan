using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Buffers;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class CommandBufferRecorder<T> where T : CommandBuffer
    {
        private readonly T _buffer;

        internal CommandBufferRecorder(T buffer)
        {
            _buffer = buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertValid()
        {
            _buffer.AssertBuilding();
        }

        /// <summary>
        /// The handle to record commands on
        /// </summary>
        public VkCommandBuffer Handle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                AssertValid();
                return _buffer.Handle;
            }
        }

        public CommandBufferRecorder<T> DynamicViewport(uint firstViewport, Span<VkViewport> viewports)
        {
            unsafe
            {
                fixed (VkViewport* viewport = &viewports.DangerousGetPinnableReference())
                    VkCommandBuffer.vkCmdSetViewport(Handle, firstViewport, (uint) viewports.Length,
                        viewport);
            }

            return this;
        }

        public CommandBufferRecorder<T> DynamicScissors(uint firstScissor, Span<VkRect2D> scissors)
        {
            unsafe
            {
                fixed (VkRect2D* scissor = &scissors.DangerousGetPinnableReference())
                    VkCommandBuffer.vkCmdSetScissor(Handle, firstScissor, (uint) scissors.Length,
                        scissor);
            }

            return this;
        }

        /// <summary>
        /// To copy data between buffer objects, call:
        /// </summary>
        /// <param name="src">is the source buffer.</param>
        /// <param name="dest">is the destination buffer.</param>
        /// <param name="region">is a pointer to an array of <see cref="VkBufferCopy"/> structures specifying the regions to copy.</param>
        public CommandBufferRecorder<T> CopyBuffer(Buffer src, Buffer dest, VkBufferCopy region)
        {
            unsafe
            {
                VkCommandBuffer.vkCmdCopyBuffer(Handle, src.Handle, dest.Handle, 1, &region);
            }

            return this;
        }

        /// <summary>
        /// To copy data between buffer objects, call:
        /// </summary>
        /// <param name="src">is the source buffer.</param>
        /// <param name="dest">is the destination buffer.</param>
        /// <param name="region">is a pointer to an array of <see cref="VkBufferCopy"/> structures specifying the regions to copy.</param>
        public CommandBufferRecorder<T> CopyBuffer(Buffer src, Buffer dest, Span<VkBufferCopy> region)
        {
            Handle.CopyBuffer(src.Handle, dest.Handle, region);
            return this;
        }

        /// <summary>
        /// To record a pipeline barrier, call:
        /// </summary>
        /// <param name="srcStageMask">is a bitmask of <c>VkPipelineStageFlagBits</c> specifying the <a href='https://www.khronos.org/registry/vulkan/specs/1.0/html/vkspec.html#synchronization-pipeline-stages-masks'> source stage mask</a>.</param>
        /// <param name="dstStageMask">is a bitmask of <c>VkPipelineStageFlagBits</c> specifying the <a href='https://www.khronos.org/registry/vulkan/specs/1.0/html/vkspec.html#synchronization-pipeline-stages-masks'> destination stage mask</a>.</param>
        /// <param name="dependencyFlags">is a bitmask of <c>VkDependencyFlagBits</c> specifying how execution and memory dependencies are formed.</param>
        /// <param name="barrier">is a pointer to an array of <see cref="VkMemoryBarrier"/> structures.</param>
        /// <param name="bufferBarrier">is a pointer to an array of <see cref="VkBufferMemoryBarrier"/> structures.</param>
        /// <param name="imageBarrier">is a pointer to an array of <see cref="VkImageMemoryBarrier"/> structures.</param>
        public CommandBufferRecorder<T> PipelineBarrier(VkPipelineStageFlag srcStageMask,
            VkPipelineStageFlag dstStageMask,
            VkDependencyFlag dependencyFlags, Span<VkMemoryBarrier> barrier,
            Span<VkBufferMemoryBarrier> bufferBarrier,
            Span<VkImageMemoryBarrier> imageBarrier)
        {
            Handle.PipelineBarrier(srcStageMask, dstStageMask, dependencyFlags, barrier, bufferBarrier, imageBarrier);
            return this;
        }

        /// <summary>
        /// To record a pipeline barrier, call:
        /// </summary>
        /// <param name="srcStageMask">is a bitmask of <c>VkPipelineStageFlagBits</c> specifying the <a href='https://www.khronos.org/registry/vulkan/specs/1.0/html/vkspec.html#synchronization-pipeline-stages-masks'> source stage mask</a>.</param>
        /// <param name="dstStageMask">is a bitmask of <c>VkPipelineStageFlagBits</c> specifying the <a href='https://www.khronos.org/registry/vulkan/specs/1.0/html/vkspec.html#synchronization-pipeline-stages-masks'> destination stage mask</a>.</param>
        /// <param name="dependencyFlags">is a bitmask of <c>VkDependencyFlagBits</c> specifying how execution and memory dependencies are formed.</param>
        /// <param name="barrier">is a pointer to an array of <see cref="VkMemoryBarrier"/> structures.</param>
        /// <param name="bufferBarrier">is a pointer to an array of <see cref="VkBufferMemoryBarrier"/> structures.</param>
        /// <param name="imageBarrier">is a pointer to an array of <see cref="VkImageMemoryBarrier"/> structures.</param>
        public CommandBufferRecorder<T> PipelineBarrier(VkPipelineStageFlag srcStageMask,
            VkPipelineStageFlag dstStageMask,
            VkDependencyFlag dependencyFlags, VkMemoryBarrier barrier = default(VkMemoryBarrier),
            VkBufferMemoryBarrier bufferBarrier = default(VkBufferMemoryBarrier),
            VkImageMemoryBarrier imageBarrier = default(VkImageMemoryBarrier))
        {
            unsafe
            {
                VkCommandBuffer.vkCmdPipelineBarrier(Handle, srcStageMask, dstStageMask, dependencyFlags,
                    barrier.SType != VkStructureType.BufferMemoryBarrier ? 0 : 1u, &barrier,
                    bufferBarrier.SType != VkStructureType.BufferMemoryBarrier ? 0 : 1u, &bufferBarrier,
                    imageBarrier.SType != VkStructureType.ImageMemoryBarrier ? 0 : 1u, &imageBarrier);
            }

            return this;
        }

        /// <summary>
        /// To transition to the next subpass in the render pass instance after recording the commands for a subpass, call:
        /// </summary>
        /// <param name="contents">specifies how the commands in the next subpass will be provided, in the same fashion as the corresponding parameter of <see cref="VkCommandBuffer.vkCmdBeginRenderPass"/>.</param>
        public CommandBufferRecorder<T> NextSubpass(VkSubpassContents contents)
        {
            Handle.NextSubpass(contents);
            return this;
        }

        public CommandBufferRecorder<T> BeginRenderPass(RenderPass pass, Framebuffer framebuffer,
            Span<VkClearValue> clearValues,
            VkSubpassContents subpass,
            VkRect2D? renderArea = null)
        {
            framebuffer.AssertValid();
            pass.AssertValid();
            unsafe
            {
                fixed (VkClearValue* clearPtr = &clearValues.DangerousGetPinnableReference())
                {
                    var info = new VkRenderPassBeginInfo()
                    {
                        SType = VkStructureType.RenderPassBeginInfo,
                        PNext = IntPtr.Zero,
                        RenderArea = renderArea ?? new VkRect2D()
                        {
                            Offset = new VkOffset2D() {X = 0, Y = 0},
                            Extent = framebuffer.Size
                        },
                        ClearValueCount = (uint) clearValues.Length,
                        PClearValues = clearPtr,
                        Framebuffer = framebuffer.Handle,
                        RenderPass = pass.Handle
                    };
                    Handle.BeginRenderPass(&info, subpass);
                }
            }

            return this;
        }

        public CommandBufferRecorder<T> BindPipeline(Pipeline pipeline)
        {
            pipeline.AssertValid();
            Handle.BindPipeline(pipeline.PipelineType, pipeline.Handle);
            return this;
        }

        public CommandBufferRecorder<T> BindDescriptorSet(Pipeline pipeline, uint set, VkDescriptorSet descriptorSets,
            uint dynamicOffset)
        {
            unsafe
            {
                return BindDescriptorSets(pipeline, set, new Span<VkDescriptorSet>(&descriptorSets, 1),
                    new Span<uint>(&dynamicOffset, 1));
            }
        }

        public CommandBufferRecorder<T> BindDescriptorSet(Pipeline pipeline, uint set, VkDescriptorSet descriptorSets)
        {
            unsafe
            {
                return BindDescriptorSets(pipeline, set, new Span<VkDescriptorSet>(&descriptorSets, 1),
                    new Span<uint>((void*) 0, 0));
            }
        }

        public CommandBufferRecorder<T> BindDescriptorSets(Pipeline pipeline, uint firstSet,
            Span<VkDescriptorSet> pDescriptorSets,
            Span<uint> pDynamicOffsets)
        {
            Handle.BindDescriptorSets(pipeline.PipelineType, pipeline.Layout.Handle, firstSet, pDescriptorSets,
                pDynamicOffsets);
            return this;
        }

        public CommandBufferRecorder<T> BindDescriptorSets(Pipeline pipeline, uint firstSet,
            Span<VkDescriptorSet> pDescriptorSets)
        {
            unsafe
            {
                Handle.BindDescriptorSets(pipeline.PipelineType, pipeline.Layout.Handle, firstSet, pDescriptorSets,
                    new Span<uint>((void*) 0, 0));
            }

            return this;
        }

        /// <summary>
        /// Binds a vertex buffer to this command buffer
        /// </summary>
        /// <param name="binding">binding</param>
        /// <param name="buffer">buffer to bind</param>
        /// <param name="offset">offset into buffer</param>
        /// <returns>this</returns>
        public CommandBufferRecorder<T> BindVertexBuffer(uint binding, IBindableBuffer buffer, ulong offset = 0)
        {
            var bh = buffer.BindingHandle;
            offset += buffer.Offset;
            unsafe
            {
                VkCommandBuffer.vkCmdBindVertexBuffers(Handle, binding, 1, &bh, &offset);
            }
            return this;
        }

        /// <summary>
        /// Binds an index buffer to this command buffer
        /// </summary>
        /// <param name="buffer">buffer to bind</param>
        /// <param name="offset">offset into buffer</param>
        /// <returns>this</returns>
        public CommandBufferRecorder<T> BindIndexBuffer(IBindableBuffer buffer, VkIndexType type, ulong offset = 0)
        {
            var bh = buffer.BindingHandle;
            offset += buffer.Offset;
            VkCommandBuffer.vkCmdBindIndexBuffer(Handle, bh, offset, type);
            return this;
        }

        /// <summary>
        /// To bind vertex buffers to a command buffer for use in subsequent draw commands, call:
        /// </summary>
        /// <param name="firstBinding">is the index of the first vertex input binding whose state is updated by the command.</param>
        /// <param name="pBuffers">is a pointer to an array of buffer handles.</param>
        /// <param name="pOffsets">is a pointer to an array of buffer offsets.</param>
        /// <returns>this</returns>
        public CommandBufferRecorder<T> BindVertexBuffers(uint firstBinding, Buffer[] pBuffers, Span<ulong> pOffsets)
        {
            Handle.BindVertexBuffers(firstBinding, pBuffers.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray(), pOffsets);
            return this;
        }

        /// <summary>
        /// To record a non-indexed draw, call:
        /// </summary>
        /// <param name="vertexCount">is the number of vertices to draw.</param>
        /// <param name="instanceCount">is the number of instances to draw.</param>
        /// <param name="firstVertex">is the index of the first vertex to draw.</param>
        /// <param name="firstInstance">is the instance ID of the first instance to draw.</param>
        public CommandBufferRecorder<T> Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        {
            Handle.Draw(vertexCount, instanceCount, firstVertex, firstInstance);
            return this;
        }

        /// <summary>
        /// To record an indexed draw, call:
        /// </summary>
        /// <param name="indexCount">is the number of vertices to draw.</param>
        /// <param name="instanceCount">is the number of instances to draw.</param>
        /// <param name="firstIndex">is the base index within the index buffer.</param>
        /// <param name="vertexOffset">is the value added to the vertex index before indexing into the vertex buffer.</param>
        /// <param name="firstInstance">is the instance ID of the first instance to draw.</param>
        public CommandBufferRecorder<T> DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex,
            int vertexOffset,
            uint firstInstance)
        {
            Handle.DrawIndexed(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
            return this;
        }

        /// <summary>
        /// To record a non-indexed indirect draw, call:
        /// </summary>
        /// <param name="buffer">is the buffer containing draw parameters.</param>
        /// <param name="offset">is the byte offset into buffer where parameters begin.</param>
        /// <param name="drawCount">is the number of draws to execute, and can: be zero.</param>
        /// <param name="stride">is the byte stride between successive sets of draw parameters.</param>
        public CommandBufferRecorder<T> DrawIndirect(VkBuffer buffer, ulong offset, uint drawCount, uint stride)
        {
            Handle.DrawIndirect(buffer, offset, drawCount, stride);
            return this;
        }

        /// <summary>
        /// To record an indexed indirect draw, call:
        /// </summary>
        /// <param name="buffer">is the buffer containing draw parameters.</param>
        /// <param name="offset">is the byte offset into buffer where parameters begin.</param>
        /// <param name="drawCount">is the number of draws to execute, and can: be zero.</param>
        /// <param name="stride">is the byte stride between successive sets of draw parameters.</param>
        public CommandBufferRecorder<T> DrawIndexedIndirect(VkBuffer buffer, ulong offset, uint drawCount, uint stride)
        {
            Handle.DrawIndexedIndirect(buffer, offset, drawCount, stride);
            return this;
        }

        /// <summary>
        /// Records a buffer image barrier
        /// </summary>
        /// <returns>this</returns>
        public CommandBufferRecorder<T> BufferMemoryBarrier(Buffer buffer,
            VkAccessFlag srcAccess,
            uint srcQueue,
            VkAccessFlag dstAccess,
            uint dstQueue = Vulkan.QueueFamilyIgnored,
            VkPipelineStageFlag srcStage = VkPipelineStageFlag.AllCommands,
            VkPipelineStageFlag dstStage = VkPipelineStageFlag.AllCommands,
            VkDependencyFlag depFlag = VkDependencyFlag.None,
            ulong offset = 0, ulong size = 0)
        {
            unsafe
            {
                var temp = new VkBufferMemoryBarrier()
                {
                    SType = VkStructureType.BufferMemoryBarrier,
                    PNext = IntPtr.Zero,
                    SrcAccessMask = srcAccess,
                    DstAccessMask = dstAccess,
                    Buffer = buffer.Handle,
                    SrcQueueFamilyIndex = srcQueue,
                    DstQueueFamilyIndex = dstQueue,
                    Offset = offset,
                    Size = size == 0 ? buffer.Size - offset : size
                };
                return PipelineBarrier(srcStage, dstStage, depFlag, default(VkMemoryBarrier), temp,
                    default(VkImageMemoryBarrier));
            }
        }

        /// <summary>
        /// Records the an image memory barrier
        /// </summary>
        /// <param name="img">image</param>
        /// <param name="format">format</param>
        /// <param name="old">old layout</param>
        /// <param name="new">new layout</param>
        /// <param name="range">resource range</param>
        /// <param name="srcQueue">source queue</param>
        /// <param name="dstQueue">destination queue</param>
        /// <param name="flags">dependency flags</param>
        /// <returns>this</returns>
        public CommandBufferRecorder<T> ImageMemoryBarrier(VkImage img, VkFormat format, VkImageLayout old,
            VkImageLayout @new, VkImageSubresourceRange range, VkDependencyFlag flags = 0,
            uint srcQueue = Vulkan.QueueFamilyIgnored,
            uint dstQueue = Vulkan.QueueFamilyIgnored)
        {
            unsafe
            {
                var spec = new VkImageMemoryBarrier()
                {
                    SType = VkStructureType.ImageMemoryBarrier,
                    PNext = IntPtr.Zero,
                    OldLayout = old,
                    NewLayout = @new,
                    SrcQueueFamilyIndex = srcQueue,
                    DstQueueFamilyIndex = dstQueue,
                    Image = img,
                    SubresourceRange = range,
                    SrcAccessMask = VkAccessFlag.None,
                    DstAccessMask = VkAccessFlag.None
                };
                GetStageInfo(old, out VkPipelineStageFlag srcFlag, out spec.SrcAccessMask);
                GetStageInfo(@new, out VkPipelineStageFlag dstFlag, out spec.DstAccessMask);
                return PipelineBarrier(srcFlag, dstFlag, flags, default(VkMemoryBarrier),
                    default(VkBufferMemoryBarrier), spec);
            }
        }

        private static void GetStageInfo(VkImageLayout layout, out VkPipelineStageFlag pipelineStage,
            out VkAccessFlag accessMask)
        {
            accessMask = VkAccessFlag.None;
            pipelineStage = 0;
            switch (layout)
            {
                case VkImageLayout.ColorAttachmentOptimal:
                    pipelineStage = VkPipelineStageFlag.AllGraphics;
                    accessMask |= VkAccessFlag.ColorAttachmentRead | VkAccessFlag.ColorAttachmentWrite;
                    break;
                case VkImageLayout.DepthStencilAttachmentOptimal:
                case VkImageLayout.DepthReadOnlyStencilAttachmentOptimalKhr:
                    pipelineStage = VkPipelineStageFlag.AllGraphics;
                    accessMask |= VkAccessFlag.DepthStencilAttachmentRead | VkAccessFlag.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.DepthStencilReadOnlyOptimal:
                case VkImageLayout.DepthAttachmentStencilReadOnlyOptimalKhr:
                    pipelineStage = VkPipelineStageFlag.AllGraphics;
                    accessMask |= VkAccessFlag.DepthStencilAttachmentRead;
                    break;
                case VkImageLayout.ShaderReadOnlyOptimal:
                    pipelineStage = VkPipelineStageFlag.AllGraphics;
                    accessMask |= VkAccessFlag.ShaderRead;
                    break;
                case VkImageLayout.TransferSrcOptimal:
                    pipelineStage = VkPipelineStageFlag.Transfer;
                    accessMask |= VkAccessFlag.TransferRead;
                    break;
                case VkImageLayout.TransferDstOptimal:
                    pipelineStage = VkPipelineStageFlag.Transfer;
                    accessMask |= VkAccessFlag.TransferWrite;
                    break;
                case VkImageLayout.SharedPresentKhr:
                case VkImageLayout.PresentSrcKhr:
                case VkImageLayout.Preinitialized:
                case VkImageLayout.Undefined:
                case VkImageLayout.General:
                    pipelineStage = VkPipelineStageFlag.AllCommands;
                    accessMask |= VkAccessFlag.AllExceptExt;
                    break;
            }
        }

        public CommandBufferRecorder<T> EndRenderPass()
        {
            Handle.EndRenderPass();
            return this;
        }

        public T Commit()
        {
            _buffer.FinishBuild();
            return _buffer;
        }

        public CommandBufferRecorder<T> CopyBufferToImage(VkBuffer buffer, VkImage image, VkBufferImageCopy copyData)
        {
            unsafe
            {
                var local = copyData;
                VkCommandBuffer.vkCmdCopyBufferToImage(Handle, buffer, image, VkImageLayout.TransferDstOptimal, 1,
                    &local);
            }

            return this;
        }
    }
}