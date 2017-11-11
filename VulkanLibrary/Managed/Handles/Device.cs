using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using VulkanLibrary.Managed.Memory.Pool;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Device
    {
        private readonly Queue[][] _queues;

        /// <summary>
        /// Pooled memory allocator
        /// </summary>
        public readonly VulkanMemoryPools MemoryPool;

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
        /// Gets all queues on this device
        /// </summary>
        public IReadOnlyList<Queue> Queues { get; }

        private readonly HashSet<VkExtension> _enabledExtensions;
        private readonly HashSet<string> _enableExtensionsByName;

        public struct QueueCreateInfo
        {
            public readonly uint Family;
            public readonly IReadOnlyList<float> Priorities;

            public QueueCreateInfo(uint family, params float[] prior)
            {
                Family = family;
                Priorities = new List<float>(prior);
            }
        }

        /// <summary>
        /// Creates a new device and queues
        /// </summary>
        /// <param name="physDevice">Physical device</param>
        /// <param name="requiredLayers"></param>
        /// <param name="preferredExtensions"></param>
        /// <param name="requiredExtensions"></param>
        /// <param name="preferredLayers"></param>
        /// <param name="queueOptions">Queue options</param>
        public Device(PhysicalDevice physDevice, ICollection<VkExtension> preferredExtensions,
            ICollection<VkExtension> requiredExtensions,
            ICollection<string> preferredLayers, ICollection<string> requiredLayers,
            QueueCreateInfo[] queueOptions)
        {
            PhysicalDevice = physDevice;

            foreach (var ext in preferredExtensions.Union(requiredExtensions).Select(VkExtensionDatabase.Extension))
                Debug.Assert(ext.Type == ExtensionType.Device || ext.Type == ExtensionType.Unknown,
                    $"Ext {ext.Extension} type {ext.Type} doesn't conform");
            var supportedLayers =
                physDevice.Handle.EnumerateLayerProperties().Select(x => x.LayerNameString).ToHashSet();
            Console.WriteLine($"Supported device layers: {string.Join(", ", supportedLayers)}");
            foreach (var requiredLayer in requiredLayers)
                if (!supportedLayers.Contains(requiredLayer))
                    throw new NotSupportedException($"Layer {requiredLayer} isn't supported");
            var layersToUse = requiredLayers.Union(preferredLayers.Where(supportedLayers.Contains)).ToList();

            var supportedExtensions = physDevice.Handle.EnumerateExtensionProperties(null).Union(
                    layersToUse.SelectMany(physDevice.Handle.EnumerateExtensionProperties))
                .Select(x => x.ExtensionNameString).ToHashSet();
            Console.WriteLine($"Supported device extensions: {string.Join(", ", supportedExtensions)}");
            foreach (var requiredExtension in requiredExtensions)
                if (!supportedExtensions.Contains(VkExtensionDatabase.Extension(requiredExtension).Extension))
                    throw new NotSupportedException($"Extension {requiredExtension} isn't supported");
            var extensionsToUse = requiredExtensions.Select(VkExtensionDatabase.Extension).Select(x => x.Extension)
                .Union(
                    preferredExtensions.Select(VkExtensionDatabase.Extension).Select(x => x.Extension)
                        .Where(supportedExtensions.Contains)).ToList();

            _enabledExtensions =
                extensionsToUse.Select(VkExtensionDatabase.Extension).Where(y => y != null).Select(x => x.ExtensionId)
                    .ToHashSet();
            _enableExtensionsByName = extensionsToUse.ToHashSet();

            Console.WriteLine($"Using device layers: {string.Join(", ", layersToUse)}");
            Console.WriteLine($"Using device extensions: {string.Join(", ", extensionsToUse)}");

            var pins = new List<GCHandle>();
            var queueOptionsRedirect = new int[queueOptions.Length];
            try
            {
                VkDeviceQueueCreateInfo[] queueCreateInfo;
                {
                    var queueOptionsRewrite = new Dictionary<uint, List<float>>();
                    for (var queueId = 0; queueId < queueOptions.Length; queueId++)
                    {
                        var opti = queueOptions[queueId];
                        if (opti.Priorities.Count == 0)
                            continue;
                        if (!queueOptionsRewrite.TryGetValue(opti.Family, out var list))
                            list = queueOptionsRewrite[opti.Family] = new List<float>();
                        queueOptionsRedirect[queueId] = list.Count;
                        list.AddRange(opti.Priorities);
                    }
                    queueCreateInfo = new VkDeviceQueueCreateInfo[queueOptionsRewrite.Count];
                    var family = 0;
                    foreach (var kv in queueOptionsRewrite)
                    {
                        unsafe
                        {
                            var block = kv.Value.ToArray();
                            pins.Add(GCHandle.Alloc(block, GCHandleType.Pinned));
                            queueCreateInfo[family++] = new VkDeviceQueueCreateInfo()
                            {
                                SType = VkStructureType.DeviceQueueCreateInfo,
                                Flags = 0,
                                PNext = (void*) 0,
                                QueueFamilyIndex = kv.Key,
                                QueueCount = (uint) block.Length,
                                PQueuePriorities = (float*) Marshal.UnsafeAddrOfPinnedArrayElement(block, 0).ToPointer()
                            };
                        }
                    }
                }

                unsafe
                {
                    var layersToUseAnsi = new IntPtr[layersToUse.Count];
                    for (var i = 0; i < layersToUse.Count; i++)
                        layersToUseAnsi[i] = Marshal.StringToHGlobalAnsi(layersToUse[i]);
                    var extensionsToUseAnsi = new IntPtr[extensionsToUse.Count];
                    for (var i = 0; i < extensionsToUse.Count; i++)
                        extensionsToUseAnsi[i] = Marshal.StringToHGlobalAnsi(extensionsToUse[i]);

                    var pinnedLayersToUse = GCHandle.Alloc(layersToUseAnsi, GCHandleType.Pinned);
                    var pinnedExtensionsToUse = GCHandle.Alloc(extensionsToUseAnsi, GCHandleType.Pinned);
                    try
                    {
                        fixed (VkDeviceQueueCreateInfo* queueOptionsPtr = queueCreateInfo)
                        {
                            var desiredFeatures = ChooseDeviceFeatures();
                            var deviceCreateInfo = new VkDeviceCreateInfo()
                            {
                                SType = VkStructureType.DeviceCreateInfo,
                                QueueCreateInfoCount = (uint) queueOptions.Length,
                                PQueueCreateInfos = queueOptionsPtr,
                                PEnabledFeatures = &desiredFeatures,
                                EnabledExtensionCount = (uint) extensionsToUse.Count,
                                PpEnabledExtensionNames = extensionsToUse.Count > 0
                                    ? (byte**) Marshal.UnsafeAddrOfPinnedArrayElement(extensionsToUseAnsi, 0)
                                        .ToPointer()
                                    : (byte**) 0,
                                EnabledLayerCount = (uint) layersToUse.Count,
                                PpEnabledLayerNames = layersToUse.Count > 0
                                    ? (byte**) Marshal.UnsafeAddrOfPinnedArrayElement(layersToUseAnsi, 0).ToPointer()
                                    : (byte**) 0,
                            };

                            Handle = PhysicalDevice.Handle.CreateDevice(&deviceCreateInfo,
                                Instance.AllocationCallbacks);
                        }
                    }
                    finally
                    {
                        foreach (var ptr in layersToUseAnsi)
                            Marshal.FreeHGlobal(ptr);
                        foreach (var ptr in extensionsToUseAnsi)
                            Marshal.FreeHGlobal(ptr);
                    }
                }
            }
            finally
            {
                foreach (var pin in pins)
                    pin.Free();
                pins.Clear();
            }

            _queues = new Queue[queueOptions.Length][];
            var queuesAll = new List<Queue>();
            for (var i = 0; i < queueOptions.Length; i++)
            {
                _queues[i] = new Queue[queueOptions[i].Priorities.Count];
                for (var j = 0; j < _queues[i].Length; j++)
                    queuesAll.Add(_queues[i][j] =
                        new Queue(this, queueOptions[i].Family, (uint) (queueOptionsRedirect[i] + j)));
            }
            Queues = queuesAll;

            MemoryPool = new VulkanMemoryPools(this);
        }

        /// <summary>
        /// Creates a swapchain builder
        /// </summary>
        /// <param name="surface">surface to build a swapchain for</param>
        /// <param name="size">size of swapchain or null to use surface size</param>
        /// <returns>swapchain builder</returns>
        public SwapchainKHRBuilder SwapchainBuilder(SurfaceKHR surface, VkExtent2D? size = null)
        {
            if (!size.HasValue)
                size = surface.Capabilities(PhysicalDevice).CurrentExtent;
            return new SwapchainKHRBuilder(surface, this, size.Value);
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

        /// <summary>
        /// Checks if the given extension is enabled on this device.
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public bool ExtensionEnabled(VkExtension ext)
        {
            return _enabledExtensions.Contains(ext) || Instance.ExtensionEnabled(ext);
        }

        /// <summary>
        /// Checks if the given extension is enabled on this device.
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public bool ExtensionEnabled(string ext)
        {
            return _enableExtensionsByName.Contains(ext) || Instance.ExtensionEnabled(ext);
        }

        /// <summary>
        /// Starts building a new render pass
        /// </summary>
        /// <returns>render pass builder</returns>
        public RenderPassBuilder RenderPassBuilder()
        {
            return new RenderPassBuilder(this);
        }

        /// <summary>
        /// Starts building a new render pass
        /// </summary>
        /// <typeparam name="TAttachment">Attachment ID type</typeparam>
        /// <typeparam name="TPass">Subpass ID type</typeparam>
        /// <returns>render pass builder</returns>
        public RenderPassWithIdentifiersBuilder<TAttachment, TPass> RenderPassBuilder<TAttachment, TPass>()
        {
            return new RenderPassWithIdentifiersBuilder<TAttachment, TPass>(this);
        }

        /// <summary>
        /// Starts building a new pipeline layout
        /// </summary>
        /// <returns>builder</returns>
        public PipelineLayoutBuilder PipelineLayoutBuilder()
        {
            return new PipelineLayoutBuilder(this);
        }

        public ShaderModule LoadShader(byte[] code)
        {
            return new ShaderModule(this, code);
        }
    }
}