using System;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    /// <summary>
    /// Represents a handle to device memory
    /// </summary>
    public partial class DeviceMemory
    {
        /// <summary>
        /// Size of this memory allocation
        /// </summary>
        public ulong Capacity { get; private set; }

        /// <summary>
        /// The owner of this memory allocation, or null if it wasn't a dedicated allocation.
        /// </summary>
        public IDedicatedMemoryOwner DedicatedMemoryOwner { get; private set; }

        public readonly MemoryType MemoryType;

        internal DeviceMemory(Device device, MemoryRequirements requirements)
        {
            Device = device;
            MemoryType = requirements.FindMemoryType(PhysicalDevice);
            if (MemoryType == null)
                throw new NotSupportedException($"Unable to find memory type for {requirements}");

            if (requirements.DedicatedMemory.RequirementLevel != MemoryRequirementLevel.None)
            {
                if (TryAllocateDedicated(requirements, MemoryType))
                    return;
                if (requirements.DedicatedMemory.RequirementLevel == MemoryRequirementLevel.Required)
                    throw new NotSupportedException($"Unable to allocate dedicated memory for {requirements}");
            }
            if (!TryAllocateNormal(requirements.TypeRequirements.Size, MemoryType))
                throw new NotSupportedException($"Failed to allocate memory for {requirements}");
        }

        internal DeviceMemory(Device device, ulong capacity, uint typeIndex)
        {
            Device = device;
            MemoryType = device.PhysicalDevice.MemoryTypes[(int) typeIndex];
            if (!TryAllocateNormal(capacity, MemoryType))
                throw new NotSupportedException($"Failed to allocate {capacity} on memory type {MemoryType}");
        }

        private bool TryAllocateDedicated(MemoryRequirements req, MemoryType memoryType)
        {
            unsafe
            {
                if (!Device.ExtensionEnabled(VkExtension.KhrDedicatedAllocation))
                    return false;
                    var dedInfo = new VkMemoryDedicatedAllocateInfoKHR()
                    {
                        SType = VkStructureType.MemoryDedicatedAllocateInfoKhr,
                        PNext = (void*) 0
                    };
                    if (!req.DedicatedMemory.Value.SetOwnerOn(ref dedInfo))
                        return false;
                var info = new VkMemoryAllocateInfo()
                {
                    SType = VkStructureType.MemoryAllocateInfo,
                    AllocationSize = req.TypeRequirements.Size,
                    MemoryTypeIndex = memoryType.TypeIndex,
                    PNext = &dedInfo
                };
                Handle = Device.Handle.AllocateMemory(&info, Instance.AllocationCallbacks);
                if (Handle == VkDeviceMemory.Null) return false;
                DedicatedMemoryOwner = req.DedicatedMemory.Value;
                Capacity = req.TypeRequirements.Size;
                return true;
            }
        }

        private bool TryAllocateNormal(ulong capacity, MemoryType memoryType)
        {
            unsafe
            {
                var info = new VkMemoryAllocateInfo()
                {
                    SType = VkStructureType.MemoryAllocateInfo,
                    AllocationSize = capacity,
                    MemoryTypeIndex = memoryType.TypeIndex,
                    PNext = (void*) 0
                };
                Handle = Device.Handle.AllocateMemory(&info, Instance.AllocationCallbacks);
            }
            if (Handle == VkDeviceMemory.Null) return false;
            DedicatedMemoryOwner = null;
            Capacity = capacity;
            return true;
        }
    }
}