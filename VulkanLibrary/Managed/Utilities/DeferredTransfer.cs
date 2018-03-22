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
using VulkanLibrary.Unmanaged.Handles;
using Buffer = System.Buffer;
using Semaphore = System.Threading.Semaphore;

namespace VulkanLibrary.Managed.Utilities
{
    public class DeferredTransfer : IDeviceOwned, IDisposable, INameableResource
    {
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
            public readonly TransferArguments Arguments;

            protected PendingFlush(BufferPools.MemoryHandle src, TransferArguments arguments)
            {
                _handle = src;
                Arguments = arguments;
                Arguments.SetEvent?.IncreasePins();
            }

            public virtual void Finished()
            {
                _handle.Free();
                Arguments.SetEvent?.DecreasePins();
                Arguments._callback?.Invoke();
            }

#if DEFERRED_ERROR_TRACING
            public readonly string Allocated = Environment.StackTrace;
            #endif
        }

        private class PendingFlushBuffer : PendingFlush, IBindableBuffer
        {
            public readonly IPinnableBindableBuffer Destination;
            public readonly ulong Offset;
            public readonly ulong Size;

            public PendingFlushBuffer(BufferPools.MemoryHandle src, TransferArguments arguments,
                IPinnableBindableBuffer dst, ulong dstOffset, ulong dstCount) : base(src, arguments)
            {
                Destination = dst;
                Offset = dstOffset;
                Size = dstCount;
                Destination.IncreasePins();
            }

            public override void Finished()
            {
                base.Finished();
                Destination.DecreasePins();
            }

            Handles.Buffer IBindableBuffer.BindingHandle => Destination.BindingHandle;
            ulong IBindableBuffer.Offset => Destination.Offset + Offset;
            ulong IBindableBuffer.Size => Size;
        }

        private class PendingFlushImage : PendingFlush
        {
            public readonly Image Destination;
            public readonly VkBufferImageCopy CopyData;

            public PendingFlushImage(BufferPools.MemoryHandle src, TransferArguments arguments, Image dst,
                VkBufferImageCopy copyDesc) : base(src, arguments)
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
        private const VkBufferUsageFlag Usage = VkBufferUsageFlag.TransferSrc;
        private const VkBufferCreateFlag Flags = 0;

        public int PendingTransfers => _pendingFlush.Count;

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

        private int _activeFlushers;

        /// <summary>
        /// Waits until all tasks have been submitted to the queue.
        /// </summary>
        public void WaitUntilFlushed()
        {
            while (_pendingFlush.Count > 0 || _activeFlushers > 0)
                ;
        }

        /// <summary>
        /// Puts all pending transfers in the transfer queue
        /// </summary>
        /// <returns>true if anything was queued</returns>
        public bool Flush()
        {
            var flushed = 0;
            Interlocked.Increment(ref _activeFlushers);

            CommandBufferPooledExclusiveUse buffer = null;
            try
            {
                CommandBufferRecorder recorder = default;

                while (_pendingFlush.TryDequeue(out var temp))
                {
                    if (buffer == null)
                    {
                        buffer = _cmdBufferPool.Borrow();
                        recorder = buffer.RecordCommands(VkCommandBufferUsageFlag.OneTimeSubmit);
                    }

                    flushed++;
                    buffer.SubmissionFinished += temp.Finished;
                    switch (temp)
                    {
                        case PendingFlushBuffer pendingBuffer:
                        {
                            _log.Trace(
                                $"Transferring {pendingBuffer.Size} bytes to {pendingBuffer.Destination}\t({_pendingFlush.Count} remain)");
                            var target = (IBindableBuffer) pendingBuffer;

                            recorder.BufferMemoryBarrier(target.BindingHandle,
                                VkAccessFlag.AllExceptExt, pendingBuffer.Arguments.SrcQueueFamily,
                                VkAccessFlag.AllExceptExt, _transferQueue.FamilyIndex, VkPipelineStageFlag.AllCommands,
                                VkPipelineStageFlag.AllCommands, VkDependencyFlag.None,
                                target.Offset, target.Size);

                            recorder.CopyBuffer(pendingBuffer.Handle.BackingBuffer, target.BindingHandle,
                                new VkBufferCopy
                                {
                                    SrcOffset = pendingBuffer.Handle.Offset,
                                    DstOffset = target.Offset,
                                    Size = target.Size
                                });
                            if (temp.Arguments.DstQueueFamily != Vulkan.QueueFamilyIgnored)
                                recorder.BufferMemoryBarrier(pendingBuffer.Destination.BindingHandle,
                                    VkAccessFlag.AllExceptExt, _transferQueue.FamilyIndex, VkAccessFlag.AllExceptExt,
                                    temp.Arguments.DstQueueFamily,
                                    VkPipelineStageFlag.AllCommands, VkPipelineStageFlag.AllCommands,
                                    VkDependencyFlag.None, target.Offset, target.Size);

                            if (temp.Arguments.SetEvent != null)
                                recorder.Handle.SetEvent(temp.Arguments.SetEvent.Handle,
                                    VkPipelineStageFlag.AllCommands);
                            break;
                        }
                        case PendingFlushImage pendingImage:
                        {
                            _log.Trace(
                                $"Transferring {pendingImage.CopyData.BufferRowLength * pendingImage.CopyData.ImageExtent.Height} pixels to {pendingImage.Destination}\t({_pendingFlush.Count} remain)");
                            var range = new VkImageSubresourceRange
                            {
                                AspectMask = pendingImage.CopyData.ImageSubresource.AspectMask,
                                BaseMipLevel = pendingImage.CopyData.ImageSubresource.MipLevel,
                                LevelCount = 1,
                                BaseArrayLayer = pendingImage.CopyData.ImageSubresource.BaseArrayLayer,
                                LayerCount = pendingImage.CopyData.ImageSubresource.LayerCount
                            };
                            recorder.ImageMemoryBarrier(pendingImage.Destination.Handle,
                                pendingImage.Destination.Format, VkImageLayout.Undefined,
                                VkImageLayout.TransferDstOptimal, range, VkDependencyFlag.None,
                                pendingImage.Arguments.SrcQueueFamily, _transferQueue.FamilyIndex);
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
                            if (temp.Arguments.DstQueueFamily != Vulkan.QueueFamilyIgnored)
                                recorder.ImageMemoryBarrier(pendingImage.Destination.Handle,
                                    pendingImage.Destination.Format, VkImageLayout.Undefined,
                                    VkImageLayout.TransferDstOptimal, range, VkDependencyFlag.None,
                                    _transferQueue.FamilyIndex, temp.Arguments.DstQueueFamily);

                            if (temp.Arguments.SetEvent != null)
                                recorder.Handle.SetEvent(temp.Arguments.SetEvent.Handle,
                                    VkPipelineStageFlag.AllCommands);
                            break;
                        }
                        default:
                            throw new InvalidOperationException(
                                $"Can't handle pending data {temp.GetType().Name}");
                    }
                }

                if (buffer != null)
                {
                    recorder.Commit();
                    _transferQueue.Submit(buffer);
                }
            }
            finally
            {
                if (buffer != null)
                    _cmdBufferPool.Return(buffer);
                Interlocked.Decrement(ref _activeFlushers);
            }

            return flushed > 0;
        }

