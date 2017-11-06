using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Utilities;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class PhysicalDevice
    {
        // Not readonly so it doesn't copy
        private VkPhysicalDeviceProperties _properties;

        /// <summary>
        /// Device properties.
        /// </summary>
        public VkPhysicalDeviceProperties Properties => _properties;

        /// <summary>
        /// Device limits
        /// </summary>
        public VkPhysicalDeviceLimits Limits => _properties.Limits;

        /// <summary>
        /// Memory types supported by this device
        /// </summary>
        public readonly IReadOnlyList<MemoryType> MemoryTypes;
        
        /// <summary>
        /// Memory heaps supported by this device
        /// </summary>
        public readonly IReadOnlyList<MemoryHeap> MemoryHeaps;

        /// <summary>
        /// Device name
        /// </summary>
        public string DeviceName { get; }

        internal PhysicalDevice(Instance instance, VkPhysicalDevice handle)
        {
            Instance = instance;
            Handle = handle;
            unsafe
            {
                var props = Handle.GetPhysicalDeviceProperties();
                _properties = props;
                DeviceName = Marshal.PtrToStringAnsi(new IntPtr(props.DeviceName));
                {
                    var memProps = Handle.GetPhysicalDeviceMemoryProperties();
                    var heapList = new List<MemoryHeap>((int) memProps.MemoryHeapCount);
                    for (uint i = 0; i < memProps.MemoryHeapCount; i++)
                        heapList.Add(new MemoryHeap(i, memProps.MemoryHeaps[(int) i]));
                    MemoryHeaps = heapList;
                    var typeList = new List<MemoryType>((int) memProps.MemoryTypeCount);
                    for (uint i = 0; i < memProps.MemoryTypeCount; i++)
                        typeList.Add(new MemoryType(i, heapList[(int) memProps.MemoryTypes[(int) i].HeapIndex],
                            (VkMemoryPropertyFlag) memProps.MemoryTypes[(int) i].PropertyFlags));
                    MemoryTypes = typeList;
                }
            }
        }
    }
}