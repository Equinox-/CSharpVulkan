using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            public BufferPools.MemoryHandle Handle;
            public uint OwnerQueueFamily;
            public Action Callback;

            protected PendingFlush()
            {
                Finished = () =>
                {
                    Callback?.Invoke();
                    Handle.Free();
                };
            }

            public readonly Action Finished;
        }

        private class PendingFlushBuffer : PendingFlush
        {
            public IBindableBuffer Destination;
            public ulong DestinationOffset, Count;
        }

        private class PendingFlushImage : PendingFlush
        {
            public Image Destination;
            public VkBufferImageCopy CopyData;
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
                            _log.Trace(
                                $"Transferring {pendingBuffer.Count} bytes to {pendingBuffer.Destination}");
                            buffer.RecordCommands(VkCommandBufferUsageFlag.OneTimeSubmit)
                                .BufferMemoryBarrier(pendingBuffer.Destination.BindingHandle,
                                    VkAccessFlag.AllExceptExt,
                                    pendingBuffer.OwnerQueueFamily,
                                    VkAccessFlag.AllExceptExt, _transferQueue.FamilyIndex,
                                    VkPipelineStageFlag.AllCommands, VkPipelineStageFlag.AllCommands,
                                    VkDependencyFlag.None,
                                    pendingBuffer.Destination.Offset + pendingBuffer.DestinationOffset,
                                    pendingBuffer.Count)
                                .CopyBuffer(pendingBuffer.Handle.BackingBuffer,
                                    pendingBuffer.Destination.BindingHandle,
                                    new VkBufferCopy
                                    {
                                        SrcOffset = pendingBuffer.Handle.Offset,
                                        DstOffset = pendingBuffer.Destination.Offset +
                                                    pendingBuffer.DestinationOffset,
                                        Size = pendingBuffer.Count
                                    })
                                .BufferMemoryBarrier(pendingBuffer.Destination.BindingHandle,
                                    VkAccessFlag.AllExceptExt,
                                    _transferQueue.FamilyIndex,
                                    VkAccessFlag.AllExceptExt,
                                    pendingBuffer.OwnerQueueFamily,
                                    VkPipelineStageFlag.AllCommands, VkPipelineStageFlag.AllCommands,
                                    VkDependencyFlag.None,
                                    pendingBuffer.Destination.Offset + pendingBuffer.DestinationOffset,
                                    pendingBuffer.Count)
                                .Commit();
                            break;
                        case PendingFlushImage pendingImage:
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
                            buffer.RecordCommands(VkCommandBufferUsageFlag.OneTimeSubmit)
                                .ImageMemoryBarrier(pendingImage.Destination.Handle,
                                    pendingImage.Destination.Format, VkImageLayout.Undefined,
                                    VkImageLayout.TransferDstOptimal,
                                    range, VkDependencyFlag.None,
                                    pendingImage.OwnerQueueFamily, _transferQueue.FamilyIndex)
                                .CopyBufferToImage(pendingImage.Handle.BackingBuffer.Handle,
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
                                    })
                                .ImageMemoryBarrier(pendingImage.Destination.Handle,
                                    pendingImage.Destination.Format, VkImageLayout.Undefined,
                                    VkImageLayout.TransferDstOptimal,
                                    range, VkDependencyFlag.None,
                                    _transferQueue.FamilyIndex, pendingImage.OwnerQueueFamily)
                                .Commit();
                            break;
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

        public unsafe void Transfer(IBindableBuffer dest, ulong destOffset, void* data, ulong count,
            uint destQueueFamily, Action callback = null)
        {
            var handle = Device.BufferPools.Allocate(_transferType, _usage, _flags, count);
            Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlushBuffer
            {
                Count = count,
                Destination = dest,
                DestinationOffset = destOffset,
                Handle = handle,
                OwnerQueueFamily = destQueueFamily,
                Callback = callback
            });
            _pendingFlushQueued.Set();
        }

        public unsafe void Transfer(Image dest, void* data, ulong count, VkBufferImageCopy copyInfo,
            uint destQueueFamily, Action callback = null)
        {
            var handle = Device.BufferPools.Allocate(_transferType, _usage, _flags, count);
            Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlushImage
            {
                Destination = dest,
                Handle = handle,
                CopyData = copyInfo,
                OwnerQueueFamily = destQueueFamily,
                Callback = callback
            });
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