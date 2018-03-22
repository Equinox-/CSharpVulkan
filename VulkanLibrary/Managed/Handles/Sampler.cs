using System;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Sampler
    {
        public Sampler(Device device) : this(device, new VkSamplerCreateInfo()
        {
            SType = VkStructureType.SamplerCreateInfo,
            Flags = 0,
            PNext = IntPtr.Zero,
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.Repeat,
            AddressModeV = VkSamplerAddressMode.Repeat,
            AddressModeW = VkSamplerAddressMode.Repeat,
            AnisotropyEnable = device.Features.SamplerAnisotropy,
            MaxAnisotropy = device.Features.SamplerAnisotropy ? 16 : 1,
            CompareEnable = false,
            UnnormalizedCoordinates = false
        })
        {
        }

        public Sampler(Device dev, VkSamplerCreateInfo info)
        {
            Device = dev;
            unsafe
            {
                Handle = Device.Handle.CreateSampler(&info, Instance.AllocationCallbacks);
            }
        }
    }
}