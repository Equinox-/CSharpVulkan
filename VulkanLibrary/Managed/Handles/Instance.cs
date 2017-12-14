using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using NLog;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Instance
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        
        public readonly unsafe VkAllocationCallbacks* AllocationCallbacks;

        /// <summary>
        /// Physical devices belonging to this instance
        /// </summary>
        public IReadOnlyList<PhysicalDevice> PhysicalDevices { get; }

        private readonly HashSet<VkExtension> _enabledExtensions;
        private readonly HashSet<string> _enableExtensionsByName;
        
        public Instance(ICollection<VkExtension> preferredExtensions, ICollection<VkExtension> requiredExtensions,
            ICollection<string> preferredLayers, ICollection<string> requiredLayers)
        {
            var supportedLayers = Vulkan.EnumerateLayerProperties().Select(x => x.LayerNameString).ToHashSet();
            Log.Info($"Supported instance layers: {string.Join(", ", supportedLayers)}");
            foreach (var requiredLayer in requiredLayers)
                if (!supportedLayers.Contains(requiredLayer))
                    throw new NotSupportedException($"Layer {requiredLayer} isn't supported");
            var layersToUse = requiredLayers.Union(preferredLayers.Where(supportedLayers.Contains)).ToList();

            var supportedExtensions = Vulkan.EnumerateExtensionProperties(null).Union(
                    layersToUse.SelectMany(Vulkan.EnumerateExtensionProperties)).Select(x => x.ExtensionNameString)
                .ToHashSet();
            Log.Info($"Supported instance extensions: {string.Join(", ", supportedExtensions)}");
            foreach (var requiredExtension in requiredExtensions)
                if (!supportedExtensions.Contains(VkExtensionDatabase.Extension(requiredExtension).Extension))
                    throw new NotSupportedException($"Extension {requiredExtension} isn't supported");

            var extensionsToUse = requiredExtensions.Select(VkExtensionDatabase.Extension).Select(x => x.Extension)
                .Union(
                    preferredExtensions.Select(VkExtensionDatabase.Extension).Select(x => x.Extension)
                        .Where(supportedExtensions.Contains)).ToList();

            _enabledExtensions =
                extensionsToUse.Select(VkExtensionDatabase.Extension).Where(y => y != null).Select(x=>x.ExtensionId).ToHashSet();
            _enableExtensionsByName = extensionsToUse.ToHashSet();
            
            Log.Info($"Using instance layers: {string.Join(", ", layersToUse)}");
            Log.Info($"Using instance extensions: {string.Join(", ", extensionsToUse)}");

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
                    AllocationCallbacks = (VkAllocationCallbacks*) 0;
                    var appInfo = new VkApplicationInfo()
                    {
                        SType = VkStructureType.ApplicationInfo,
                        ApiVersion = new VkVersion(1, 0, 0),
                        PApplicationName = (byte*) 0,
                        PEngineName = (byte*) 0,
                        PNext = IntPtr.Zero
                    };

                    var instanceInfo = new VkInstanceCreateInfo()
                    {
                        SType = VkStructureType.InstanceCreateInfo,
                        EnabledExtensionCount = (uint) extensionsToUse.Count,
                        PpEnabledExtensionNames = extensionsToUse.Count > 0
                            ? (byte**) Marshal.UnsafeAddrOfPinnedArrayElement(extensionsToUseAnsi, 0).ToPointer()
                            : (byte**) 0,
                        EnabledLayerCount = (uint) layersToUse.Count,
                        PpEnabledLayerNames =  layersToUse.Count > 0
                            ? (byte**) Marshal.UnsafeAddrOfPinnedArrayElement(layersToUseAnsi, 0).ToPointer()
                            : (byte**) 0,
                        Flags = 0,
                        PApplicationInfo = &appInfo,
                        PNext = IntPtr.Zero
                    };
                    Handle = Vulkan.CreateInstance(&instanceInfo, AllocationCallbacks);
                }
                finally
                {
                    pinnedLayersToUse.Free();
                    pinnedExtensionsToUse.Free();
                    foreach (var ptr in layersToUseAnsi)
                        Marshal.FreeHGlobal(ptr);
                    foreach (var ptr in extensionsToUseAnsi)
                        Marshal.FreeHGlobal(ptr);
                }
            }

            var devs = new List<PhysicalDevice>();
            foreach (var dev in Handle.EnumeratePhysicalDevices())
                devs.Add(new PhysicalDevice(this, dev));
            PhysicalDevices = devs;
        }

        /// <summary>
        /// Checks if the given extension is enabled on this device.
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public bool ExtensionEnabled(VkExtension ext)
        {
            return _enabledExtensions.Contains(ext);
        }

        /// <summary>
        /// Checks if the given extension is enabled on this device.
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public bool ExtensionEnabled(string ext)
        {
            return _enableExtensionsByName.Contains(ext);
        }

        /// <summary>
        /// Creates a Win32 surface
        /// </summary>
        /// <param name="hInstance">Application ID</param>
        /// <param name="hwnd">Window ID</param>
        /// <returns>Surface</returns>
        public SurfaceKHR CreateSurfaceWin32(IntPtr hInstance, IntPtr hwnd)
        {
            unsafe
            {
                var info = new VkWin32SurfaceCreateInfoKHR()
                {
                    SType = VkStructureType.Win32SurfaceCreateInfoKhr,
                    PNext = IntPtr.Zero,
                    Flags = 0,
                    Hwnd = hwnd,
                    Hinstance = hInstance
                };
                return new SurfaceKHR(this, Handle.CreateWin32SurfaceKHR(&info, AllocationCallbacks));
            }
        }
    }
}