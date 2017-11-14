using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using VulkanLibrary.Managed.Buffers.Pool;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Buffers
{
    public class BufferTransfer : IDeviceOwned, IDisposable
    {
        /// <inheritdoc/>
        public Instance Instance => Device.Instance;

        /// <inheritdoc/>
        public PhysicalDevice PhysicalDevice => Device.PhysicalDevice;

        /// <inheritdoc/>
        public Device Device { get; }

        private struct PendingFlush
        {
            public BufferPools.MemoryHandle Handle;
            public Handles.Buffer Destination;
            public ulong DestinationOffset, Count;
        }

        private readonly MemoryType _transferType;
        private readonly Queue _transferQueue;
        private readonly CommandPoolCached _cmdBufferPool;
        private readonly ManualResetEventSlim _pendingFlushQueued = new ManualResetEventSlim(false);
        private readonly ConcurrentQueue<PendingFlush> _pendingFlush = new ConcurrentQueue<PendingFlush>();
        private const VkBufferUsageFlag _usage = VkBufferUsageFlag.TransferSrc;
        private const VkBufferCreateFlag _flags = 0;


        public BufferTransfer(Device dev, Queue transferQueue)
        {
            Device = dev;
            var reqs = new MemoryRequirements
            {
                HostVisible = MemoryRequirementLevel.Required,
                HostCoherent = MemoryRequirementLevel.Preferred
            };
            _transferType = reqs.FindMemoryType(dev.PhysicalDevice);
            _transferQueue = transferQueue;
            _cmdBufferPool = new CommandPoolCached(dev, transferQueue.FamilyIndex, 32, VkCommandPoolCreateFlag.Transient | VkCommandPoolCreateFlag.ResetCommandBuffer);
        }

        /// <summary>
        /// Waits for a flush queued event, then queues new buffer transfers.
        /// </summary>
        /// <returns>true if anything was queued.</returns>
        public bool FlushBlocking()
        {
            _pendingFlushQueued.Wait();
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
            try
            {
                foreach (var pending in toFlush)
                {
                    unsafe
                    {
                        var buffer = _cmdBufferPool.Borrow();
                        try
                        {
                            buffer.RecordCommands(VkCommandBufferUsageFlag.OneTimeSubmit)
                                .CopyBuffer(pending.Handle.BackingBuffer, pending.Destination, new VkBufferCopy()
                                {
                                    SrcOffset = pending.Handle.Offset,
                                    DstOffset = pending.DestinationOffset,
                                    Size = pending.Count
                                })
                                .PipelineBarrier(VkPipelineStageFlag.Transfer, VkPipelineStageFlag.AllCommands, 0,
                                    default(VkMemoryBarrier), new VkBufferMemoryBarrier()
                                    {
                                        SType = VkStructureType.BufferMemoryBarrier,
                                        PNext = (void*) 0,
                                        SrcAccessMask = VkAccessFlag.TransferWrite,
                                        DstAccessMask = VkAccessFlag.MemoryRead,
                                        Buffer = pending.Destination.Handle,
                                        SrcQueueFamilyIndex = Vulkan.QueueFamilyIgnored,
                                        DstQueueFamilyIndex = Vulkan.QueueFamilyIgnored,
                                        Offset = pending.DestinationOffset,
                                        Size = pending.Count
                                    })
                                .Commit();
                            _transferQueue.Submit(buffer);
                        }
                        finally
                        {
                            _cmdBufferPool.Return(buffer);
                        }
                    }
                }
            }
            finally
            {
                foreach (var k in toFlush)
                    k.Handle.Free();
            }
            return toFlush.Count > 0;
        }

        public unsafe void DeferredTransfer(Handles.Buffer dest, ulong destOffset, void* data, ulong count)
        {
            var handle = Device.BufferPools.Allocate(_transferType, _usage, _flags, count);
            System.Buffer.MemoryCopy(data, handle.MappedMemory.Handle.ToPointer(), handle.Size, count);
            if (!handle.BackingMemory.MemoryType.HostCoherent)
                handle.MappedMemory.FlushRange(0, count);
            _pendingFlush.Enqueue(new PendingFlush()
            {
                Count = count,
                Destination = dest,
                DestinationOffset = destOffset,
                Handle = handle
            });
            _pendingFlushQueued.Set();
        }

        public void Dispose()
        {
            unsafe
            {
                _cmdBufferPool.Dispose();
                while (_pendingFlush.TryDequeue(out var k))
                    k.Handle.Free();
            }
        }
    }
}