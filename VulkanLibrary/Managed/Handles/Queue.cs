using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Queue
    {
        /// <summary>
        /// The index of this queue's family.
        /// </summary>
        public uint FamilyIndex { get; }

        /// <summary>
        /// The properties of this queue's family.
        /// </summary>
        public VkQueueFamilyProperties FamilyProperties { get; }

        internal Queue(Device device, uint family, uint index)
        {
            Device = device;
            Handle = device.Handle.GetDeviceQueue(family, index);
            FamilyIndex = family;
            FamilyProperties = Device.PhysicalDevice.Handle.GetQueueFamilyProperties()[(int) family];
        }

        public void Submit(CommandBuffer buffer, Semaphore wait, VkPipelineStageFlag waitStage, Semaphore signal,
            Fence submit)
        {
            wait?.AssertValid();
            signal?.AssertValid();
            submit?.AssertValid();
            Submit(buffer, wait?.Handle, waitStage, signal?.Handle, submit?.Handle);
        }

        public void Submit(CommandBuffer buffer, VkSemaphore? wait = null, VkPipelineStageFlag waitStage = 0,
            VkSemaphore? signal = null,
            VkFence? submit = null)
        {
            buffer.AssertBuilt();
            var buff = buffer.Handle;
            unsafe
            {
                var waitH = wait ?? VkSemaphore.Null;
                var signalH = signal ?? VkSemaphore.Null;
                var submitH = submit ?? VkFence.Null;
                var info = new VkSubmitInfo()
                {
                    SType = VkStructureType.SubmitInfo,
                    PNext = IntPtr.Zero,
                    CommandBufferCount = 1,
                    PCommandBuffers = &buff,
                    SignalSemaphoreCount = signal.HasValue ? 1u : 0u,
                    PSignalSemaphores = &signalH,
                    WaitSemaphoreCount = wait.HasValue ? 1u : 0u,
                    PWaitSemaphores = &waitH,
                    PWaitDstStageMask = &waitStage,
                };
                if (buffer is CommandBufferPooledExclusiveUse peu)
                {
                    Debug.Assert(submitH == VkFence.Null, "Can't use a submit fence on a pooled handle");
                    peu.DoSubmit(Handle, info);
                }
                else
                    VkException.Check(VkQueue.vkQueueSubmit(Handle, 1, &info, submitH));
            }
        }

        public void Submit(CommandBuffer[] buffers, IEnumerable<Semaphore> wait, VkPipelineStageFlag[] waitStages,
            IEnumerable<Semaphore> signal, Fence submit)
        {
            Submit(buffers, wait?.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray(), waitStages, signal?.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray(), submit?.Handle ?? VkFence.Null);
        }

        public void Submit(CommandBuffer[] buffers, VkSemaphore[] wait, VkPipelineStageFlag[] waitStages,
            VkSemaphore[] signal, VkFence submit)
        {
            unsafe
            {
                // ReSharper disable once PossibleNullReferenceException
                Debug.Assert((waitStages == null && wait == null) || waitStages.Length == wait.Length);
                var arrayBuffers = buffers.Where(x => !(x is CommandBufferPooledExclusiveUse)).Select(x =>
                {
                    x.AssertBuilt();
                    return x.Handle;
                }).ToArray();
                var pooledBuffers = buffers.OfType<CommandBufferPooledExclusiveUse>().ToArray();
                Debug.Assert(pooledBuffers.Length == 0 || submit == VkFence.Null,
                    "Can't use custom submit fence on pooled buffers");
                fixed (VkSemaphore* waitPtr = wait)
                fixed (VkPipelineStageFlag* waitStagePtr = waitStages)
                fixed (VkSemaphore* signalPtr = signal)
                {
                    if (arrayBuffers.Length > 0)
                        fixed (VkCommandBuffer* buffer = arrayBuffers)
                        {
                            var info = new VkSubmitInfo()
                            {
                                SType = VkStructureType.SubmitInfo,
                                PNext = IntPtr.Zero,
                                CommandBufferCount = (uint) arrayBuffers.Length,
                                PCommandBuffers = buffer,
                                SignalSemaphoreCount = (uint) (signal?.Length ?? 0),
                                PSignalSemaphores = signalPtr,
                                WaitSemaphoreCount = (uint) (wait?.Length ?? 0),
                                PWaitSemaphores = waitPtr,
                                PWaitDstStageMask = waitStagePtr,
                            };
                            VkException.Check(VkQueue.vkQueueSubmit(Handle, 1, &info, submit));
                        }
                    foreach (var pooled in pooledBuffers)
                    {
                        var handle = pooled.Handle;
                        var info = new VkSubmitInfo()
                        {
                            SType = VkStructureType.SubmitInfo,
                            PNext = IntPtr.Zero,
                            CommandBufferCount = 1,
                            PCommandBuffers = &handle,
                            SignalSemaphoreCount = (uint) (signal?.Length ?? 0),
                            PSignalSemaphores = signalPtr,
                            WaitSemaphoreCount = (uint) (wait?.Length ?? 0),
                            PWaitSemaphores = waitPtr,
                            PWaitDstStageMask = waitStagePtr,
                        };
                        pooled.DoSubmit(Handle, info);
                    }
                }
            }
        }

        public void PresentKHR(SwapchainKHR swapchain, uint imageIndex, Semaphore[] semaphores)
        {
            PresentKHR(swapchain, imageIndex, semaphores?.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray());
        }

        public void PresentKHR(SwapchainKHR swapchain, uint imageIndex, VkSemaphore[] waitSemaphores)
        {
            AssertValid();
            unsafe
            {
                swapchain.AssertValid();
                var swapHandle = swapchain.Handle;
                fixed (VkSemaphore* waitPtr = waitSemaphores)
                {
                    var result = VkResult.ErrorDeviceLost;
                    var info = new VkPresentInfoKHR()
                    {
                        SType = VkStructureType.PresentInfoKhr,
                        PNext = IntPtr.Zero,
                        SwapchainCount = 1,
                        PSwapchains = &swapHandle,
                        PImageIndices = &imageIndex,
                        WaitSemaphoreCount = (uint) (waitSemaphores?.Length ?? 0),
                        PWaitSemaphores = waitPtr,
                        PResults = &result
                    };
                    Handle.QueuePresentKHR(&info);
                    VkException.Check(result);
                }
            }
        }
    }
}