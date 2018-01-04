// #define DEFERRED_ERROR_TRACING

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;
using VulkanLibrary.Managed.Buffers;
using VulkanLibrary.Managed.Buffers.Pool;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;
using Buffer = System.Buffer;

namespace VulkanLibrary.Managed.Utilities
{
    public class DeferredTransfer : IDeviceOwned, IDisposable
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        /// <inheritdoc/>
        public Instance Instance => Device.Instance;

        /// <inheritdoc/>
        public PhysicalDevice PhysicalDevice => Device.PhysicalDevice;

        /// <inheritdoc/>
        public Device Device { get; }

        private abstract class PendingFlush
        {
            private BufferPools.MemoryHandle _handle;

            public BufferPools.MemoryHandle Handle => _handle;
            public readonly uint OwnerQueueFamily;
            public readonly Action Callback;

            protected PendingFlush(BufferPools.MemoryHandle src, uint queue, Action callback)
            {
                _handle = src;
                OwnerQueueFamily = queue;
                Callback = callback;
            }

            public virtual void Finished()
            {
                Callback?.Invoke();
                _handle.Free();
            }

            #if DEFERRED_ERROR_TRACING
            public readonly string Allocated = Environment.StackTrace;
            #endif
        }

        private class PendingFlushBuffer : PendingFlush
        {
            public readonly IPinnableBindableBuffer Destination;
            public readonly ulong DestinationOffset, Count;

            public PendingFlushBuffer(BufferPools.MemoryHandle src, uint queue, Action callback, IPinnableBindableBuffer dst,
                ulong dstOffset, ulong dstCount) : base(src, queue, callback)
            {
                Destination = dst;
                DestinationOffset = dstOffset;
                Count = dstCount;
                Destination.IncreasePins();
            }

            public override void Finished()
            {
                base.Finished();
                Destination.DecreasePins();
            }
        }

        private class PendingFlushImage : PendingFlush
        {
            public readonly Image Destination;
            public readonly VkBufferImageCopy CopyData;

            public PendingFlushImage(BufferPools.MemoryHandle src, uint queue, Action callback, Image dst,
                VkBufferImageCopy copyDesc) : base(src, queue, callback)
            {
                Destination = dst;
                CopyData = copyDesc;
                Destination.IncreasePins();
            }

            public override void Finished()
            {
                base.Finished();
                Destination.DecreasePins();
            }
        }

        private readonly MemoryType _transferType;
        private readonly Queue _transferQueue;
        private readonly CommandPoolCached _cmdBufferPool;
        private readonly AutoResetEvent _pendingFlushQueued = new AutoResetEvent(false);
        private readonly ConcurrentQueue<PendingFlush> _pendingFlush = new ConcurrentQueue<PendingFlush>();
        private const VkBufferUsageFlag _usage = VkBufferUsageFlag.TransferSrc;
        private const VkBufferCreateFlag _flags = 0;


        public DeferredTransfer(Device dev, Queue transferQueue)
        {
            Device = dev;
            var reqs = new MemoryRequirements
            {
                HostVisible = MemoryRequirementLevel.Required,
                HostCoherent = MemoryRequirementLevel.Preferred
            };
            _transferType = reqs.FindMemoryType(dev.PhysicalDevice);
            _transferQueue = transferQueue;
            _cmdBufferPool = new CommandPoolCached(dev, transferQueue.FamilyIndex, 32,
                VkCommandPoolCreateFlag.Transient | VkCommandPoolCreateFlag.ResetCommandBuffer);
        }

        /// <summary>
        /// Waits for a flush queued event, then queues new buffer transfers.
        /// </summary>
        /// <returns>true if anything was queued.</returns>
        public bool FlushBlocking()
        {
            _pendingFlushQueued.WaitOne();
            return Flush();
        }

