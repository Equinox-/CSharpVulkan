using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class SwapchainKHRBuilder
    {
        private readonly SurfaceKHR _surf;
        private readonly Device _dev;

        private readonly List<KeyValuePair<VkColorSpaceKHR, VkFormat[]>> _preferredFormats =
            new List<KeyValuePair<VkColorSpaceKHR, VkFormat[]>>();

        private readonly List<KeyValuePair<VkColorSpaceKHR, VkFormat[]>> _requiredFormats =
            new List<KeyValuePair<VkColorSpaceKHR, VkFormat[]>>();

        private readonly VkExtent2D _extent;
        private VkSharingMode _shareMode;
        private VkSurfaceTransformFlagBitsKHR _preferredTransform, _requiredTransform;
        private uint[] _sharingParameters;
        private bool _clipped;
        private readonly List<VkPresentModeKHR> _preferredPresentModes = new List<VkPresentModeKHR>();
        private readonly List<VkPresentModeKHR> _requiredPresentModes = new List<VkPresentModeKHR>();
        private VkCompositeAlphaFlagBitsKHR _preferredCompositeAlpha, _requiredCompositeAlpha;
        private VkImageUsageFlag _preferredUsageFlags, _requiredUsageFlags;
        private readonly List<uint> _preferredLayerCount = new List<uint>();
        private readonly List<uint> _requiredLayerCount = new List<uint>();
        private readonly List<uint> _preferredImageCount = new List<uint>();
        private readonly List<uint> _requiredImageCount = new List<uint>();

        internal SwapchainKHRBuilder(SurfaceKHR surf, Device dev, VkExtent2D size)
        {
            _surf = surf;
            _dev = dev;
            _extent = size;
            _shareMode = VkSharingMode.Exclusive;
            _preferredTransform = 0;
            _requiredTransform = VkSurfaceTransformFlagBitsKHR.InheritBitKhr;
        }

        public SwapchainKHRBuilder PreferredFormats(VkColorSpaceKHR colorSpace, params VkFormat[] formats)
        {
            _preferredFormats.Add(new KeyValuePair<VkColorSpaceKHR, VkFormat[]>(colorSpace, formats));
            return this;
        }

        public SwapchainKHRBuilder RequiredFormats(VkColorSpaceKHR colorSpace, params VkFormat[] formats)
        {
            _requiredFormats.Add(new KeyValuePair<VkColorSpaceKHR, VkFormat[]>(colorSpace, formats));
            return this;
        }

        public SwapchainKHRBuilder Exclusive()
        {
            _shareMode = VkSharingMode.Exclusive;
            _sharingParameters = null;
            return this;
        }

        public SwapchainKHRBuilder Concurrent(params uint[] queues)
        {
            _shareMode = VkSharingMode.Concurrent;
            _sharingParameters = queues;
            return this;
        }

        public SwapchainKHRBuilder PreferredTransform(VkSurfaceTransformFlagBitsKHR pref)
        {
            _preferredTransform = pref;
            return this;
        }

        public SwapchainKHRBuilder RequiredTransform(VkSurfaceTransformFlagBitsKHR pref)
        {
            _requiredTransform = pref;
            return this;
        }

        public SwapchainKHRBuilder PreferredPresentModes(params VkPresentModeKHR[] modes)
        {
            _preferredPresentModes.AddRange(modes);
            return this;
        }

        public SwapchainKHRBuilder RequiredPresentModes(params VkPresentModeKHR[] modes)
        {
            _requiredPresentModes.AddRange(modes);
            return this;
        }

        public SwapchainKHRBuilder PreferredLayerCount(params uint[] modes)
        {
            _preferredLayerCount.AddRange(modes);
            return this;
        }

        public SwapchainKHRBuilder RequiredLayerCount(params uint[] modes)
        {
            _requiredLayerCount.AddRange(modes);
            return this;
        }

        public SwapchainKHRBuilder PreferredImageCount(params uint[] modes)
        {
            _preferredImageCount.AddRange(modes);
            return this;
        }

        public SwapchainKHRBuilder RequiredImageCount(params uint[] modes)
        {
            _requiredImageCount.AddRange(modes);
            return this;
        }

        public SwapchainKHRBuilder PreferredUsage(VkImageUsageFlag pref)
        {
            _preferredUsageFlags = pref;
            return this;
        }

        public SwapchainKHRBuilder RequiredUsage(VkImageUsageFlag pref)
        {
            _requiredUsageFlags = pref;
            return this;
        }

        public SwapchainKHRBuilder Clipped(bool val)
        {
            _clipped = val;
            return this;
        }

        public SwapchainKHRBuilder PreferredCompositeAlpha(VkCompositeAlphaFlagBitsKHR pref)
        {
            _preferredCompositeAlpha = pref;
            return this;
        }

        public SwapchainKHRBuilder RequiredCompositeAlpha(VkCompositeAlphaFlagBitsKHR pref)
        {
            _requiredCompositeAlpha = pref;
            return this;
        }

        private static ulong ChooseBit(ulong caps, ulong preferred, ulong required)
        {
            for (var i = 1Ul; i <= caps; i <<= 1)
                if ((caps & i) != 0 && (preferred & i) != 0)
                    return i;
            for (var i = 1Ul; i <= caps; i <<= 1)
                if ((caps & i) != 0 && (required & i) != 0)
                    return i;
            if (required != 0) return 0;
            for (var i = 1Ul; i <= caps; i <<= 1)
                if ((caps & i) != 0)
                    return i;
            return 0;
        }

        /// <summary>
        /// Creates a swapchain from this builder
        /// </summary>
        /// <returns>the swapchain</returns>
        /// <exception cref="NotSupportedException">If the swapchain isn't supported</exception>
        public SwapchainKHR Build()
        {
            var caps = _dev.PhysicalDevice.Handle.GetPhysicalDeviceSurfaceCapabilitiesKHR(_surf.Handle);
            var alphaComposite = (VkCompositeAlphaFlagBitsKHR) ChooseBit(
                (ulong) caps.SupportedCompositeAlpha, (ulong) _preferredCompositeAlpha,
                (ulong) _requiredCompositeAlpha);
            if (alphaComposite == 0)
                throw new NotSupportedException(
                    $"Alpha composite {_requiredCompositeAlpha} not supported in {caps.SupportedCompositeAlpha}");


            var surfaceTransform = (VkSurfaceTransformFlagBitsKHR) 0;
            {
                if (_preferredTransform == 0 ||
                    (_preferredTransform & VkSurfaceTransformFlagBitsKHR.InheritBitKhr) != 0)
                    surfaceTransform = caps.CurrentTransform;
                if (surfaceTransform == 0)
                {
                    surfaceTransform = (VkSurfaceTransformFlagBitsKHR) ChooseBit((ulong) caps.SupportedTransforms,
                        (ulong) _preferredTransform, (ulong) _requiredTransform);
                    if (surfaceTransform == 0 && (_requiredTransform == 0 ||
                                                  (_requiredTransform & VkSurfaceTransformFlagBitsKHR.InheritBitKhr) !=
                                                  0))
                        surfaceTransform = caps.CurrentTransform;
                }
                if (surfaceTransform == 0)
                    throw new NotSupportedException(
                        $"Surface transform {_requiredTransform} not supported in {caps.SupportedTransforms}");
            }

            var usageFlags = (VkImageUsageFlag) 0;
            {
                if ((_preferredUsageFlags & caps.SupportedUsageFlags) == _preferredUsageFlags)
                    usageFlags = _preferredUsageFlags;
                if ((_requiredUsageFlags & caps.SupportedUsageFlags) == _requiredUsageFlags)
                    usageFlags = _requiredUsageFlags;
                if (usageFlags == 0)
                    throw new NotSupportedException(
                        $"Usage flags {_requiredUsageFlags} not supported in {caps.SupportedUsageFlags}");
            }

            uint imageCount = 0;
            {
                foreach (var k in _preferredImageCount.Concat(_requiredImageCount))
                    if (k >= caps.MinImageCount && k <= caps.MaxImageCount)
                    {
                        imageCount = k;
                        break;
                    }
                if (imageCount == 0 && _requiredImageCount.Count == 0)
                    imageCount = caps.MinImageCount;
                if (imageCount == 0)
                    throw new NotSupportedException(
                        $"Image counts {string.Join(", ", _requiredImageCount)} not supported by {caps.MinImageCount} to {caps.MaxImageCount}");
            }

            uint layerCount = 0;
            {
                foreach (var k in _preferredLayerCount.Concat(_requiredLayerCount))
                    if (k <= caps.MaxImageArrayLayers)
                    {
                        layerCount = k;
                        break;
                    }
                if (layerCount == 0 && _requiredLayerCount.Count == 0)
                    layerCount = Math.Min(caps.MaxImageArrayLayers, 1);
                if (layerCount == 0)
                    throw new NotSupportedException(
                        $"Layer counts {string.Join(", ", _requiredLayerCount)} not supported by 1 to {caps.MaxImageArrayLayers}");
            }

            VkSurfaceFormatKHR? format = null;
            {
                var formats = _dev.PhysicalDevice.Handle.GetPhysicalDeviceSurfaceFormatsKHR(_surf.Handle);
                foreach (var test in _preferredFormats.Concat(_requiredFormats))
                {
                    foreach (var fmt in test.Value)
                    {
                        foreach (var cmp in formats)
                            if (cmp.Format == fmt && cmp.ColorSpace == test.Key)
                            {
                                format = cmp;
                                break;
                            }
                        if (format.HasValue)
                            break;
                    }
                    if (format.HasValue)
                        break;
                }
                if (!format.HasValue && _requiredFormats.Count == 0 && formats.Length > 0)
                    format = formats[0];
                if (!format.HasValue)
                {
                    var fmts = _requiredFormats.SelectMany(x => x.Value.Select(y => $"{{{x.Key}, {y}"));
                    var fmtsSupported = formats.Select(x => $"{{{x.ColorSpace}, {x.Format}");
                    throw new NotSupportedException(
                        $"Formats {string.Join(", ", fmts)} not supported by {string.Join(", ", fmtsSupported)}");
                }
            }

            VkPresentModeKHR? presentMode = null;
            {
                var opts = _dev.PhysicalDevice.Handle.GetPhysicalDeviceSurfacePresentModesKHR(_surf.Handle);
                foreach (var mode in _preferredPresentModes.Concat(_requiredPresentModes))
                    if (opts.Contains(mode))
                    {
                        presentMode = mode;
                        break;
                    }
                if (!presentMode.HasValue && _requiredPresentModes.Count == 0 && opts.Length > 0)
                    presentMode = opts[0];
                if (!presentMode.HasValue)
                    throw new NotSupportedException(
                        $"Present modes {string.Join(", ", _requiredPresentModes)} not supported by {string.Join(", ", opts)}");
            }

            return new SwapchainKHR(_surf, _dev, imageCount, layerCount, usageFlags, format.Value.Format,
                format.Value.ColorSpace,
                _extent, alphaComposite, presentMode.Value, _clipped, surfaceTransform, _shareMode, _sharingParameters);
        }
    }
}