using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Memory.Mapped
{
    /// <summary>
    /// Represents a section of mapped memory for a raw memory handle.
    /// </summary>
    public class MappedMemory : VulkanHandle<IntPtr>, IMappedMemory
    {
        protected readonly DeviceMemory Backing;

        /// <summary>
        /// Offset of this mapped memory region.
        /// </summary>
        public ulong Offset { get; }

        /// <inheritdoc cref="IMappedMemory.Size"/>
        public ulong Size { get; }

        internal MappedMemory(DeviceMemory backing, ulong offset, ulong size, uint flags)
        {
            if (size == Vulkan.WholeSize)
                size = backing.Capacity - offset;

            Debug.Assert(offset + size <= backing.Capacity,
                $"End of mapped region is beyond allocated capacity. {offset} + {size} > {backing.Capacity}");
            Debug.Assert(backing.MemoryType.HostVisible, $"Can not map memory regions on a non host visible allocation");
            
            Backing = backing;
            unsafe
            {
                void* mapData;
                backing.Device.Handle.MapMemory(backing.Handle, offset, size, flags, &mapData);
                Handle = new IntPtr(mapData);
            }
            Offset = offset;
            Size = size;
        }

        /// <inheritdoc cref="VulkanHandle{T}.AssertValid"/>
        public override void AssertValid()
        {
            base.AssertValid();
            Backing.AssertValid();
        }

        protected override void Free()
        {
            unsafe
            {
                Backing.Device.Handle.UnmapMemory(Backing.Handle);
            }
            Handle = IntPtr.Zero;
        }
        
        
        /// <inheritdoc />
        public void Flush()
        {
            FlushRange(0, Vulkan.WholeSize);
        }

        /// <inheritdoc />
        public void FlushRange(ulong offset, ulong count)
        {
            Debug.Assert(!Backing.MemoryType.HostCoherent, "Flushing range on incoherent memory");
            var range = RangeDesc(offset, count);
            unsafe
            {
                VkDevice.vkFlushMappedMemoryRanges(Backing.Device.Handle, 1, &range);
            }
        }

        /// <inheritdoc />
        public void Invalidate()
        {
            InvalidateRange(0, Vulkan.WholeSize);
        }

        /// <inheritdoc />
        public void InvalidateRange(ulong offset, ulong count)
        {
            Debug.Assert(!Backing.MemoryType.HostCoherent, "Invalidating range on incoherent memory");
            var range = RangeDesc(offset, count);
            unsafe
            {
                VkDevice.vkInvalidateMappedMemoryRanges(Backing.Device.Handle, 1, &range);
            }
        }

        /// <summary>
        /// Creates a mapped memory range description
        /// </summary>
        /// <param name="offset">Start of range</param>
        /// <param name="count">Size of range</param>
        /// <returns>Range descriptor</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VkMappedMemoryRange RangeDesc(ulong offset, ulong count)
        {
            AssertValid();
            if (count == Vulkan.WholeSize)
                Debug.Assert(offset < Size, $"Range to flush starts beyond capacity. {offset} + {count} >= {Size}");
            else
                Debug.Assert(offset + count < Size,
                    $"End of range to flush is beyond capacity. {offset} + {count} >= {Size}");

            offset += Offset;

            var atomSize = Backing.PhysicalDevice.Limits.NonCoherentAtomSize;
            Debug.Assert((offset % atomSize) == 0, $"Memory range raw offset isn't a multiple of NonCoherentAtomSize");
            if (count == Vulkan.WholeSize)
                Debug.Assert(((offset + Size) % atomSize) == 0,
                    $"Memory range raw end isn't a multiple of NonCoherentAtomSize");
            else
                Debug.Assert((count % atomSize) == 0 || (offset + count) == Backing.Capacity,
                    $"Memory range raw end isn't a multiple of NonCoherentAtomSize or the raw memory capacity");

            unsafe
            {
                return new VkMappedMemoryRange()
                {
                    SType = VkStructureType.MappedMemoryRange,
                    Memory = Backing.Handle,
                    PNext = IntPtr.Zero,
                    Offset = offset,
                    Size = count
                };
            }
        }
    }
}