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

        public void Submit(CommandBuffer buffer, VkSemaphore? wait, VkPipelineStageFlag waitStage, VkSemaphore? signal,
            VkFence? submit)
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
                    PNext = (void*) 0,
                    CommandBufferCount = 1,
                    PCommandBuffers = &buff,
                    SignalSemaphoreCount = signal.HasValue ? 1u : 0u,
                    PSignalSemaphores = &signalH,
                    WaitSemaphoreCount = wait.HasValue ? 1u : 0u,
                    PWaitSemaphores = &waitH,
                    PWaitDstStageMask = &waitStage,
                };
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

                var pinWait = wait != null && wait.Length > 0
                    ? GCHandle.Alloc(wait, GCHandleType.Pinned)
                    : default(GCHandle);
                var pinWaitStage =
                    wait != null && wait.Length > 0
                        ? GCHandle.Alloc(waitStages, GCHandleType.Pinned)
                        : default(GCHandle);
                var pinSignal = signal != null && signal.Length > 0 ? GCHandle.Alloc(signal) : default(GCHandle);

                var arrayBuffers = buffers.Select(x =>
                {
                    x.AssertBuilt();
                    return x.Handle;
                }).ToArray();
                try
                {
                    fixed (VkCommandBuffer* buffer = &arrayBuffers[0])
                    {
                        var info = new VkSubmitInfo()
                        {
                            SType = VkStructureType.SubmitInfo,
                            PNext = (void*) 0,
                            CommandBufferCount = (uint) arrayBuffers.Length,
                            PCommandBuffers = buffer,
                            SignalSemaphoreCount = (uint) (signal?.Length ?? 0),
                            PSignalSemaphores =
                                (VkSemaphore*) (signal != null && signal.Length > 0
                                    ? Marshal.UnsafeAddrOfPinnedArrayElement(signal, 0)
                                    : IntPtr.Zero).ToPointer(),
                            WaitSemaphoreCount = (uint) (wait?.Length ?? 0),
                            PWaitSemaphores =
                                (VkSemaphore*) (wait != null && wait.Length > 0
                                    ? Marshal.UnsafeAddrOfPinnedArrayElement(wait, 0)
                                    : IntPtr.Zero).ToPointer(),
                            PWaitDstStageMask =
                                (VkPipelineStageFlag*) (wait != null && wait.Length > 0
                                    ? Marshal.UnsafeAddrOfPinnedArrayElement(waitStages, 0)
                                    : IntPtr.Zero).ToPointer(),
                        };
                        VkException.Check(VkQueue.vkQueueSubmit(Handle, 1, &info, submit));
                    }
                }
                finally
                {
                    if (wait != null && wait.Length > 0)
                    {
                        pinWait.Free();
                        pinWaitStage.Free();
                    }
                    if (signal != null && signal.Length > 0)
                        pinSignal.Free();
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
                var pinWait = waitSemaphores != null && waitSemaphores.Length > 0
                    ? GCHandle.Alloc(waitSemaphores)
                    : default(GCHandle);
                try
                {
                    var result = VkResult.ErrorDeviceLost;
                    var info = new VkPresentInfoKHR()
                    {
                        SType = VkStructureType.PresentInfoKhr,
                        PNext = (void*) 0,
                        SwapchainCount = 1,
                        PSwapchains = &swapHandle,
                        PImageIndices = &imageIndex,
                        WaitSemaphoreCount = (uint) (waitSemaphores?.Length ?? 0),
                        PWaitSemaphores =
                            (VkSemaphore*) (waitSemaphores != null && waitSemaphores.Length > 0
                                ? Marshal.UnsafeAddrOfPinnedArrayElement(waitSemaphores, 0)
                                : IntPtr.Zero).ToPointer(),
                        PResults = &result
                    };
                    Handle.QueuePresentKHR(&info);
                    VkException.Check(result);
                }
                finally
                {
                    if (waitSemaphores != null && waitSemaphores.Length > 0)
                        pinWait.Free();
                }
            }
        }
    }
}