        /// <summary>
        /// Puts all pending transfers in the transfer queue
        /// </summary>
        /// <returns>true if anything was queued</returns>
        public bool Flush()
        {
            List<PendingFlush> toFlush = new List<PendingFlush>(_pendingFlush.Count);
            {
                while (_pendingFlush.TryDequeue(out var pending))
                    toFlush.Add(pending);
            }
            if (toFlush.Count == 0)
                return false;
            foreach (var temp in toFlush)
            {
                var buffer = _cmdBufferPool.Borrow();
                try
                {
                    buffer.SubmissionFinished += temp.Finished;
                    switch (temp)
                    {
                        case PendingFlushBuffer pendingBuffer:
                        {
                            _log.Trace(
                                $"Transferring {pendingBuffer.Count} bytes to {pendingBuffer.Destination}");
                            var recorder = buffer.RecordCommands(VkCommandBufferUsageFlag.OneTimeSubmit);
                            recorder.BufferMemoryBarrier(pendingBuffer.Destination.BindingHandle,
                                VkAccessFlag.AllExceptExt,
                                pendingBuffer.OwnerQueueFamily,
                                VkAccessFlag.AllExceptExt, _transferQueue.FamilyIndex,
                                VkPipelineStageFlag.AllCommands, VkPipelineStageFlag.AllCommands,
                                VkDependencyFlag.None,
                                pendingBuffer.Destination.Offset + pendingBuffer.DestinationOffset,
                                pendingBuffer.Count);
                            recorder.CopyBuffer(pendingBuffer.Handle.BackingBuffer,
                                pendingBuffer.Destination.BindingHandle,
                                new VkBufferCopy
                                {
                                    SrcOffset = pendingBuffer.Handle.Offset,
                                    DstOffset = pendingBuffer.Destination.Offset +
                                                pendingBuffer.DestinationOffset,
                                    Size = pendingBuffer.Count
                                });
                            recorder.BufferMemoryBarrier(pendingBuffer.Destination.BindingHandle,
                                VkAccessFlag.AllExceptExt,
                                _transferQueue.FamilyIndex,
                                VkAccessFlag.AllExceptExt,
                                pendingBuffer.OwnerQueueFamily,
                                VkPipelineStageFlag.AllCommands, VkPipelineStageFlag.AllCommands,
                                VkDependencyFlag.None,
                                pendingBuffer.Destination.Offset + pendingBuffer.DestinationOffset,
                                pendingBuffer.Count);
                            recorder.Commit();
                            break;
                        }
                        case PendingFlushImage pendingImage:
                        {
                            _log.Trace(
                                $"Transferring {pendingImage.CopyData.BufferRowLength * pendingImage.CopyData.ImageExtent.Height} pixels to {pendingImage.Destination.Handle}");
                            var range = new VkImageSubresourceRange
                            {
                                AspectMask = pendingImage.CopyData.ImageSubresource.AspectMask,
                                BaseMipLevel = pendingImage.CopyData.ImageSubresource.MipLevel,
                                LevelCount = 1,
                                BaseArrayLayer = pendingImage.CopyData.ImageSubresource.BaseArrayLayer,
                                LayerCount = pendingImage.CopyData.ImageSubresource.LayerCount
                            };
                            var recorder = buffer.RecordCommands(VkCommandBufferUsageFlag.OneTimeSubmit);
                            recorder.ImageMemoryBarrier(pendingImage.Destination.Handle,
                                pendingImage.Destination.Format, VkImageLayout.Undefined,
                                VkImageLayout.TransferDstOptimal,
                                range, VkDependencyFlag.None,
                                pendingImage.OwnerQueueFamily, _transferQueue.FamilyIndex);
                            recorder.CopyBufferToImage(pendingImage.Handle.BackingBuffer.Handle,
                                pendingImage.Destination.Handle,
                                new VkBufferImageCopy
                                {
                                    BufferOffset =
                                        pendingImage.Handle.Offset + pendingImage.CopyData.BufferOffset,
                                    BufferRowLength = pendingImage.CopyData.BufferRowLength,
                                    BufferImageHeight = pendingImage.CopyData.BufferImageHeight,
                                    ImageSubresource = pendingImage.CopyData.ImageSubresource,
                                    ImageOffset = pendingImage.CopyData.ImageOffset,
                                    ImageExtent = pendingImage.CopyData.ImageExtent
                                });
                            recorder.ImageMemoryBarrier(pendingImage.Destination.Handle,
                                pendingImage.Destination.Format, VkImageLayout.Undefined,
                                VkImageLayout.TransferDstOptimal,
                                range, VkDependencyFlag.None,
                                _transferQueue.FamilyIndex, pendingImage.OwnerQueueFamily);
                            recorder.Commit();
                            break;
                        }
                        default:
                            throw new InvalidOperationException(
                                $"Can't handle pending data {temp.GetType().Name}");
                    }

                    _transferQueue.Submit(buffer);
                }
                finally
                {
                    _cmdBufferPool.Return(buffer);
                }
            }

            return toFlush.Count > 0;
        }

        public unsafe void Transfer(IPinnableBindableBuffer dest, ulong destOffset, void* data, ulong count,
            uint destQueueFamily, Action callback = null)
        {
            var handle = Device.BufferPools.Allocate(_transferType, _usage, _flags, count);
            Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlushBuffer(handle, destQueueFamily, callback, dest, destOffset, count));
            _pendingFlushQueued.Set();
        }

        public unsafe void Transfer(Image dest, void* data, ulong count, VkBufferImageCopy copyInfo,
            uint destQueueFamily, Action callback = null)
        {
            var handle = Device.BufferPools.Allocate(_transferType, _usage, _flags, count);
            Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlushImage(handle, destQueueFamily, callback, dest, copyInfo));
            _pendingFlushQueued.Set();
        }

        public void Dispose()
        {
            _cmdBufferPool.Dispose();
            while (_pendingFlush.TryDequeue(out var k))
                k.Handle.Free();
        }
    }
}