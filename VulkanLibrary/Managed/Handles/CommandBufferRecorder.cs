using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class CommandBufferRecorder
    {
        private readonly CommandBuffer _buffer;

        internal CommandBufferRecorder(CommandBuffer buffer)
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

        public CommandBufferRecorder BeginRenderPass(RenderPass pass, Framebuffer framebuffer,
            VkClearValue[] clearValues,
            VkSubpassContents subpass,
            VkRect2D? renderArea = null)
        {
            framebuffer.AssertValid();
            pass.AssertValid();
            unsafe
            {
                fixed(VkClearValue* clearPtr = clearValues)
                {
                    var info = new VkRenderPassBeginInfo()
                    {
                        SType = VkStructureType.RenderPassBeginInfo,
                        PNext = (void*) 0,
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

        public CommandBufferRecorder BindPipeline(Pipeline pipeline)
        {
            pipeline.AssertValid();
            Handle.BindPipeline(pipeline.PipelineType, pipeline.Handle);
            return this;
        }

        /// <summary>
        /// Binds a vertex buffer to this command buffer
        /// </summary>
        /// <param name="binding">binding</param>
        /// <param name="buffer">buffer to bind</param>
        /// <param name="offset">offset into buffer</param>
        /// <returns>this</returns>
        public CommandBufferRecorder BindVertexBuffer(uint binding, Buffer buffer, ulong offset = 0)
        {
            buffer.AssertValid();
            var bh = buffer.Handle;
            unsafe
            {
                VkCommandBuffer.vkCmdBindVertexBuffers(Handle, binding, 1, &bh, &offset);
            }
            return this;
        }

        /// <summary>
        /// To bind vertex buffers to a command buffer for use in subsequent draw commands, call:
        /// </summary>
        /// <param name="firstBinding">is the index of the first vertex input binding whose state is updated by the command.</param>
        /// <param name="pBuffers">is a pointer to an array of buffer handles.</param>
        /// <param name="pOffsets">is a pointer to an array of buffer offsets.</param>
        /// <returns>this</returns>
        public  CommandBufferRecorder BindVertexBuffers(uint firstBinding, Buffer[] pBuffers, ulong[] pOffsets = null)
        {
            Handle.BindVertexBuffers(firstBinding, pBuffers.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray(), pOffsets ?? new ulong[pBuffers.Length]);
            return this;
        }

        /// <summary>
        /// To record a non-indexed draw, call:
        /// </summary>
        /// <param name="vertexCount">is the number of vertices to draw.</param>
        /// <param name="instanceCount">is the number of instances to draw.</param>
        /// <param name="firstVertex">is the index of the first vertex to draw.</param>
        /// <param name="firstInstance">is the instance ID of the first instance to draw.</param>
        public CommandBufferRecorder Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
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
        public CommandBufferRecorder DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset,
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
        public CommandBufferRecorder DrawIndirect(VkBuffer buffer, ulong offset, uint drawCount, uint stride)
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
        public CommandBufferRecorder DrawIndexedIndirect(VkBuffer buffer, ulong offset, uint drawCount, uint stride)
        {
            Handle.DrawIndexedIndirect(buffer, offset, drawCount, stride);
            return this;
        }

        public CommandBufferRecorder EndRenderPass()
        {
            Handle.EndRenderPass();
            return this;
        }

        public CommandBuffer Commit()
        {
            _buffer.FinishBuild();
            return _buffer;
        }
    }
}