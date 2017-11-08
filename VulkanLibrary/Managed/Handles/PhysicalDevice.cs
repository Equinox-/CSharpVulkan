using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Memory;
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
        /// Extension properties
        /// </summary>
        public ICollection<VkExtensionProperties> ExtensionProperties { get; }

        /// <summary>
        /// Extensions
        /// </summary>
        public ICollection<ExtensionDescriptionAttribute> AvailableExtensions { get; }

        /// <summary>
        /// Layer properties
        /// </summary>
        public ICollection<VkLayerProperties> LayerProperties { get; }

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

            {
                ExtensionProperties = new List<VkExtensionProperties>(Handle.EnumerateExtensionProperties(null));
                LayerProperties = new List<VkLayerProperties>(Handle.EnumerateLayerProperties());
                var availableExt = new HashSet<ExtensionDescriptionAttribute>();
                foreach (var ext in ExtensionProperties)
                {
                    var desc = VkExtensionDatabase.Extension(ext.ExtensionNameString);
                    if (desc != null)
                        availableExt.Add(desc);
                }
                AvailableExtensions = availableExt;
            }
        }

        /// <summary>
        /// Creates a new device builder
        /// </summary>
        /// <returns>the builder</returns>
        public DeviceBuilder DeviceBuilder()
        {
            return new DeviceBuilder(this);
        }

        /// <summary>
        /// Finds the queue family index with the given options
        /// </summary>
        /// <param name="preferredFlags">Prefer to have these options</param>
        /// <param name="requiredFlags">Require these options</param>
        /// <returns>the queue family index</returns>
        /// <exception cref="NotSupportedException">no such queue family exists</exception>
        public uint FindQueueFamily(VkQueueFlag preferredFlags, VkQueueFlag requiredFlags)
        {
            var queues = Handle.GetQueueFamilyProperties();
            var both = preferredFlags | requiredFlags;
            for (var i = 0; i < queues.Length; i++)
                if ((queues[i].QueueFlags & both) == both)
                    return (uint) i;
            for (var i = 0; i < queues.Length; i++)
                if ((queues[i].QueueFlags & requiredFlags) == requiredFlags)
                    return (uint) i;
            throw new NotSupportedException($"Queue family with flags {requiredFlags} isn't supported");
        }
    }
}