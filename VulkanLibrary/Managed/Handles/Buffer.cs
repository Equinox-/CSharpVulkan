using System;
using System.Runtime.InteropServices;
using NLog;
using VulkanLibrary.Managed.Buffers;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Buffer : IPinnableBindableBuffer, IDedicatedMemoryOwner
    {
        /// <summary>
        /// Size of this buffer
        /// </summary>
        public ulong Size { get; }
        
        /// <summary>
        /// Usage of this buffer
        /// </summary>
        public VkBufferUsageFlag Usage { get; }
        
        /// <summary>
        /// Creates a new buffer
        /// </summary>
        /// <param name="dev">device</param>
        /// <param name="usage">buffer usage</param>
        /// <param name="flags">buffer creation flags</param>
        /// <param name="size">buffer size</param>
        /// <param name="sharedQueueFamilies">Concurrency mode, or exclusive if empty</param>
        public Buffer(Device dev, VkBufferUsageFlag usage, VkBufferCreateFlag flags, ulong size,
            params uint[] sharedQueueFamilies)
        {
            Logging.Allocations?.Trace($"Creating {Extensions.FormatFileSize(size)} buffer with usage {usage}");
            
            Device = dev;
            Size = size;
            Usage = usage;
            unsafe
            {
                var pin = sharedQueueFamilies.Length > 0
                    ? GCHandle.Alloc(sharedQueueFamilies, GCHandleType.Pinned)
                    : default(GCHandle);
                try
                {
                    var info = new VkBufferCreateInfo()
                    {
                        SType = VkStructureType.BufferCreateInfo,
                        PNext = IntPtr.Zero,
                        Flags = flags,
                        Size = size,
                        Usage = usage,
                        SharingMode =
                            sharedQueueFamilies.Length > 0 ? VkSharingMode.Concurrent : VkSharingMode.Exclusive,
                        QueueFamilyIndexCount = (uint) sharedQueueFamilies.Length,
                        PQueueFamilyIndices =
                            (uint*) (sharedQueueFamilies.Length > 0
                                ? Marshal.UnsafeAddrOfPinnedArrayElement(sharedQueueFamilies, 0)
                                : IntPtr.Zero).ToPointer()
                    };
                    Handle = dev.Handle.CreateBuffer(&info, Instance.AllocationCallbacks);
                }
                finally
                {
                    if (sharedQueueFamilies.Length > 0)
                        pin.Free();
                }
            }
        }

        /// <summary>
        /// Requirements for this buffer's memory
        /// </summary>
        public MemoryRequirements MemoryRequirements
        {
            get
            {
                base.AssertValid();
                Device.AssertValid();
                if (!Device.ExtensionEnabled(VkExtension.KhrGetMemoryRequirements2))
                {
                    var requirements = Device.Handle.GetBufferMemoryRequirements(Handle);
                    return new MemoryRequirements()
                    {
                        TypeRequirements = requirements
                    };
                }
                unsafe
                {
                    var useDedicated = Device.ExtensionEnabled(VkExtension.KhrDedicatedAllocation);
                    var dedInfo = new VkMemoryDedicatedRequirementsKHR()
                    {
                        SType = VkStructureType.MemoryDedicatedRequirementsKhr,
                        PNext = IntPtr.Zero
                    };
                    var memReq2 = new VkBufferMemoryRequirementsInfo2KHR()
                    {
                        SType = VkStructureType.ImageMemoryRequirementsInfo2Khr,
                        PNext = useDedicated ? new IntPtr(&dedInfo) :IntPtr.Zero,
                        Buffer = Handle
                    };
                    var reqs2 = Device.Handle.GetBufferMemoryRequirements2KHR(&memReq2);
                    var result = new MemoryRequirements()
                    {
                        TypeRequirements = reqs2.MemoryRequirements
                    };
                    if (!useDedicated ||
                        (!dedInfo.RequiresDedicatedAllocation && !dedInfo.PrefersDedicatedAllocation))
                        return result;
                    result.DedicatedMemory.Value = this;
                    result.DedicatedMemory.RequirementLevel = dedInfo.RequiresDedicatedAllocation
                        ? MemoryRequirementLevel.Required
                        : MemoryRequirementLevel.Preferred;
                    return result;
                }
            }
        }

        protected void BindMemory(DeviceMemory mem, ulong offset)
        {
            Device.Handle.BindBufferMemory(Handle, mem.Handle, offset);
        }
        
        /// <inheritdoc/>
        public bool SetOwnerOn(ref VkMemoryDedicatedAllocateInfoKHR info)
        {
            info.Buffer = Handle;
            info.Image = VkImage.Null;
            return true;
        }

        public Buffer BindingHandle => this;
        public ulong Offset => 0;
    }
}