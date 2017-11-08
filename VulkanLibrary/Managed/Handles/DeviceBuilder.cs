using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class DeviceBuilder : CriticalFinalizerObject, IDisposable
    {
        private bool _valid;
        private readonly List<VkExtension> _preferredExtensions = new List<VkExtension>();
        private readonly List<VkExtension> _requiredExtensions = new List<VkExtension>();
        private readonly List<string> _preferredLayers = new List<string>();
        private readonly List<string> _requiredLayers = new List<string>();
        private readonly List<VkDeviceQueueCreateInfo> _queueInfo = new List<VkDeviceQueueCreateInfo>();
        private readonly List<GCHandle> _unmanagedPointers = new List<GCHandle>();
        private readonly PhysicalDevice _physicalDevice;

        internal DeviceBuilder(PhysicalDevice dev)
        {
            _valid = true;
            _physicalDevice = dev;
        }

        public DeviceBuilder RequireExtensions(params VkExtension[] ext)
        {
            Debug.Assert(_valid);
            _requiredExtensions.AddRange(ext);
            return this;
        }

        public DeviceBuilder RequireLayers(params string[] ext)
        {
            Debug.Assert(_valid);
            _requiredLayers.AddRange(ext);
            return this;
        }

        public DeviceBuilder PreferExtensions(params VkExtension[] ext)
        {
            Debug.Assert(_valid);
            _preferredExtensions.AddRange(ext);
            return this;
        }

        public DeviceBuilder PreferLayers(params string[] ext)
        {
            Debug.Assert(_valid);
            _preferredLayers.AddRange(ext);
            return this;
        }

        public DeviceBuilder WithQueueFamily(VkQueueFlag preferredFlags, VkQueueFlag requiredFlags,
            params float[] priorities)
        {
            Debug.Assert(_valid);
            _unmanagedPointers.Add(GCHandle.Alloc(priorities, GCHandleType.Pinned));
            var family = _physicalDevice.FindQueueFamily(preferredFlags, requiredFlags);
            unsafe
            {
                _queueInfo.Add(new VkDeviceQueueCreateInfo()
                {
                    SType = VkStructureType.DeviceQueueCreateInfo,
                    PNext = (void*) 0,
                    PQueuePriorities = (float*) Marshal.UnsafeAddrOfPinnedArrayElement(priorities, 0).ToPointer(),
                    Flags = 0,
                    QueueCount = (uint) priorities.Length,
                    QueueFamilyIndex = family
                });
            }
            return this;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            if (_unmanagedPointers != null)
            {
                foreach (var k in _unmanagedPointers)
                    k.Free();
                _unmanagedPointers.Clear();
            }
            _valid = false;
        }
        
        /// <summary>
        /// Builds the device described by this builder.  Also calls <see cref="Dispose"/>
        /// </summary>
        /// <returns>the new device</returns>
        public Device Build()
        {
            Debug.Assert(_valid);
            var res = new Device(_physicalDevice, _preferredExtensions, _requiredExtensions, _preferredLayers,
                _requiredLayers, _queueInfo.ToArray());
            Dispose();
            return res;
        }

        ~DeviceBuilder()
        {
            Dispose();
        }
    }
}