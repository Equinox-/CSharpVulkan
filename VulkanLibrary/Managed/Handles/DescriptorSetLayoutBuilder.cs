using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class DescriptorSetLayoutBuilder
    {
        private readonly Device _device;

        private readonly List<GCHandle> _pins = new List<GCHandle>();

        private readonly VkDescriptorSetLayoutCreateFlag _flags;

        private readonly Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings =
            new Dictionary<uint, VkDescriptorSetLayoutBinding>();

        internal DescriptorSetLayoutBuilder(Device dev, VkDescriptorSetLayoutCreateFlag flags)
        {
            _device = dev;
            _flags = flags;
        }

        /// <summary>
        /// Adds the given binding descriptor
        /// </summary>
        /// <param name="binding">Binding to add</param>
        /// <param name="type">Binding type</param>
        /// <param name="arraySize">Binding's array size</param>
        /// <param name="flags">Shader stages</param>
        /// <param name="immutableSamplers">Immutable samplers for the binding</param>
        /// <returns>this</returns>
        public DescriptorSetLayoutBuilder Add(uint binding, VkDescriptorType type, uint arraySize,
            VkShaderStageFlag flags, params Sampler[] immutableSamplers)
        {
            Debug.Assert(immutableSamplers.Length == 0 || type == VkDescriptorType.CombinedImageSampler ||
                         type == VkDescriptorType.Sampler, "Can't use immutable samplers on non-sampler type");
            var samplersRewrite = immutableSamplers.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray();
            if (samplersRewrite.Length > 0)
                _pins.Add(GCHandle.Alloc(samplersRewrite, GCHandleType.Pinned));
            unsafe
            {
                _bindings.Add(binding, new VkDescriptorSetLayoutBinding()
                {
                    Binding = binding,
                    DescriptorType = type,
                    DescriptorCount = arraySize,
                    StageFlags = flags,
                    PImmutableSamplers =
                        (VkSampler*) (samplersRewrite.Length > 0
                            ? Marshal.UnsafeAddrOfPinnedArrayElement(samplersRewrite, 0)
                            : IntPtr.Zero).ToPointer()
                });
            }
            return this;
        }

        /// <summary>
        /// Adds the given single element binding descriptor
        /// </summary>
        /// <param name="binding">Binding to add</param>
        /// <param name="type">Binding type</param>
        /// <param name="flags">Shader stages</param>
        /// <param name="immutableSampler">Immutable sampler for the binding</param>
        /// <returns>this</returns>
        public DescriptorSetLayoutBuilder Add(uint binding, VkDescriptorType type, VkShaderStageFlag flags,
            Sampler immutableSampler = null)
        {
            return Add(binding, type, 1, flags, immutableSampler != null ? new[] {immutableSampler} : new Sampler[0]);
        }

        /// <summary>
        /// Builds the descriptor set layout
        /// </summary>
        /// <returns>The layout</returns>
        public DescriptorSetLayout Build()
        {
            try
            {
                var bindings = _bindings.Values.ToArray();
                unsafe
                {
                    fixed (VkDescriptorSetLayoutBinding* pBindings = bindings)
                    {
                        var info = new VkDescriptorSetLayoutCreateInfo()
                        {
                            SType = VkStructureType.DescriptorSetLayoutCreateInfo,
                            PNext = IntPtr.Zero,
                            Flags = _flags,
                            BindingCount = (uint) bindings.Length,
                            PBindings = pBindings
                        };
                        return new DescriptorSetLayout(_device, info);
                    }
                }
            }
            finally
            {
                foreach (var pin in _pins)
                    pin.Free();
                _pins.Clear();
            }
        }
    }
}