        public unsafe void Transfer(IPinnableBindableBuffer dest, ulong destOffset, void* data, ulong count,
            TransferArguments? arguments = null)
        {
            CheckArgs(arguments);
            var handle = Device.BufferPools.Allocate(_transferType, Usage, Flags, count);
            Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlushBuffer(handle, arguments ?? TransferArguments.Null, dest, destOffset,
                count));
            _pendingFlushQueued.Set();
        }

        private void CheckArgs(TransferArguments? args)
        {
            if (!args.HasValue)
                return;
            var a = args.Value;
            if (a.SetEvent != null)
                Debug.Assert(Event.SupportedBy(_transferQueue), "Transfer queue doesn't support events");
            if (a.SrcQueueFamily != Vulkan.QueueFamilyIgnored)
                Debug.Assert(a.SrcQueueFamily != _transferQueue.FamilyIndex,
                    "Source queue family is identical to transfer queue.  Doesn't make sense");
            if (a.DstQueueFamily != Vulkan.QueueFamilyIgnored)
                Debug.Assert(a.DstQueueFamily != _transferQueue.FamilyIndex,
                    "Destination queue family is identical to transfer queue.  Doesn't make sense");
        }

        public unsafe void Transfer(Image dest, void* data, ulong count, VkBufferImageCopy copyInfo,
            TransferArguments? arguments = null)
        {
            CheckArgs(arguments);
            var handle = Device.BufferPools.Allocate(_transferType, Usage, Flags, count);
            Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlushImage(handle, arguments ?? TransferArguments.Null, dest, copyInfo));
            _pendingFlushQueued.Set();
        }

        public readonly struct TransferArguments
        {
            internal readonly Action _callback;
            internal readonly Event SetEvent;
            internal readonly uint DstQueueFamily;
            internal readonly uint SrcQueueFamily;

            public TransferArguments(Action a, Event e, uint dstFamily, uint srcFamily)
            {
                _callback = a;
                SetEvent = e;
                DstQueueFamily = dstFamily;
                SrcQueueFamily = srcFamily;
            }

            public TransferArguments WithCallback(Action a)
            {
                return new TransferArguments(_callback != null ? (_callback + a) : a, SetEvent, DstQueueFamily,
                    SrcQueueFamily);
            }

            public TransferArguments WithEvent(Event e)
            {
                if (SetEvent != null)
                    throw new InvalidOperationException("Can't set multiple events");
                return new TransferArguments(_callback, e, DstQueueFamily, SrcQueueFamily);
            }

            public TransferArguments WithDestinationQueue(uint index)
            {
                return new TransferArguments(_callback, SetEvent, index, SrcQueueFamily);
            }

            public TransferArguments WithSourceQueue(uint index)
            {
                return new TransferArguments(_callback, SetEvent, DstQueueFamily, index);
            }

            public static TransferArguments Callback(Action a)
            {
                return new TransferArguments(a, null, Vulkan.QueueFamilyIgnored, Vulkan.QueueFamilyIgnored);
            }

            public static TransferArguments Event(Event e)
            {
                return new TransferArguments(null, e, Vulkan.QueueFamilyIgnored, Vulkan.QueueFamilyIgnored);
            }

            public static TransferArguments DestinationQueue(uint index)
            {
                return new TransferArguments(null, null, index, Vulkan.QueueFamilyIgnored);
            }

            public static TransferArguments SourceQueue(uint index)
            {
                return new TransferArguments(null, null, Vulkan.QueueFamilyIgnored, index);
            }

            public static readonly TransferArguments Null = DestinationQueue(Vulkan.QueueFamilyIgnored);
        }

        public void Dispose()
        {
            _cmdBufferPool.Dispose();
            while (_pendingFlush.TryDequeue(out var k))
                k.Handle.Free();
        }

        private static readonly ILogger _logDefault = LogManager.GetCurrentClassLogger();
        private ILogger _log = _logDefault;
        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                if (string.IsNullOrEmpty(_name))
                    _log = _logDefault;
                else
                    _log = LogManager.GetLogger(GetType().FullName + "[Name=" + _name + "]");
            }
        }
    }
}