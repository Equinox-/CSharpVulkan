using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class PipelineLayoutBuilder
    {
        private readonly Device _dev;
        private readonly List<DescriptorSetLayout> _setLayouts = new List<DescriptorSetLayout>();
        private readonly List<VkPushConstantRange> _pushConstants = new List<VkPushConstantRange>();

        public PipelineLayoutBuilder(Device dev)
        {
            _dev = dev;
        }

        public PipelineLayoutBuilder With(params DescriptorSetLayout[] sets)
        {
            _setLayouts.AddRange(sets);
            return this;
        }

        public PipelineLayoutBuilder WithPushConstant(VkShaderStageFlag flags, uint offset, uint size)
        {
            _pushConstants.Add(new VkPushConstantRange()
            {
                StageFlags =  flags,
                Offset = offset,
                Size = size
            });
            return this;
        }

        private static IntPtr PinnedArrayAddr<T>(T[] array)
        {
            return array.Length > 0 ? Marshal.UnsafeAddrOfPinnedArrayElement(array, 0) : IntPtr.Zero;
        }

        public PipelineLayout Build()
        {
            var push = _pushConstants.ToArray();
            var layouts = _setLayouts.Select(x =>
            {
                x.AssertValid();
                return x.Handle;
            }).ToArray();

            var pinPush = push.Length > 0 ? GCHandle.Alloc(push, GCHandleType.Pinned) : default(GCHandle);
            var pinLayouts = layouts.Length > 0 ? GCHandle.Alloc(layouts, GCHandleType.Pinned) : default(GCHandle);
            
            try
            {
                unsafe
                {
                    return new PipelineLayout(_dev, new VkPipelineLayoutCreateInfo()
                    {
                        SType = VkStructureType.PipelineLayoutCreateInfo,
                        Flags = 0,
                        PNext = IntPtr.Zero,
                        SetLayoutCount = (uint) layouts.Length,
                        PSetLayouts = (VkDescriptorSetLayout*) PinnedArrayAddr(layouts).ToPointer(),
                        PushConstantRangeCount = (uint) push.Length,
                        PPushConstantRanges = (VkPushConstantRange*) PinnedArrayAddr(push).ToPointer()
                    });
                }
            }
            finally
            {
                if (push.Length > 0)
                    pinPush.Free();
                if (layouts.Length > 0)
                    pinLayouts.Free();
            }
        }
    }
}