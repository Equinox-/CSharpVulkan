using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Device
    {
        private readonly Queue[][] _queues;

        /// <summary>
        /// Gets the queue with the given group and id.
        /// </summary>
        /// <param name="queueGroup">Group (not family)</param>
        /// <param name="queueIndex">Index in group</param>
        /// <returns>queue</returns>
        public Queue Queue(uint queueGroup, uint queueIndex)
        {
            return _queues[queueGroup][queueIndex];
        }

        /// <summary>
        /// Creates a new device and queues
        /// </summary>
        /// <param name="physDevice">Physical device</param>
        /// <param name="requiredExtensions">Required extension types</param>
        /// <param name="preferredExtensions">Preferred extension types</param>
        /// <param name="queueOptions">Queue options</param>
        public Device(PhysicalDevice physDevice, IEnumerable<Type> requiredExtensions,
            IEnumerable<Type> preferredExtensions,
            VkDeviceQueueCreateInfo[] queueOptions)
        {
            PhysicalDevice = physDevice;

            unsafe
            {
                fixed (VkDeviceQueueCreateInfo* queueOptionsPtr = queueOptions)
                {
                    var desiredFeatures = ChooseDeviceFeatures();
                    var deviceCreateInfo = new VkDeviceCreateInfo()
                    {
                        SType = VkStructureType.DeviceCreateInfo,
                        QueueCreateInfoCount = (uint) queueOptions.Length,
                        PQueueCreateInfos = queueOptionsPtr,
                        PEnabledFeatures = &desiredFeatures,
                        EnabledExtensionCount = 0,
                        PpEnabledExtensionNames = (byte**) 0,
                        EnabledLayerCount = 0,
                        PpEnabledLayerNames = (byte**) 0,
                    };

                    Handle = PhysicalDevice.Handle.CreateDevice(&deviceCreateInfo, (VkAllocationCallbacks*) 0);
                }
            }

            _queues = new Queue[queueOptions.Length][];
            for (var i = 0; i < queueOptions.Length; i++)
            {
                _queues[i] = new Queue[queueOptions[i].QueueCount];
                for (var j = 0; j < _queues[i].Length; j++)
                    _queues[i][j] = new Queue(this, queueOptions[i].QueueFamilyIndex, (uint) j);
            }
        }

        private static VkPhysicalDeviceFeatures ChooseDeviceFeatures()
        {
            return new VkPhysicalDeviceFeatures()
            {
                GeometryShader = true,
                TessellationShader = true,
                MultiDrawIndirect = true,
#if DEBUG
                PipelineStatisticsQuery = true,
#endif
                ShaderUniformBufferArrayDynamicIndexing = true,
                ShaderSampledImageArrayDynamicIndexing = true,
                ShaderStorageImageArrayDynamicIndexing = true,
                ShaderClipDistance = true,
                ShaderCullDistance = true,
                ShaderFloat64 = true,
                InheritedQueries = true
            };
        }
    }
}