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
        private readonly List<VkExtension> _preferredExtensions = new List<VkExtension>();
        private readonly List<VkExtension> _requiredExtensions = new List<VkExtension>();
        private readonly List<string> _preferredLayers = new List<string>();
        private readonly List<string> _requiredLayers = new List<string>();
        private readonly List<Device.QueueCreateInfo> _queueInfo = new List<Device.QueueCreateInfo>();
        private readonly PhysicalDevice _physicalDevice;

        internal DeviceBuilder(PhysicalDevice dev)
        {
            _physicalDevice = dev;
        }

        public DeviceBuilder RequireExtensions(params VkExtension[] ext)
        {
            _requiredExtensions.AddRange(ext);
            return this;
        }

        public DeviceBuilder RequireLayers(params string[] ext)
        {
            _requiredLayers.AddRange(ext);
            return this;
        }

        public DeviceBuilder PreferExtensions(params VkExtension[] ext)
        {
            _preferredExtensions.AddRange(ext);
            return this;
        }

        public DeviceBuilder PreferLayers(params string[] ext)
        {
            _preferredLayers.AddRange(ext);
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
                _requiredLayers, _queueInfo.ToArray());
        }
    }
}