using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Memory;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class DeviceBuilder
    {
        private readonly HashSet<VkExtension> _preferredExtensions = new HashSet<VkExtension>();
        private readonly HashSet<VkExtension> _requiredExtensions = new HashSet<VkExtension>();
        private readonly HashSet<string> _preferredLayers = new HashSet<string>();
        private readonly HashSet<string> _requiredLayers = new HashSet<string>();
        private readonly List<Device.QueueCreateInfo> _queueInfo = new List<Device.QueueCreateInfo>();
        private readonly PhysicalDevice _physicalDevice;
        private VkPhysicalDeviceFeatures _features;

        internal DeviceBuilder(PhysicalDevice dev)
        {
            _physicalDevice = dev;
            _features = default;
        }

        public DeviceBuilder WithFeatures(VkPhysicalDeviceFeatures f)
        {
            _features = f;
            return this;
        }
        
        public DeviceBuilder RequireExtensions(params VkExtension[] ext)
        {
            foreach (var i in ext)
                _requiredExtensions.Add(i);
            return this;
        }

        public DeviceBuilder RequireLayers(params string[] ext)
        {
            foreach (var i in ext)
                _requiredLayers.Add(i);
            return this;
        }

        public DeviceBuilder PreferExtensions(params VkExtension[] ext)
        {
            foreach (var i in ext)
                _preferredExtensions.Add(i);
            return this;
        }

        public DeviceBuilder PreferLayers(params string[] ext)
        {
            foreach (var i in ext)
                _preferredLayers.Add(i);
            return this;
        }

        public DeviceBuilder RequireExtensions(IEnumerable<VkExtension> ext)
        {
            foreach (var i in ext)
                _requiredExtensions.Add(i);
            return this;
        }

        public DeviceBuilder RequireLayers(IEnumerable<string> ext)
        {
            foreach (var i in ext)
                _requiredLayers.Add(i);
            return this;
        }

        public DeviceBuilder PreferExtensions(IEnumerable<VkExtension> ext)
        {
            foreach (var i in ext)
                _preferredExtensions.Add(i);
            return this;
        }

        public DeviceBuilder PreferLayers(IEnumerable<string> ext)
        {
            foreach (var i in ext)
                _preferredLayers.Add(i);
            return this;
        }

        public DeviceBuilder WithQueueFamily(VkQueueFlag preferredFlags, VkQueueFlag requiredFlags,
            params float[] priorities)
        {
            return WithQueueFamily(preferredFlags, requiredFlags, null, null, priorities);
        }

        public DeviceBuilder WithQueueFamily(VkQueueFlag preferredFlags, VkQueueFlag requiredFlags,
            Func<PhysicalDevice, uint, bool> preferredPred,
            Func<PhysicalDevice, uint, bool> requiredPred,
            params float[] priorities)
        {
            var family = _physicalDevice.FindQueueFamily(preferredFlags, requiredFlags, preferredPred, requiredPred);
            _queueInfo.Add(new Device.QueueCreateInfo(family, priorities));
            return this;
        }

        /// <summary>
        /// Builds the device described by this builder.
        /// </summary>
        /// <returns>the new device</returns>
        public Device Build()
        {
            return new Device(_physicalDevice, _preferredExtensions, _requiredExtensions, _preferredLayers,
                _requiredLayers, _queueInfo.ToArray(), _features);
        }
    }
}