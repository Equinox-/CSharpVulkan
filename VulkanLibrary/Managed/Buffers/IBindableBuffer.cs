using System;
using System.Diagnostics;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;
using Buffer = VulkanLibrary.Managed.Handles.Buffer;

namespace VulkanLibrary.Managed.Buffers
{
    public interface IBindableBuffer
    {
        Buffer BindingHandle { get; }
        ulong Offset { get; }
        ulong Size { get; }
    }

    public interface IPinnableBindableBuffer : IBindableBuffer, IPinnable
    {
    }

    public static class BindableBufferExtensions
    {
        public static VkBufferMemoryBarrier CreateBarrier(this IBindableBuffer buffer, uint srcQueue, uint dstQueue,
            VkAccessFlag srcAccess = VkAccessFlag.AllExceptExt, VkAccessFlag dstAccess = VkAccessFlag.AllExceptExt)
        {
            return new VkBufferMemoryBarrier()
            {
                Buffer = buffer.BindingHandle.Handle,
                PNext = IntPtr.Zero,
                Offset = buffer.Offset,
                Size = buffer.Size,
                SType = VkStructureType.BufferMemoryBarrier,
                SrcQueueFamilyIndex = srcQueue,
                DstQueueFamilyIndex = dstQueue,
                SrcAccessMask = srcAccess,
                DstAccessMask = dstAccess
            };
        }
    }
}