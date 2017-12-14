using System;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class Sampler
    {
        public Sampler(Device device)
        {
            Device = device;
            unsafe
            {
                var info = new VkSamplerCreateInfo()
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
                    AnisotropyEnable = Device.Features.SamplerAnisotropy,
                    MaxAnisotropy = Device.Features.SamplerAnisotropy ? 16 : 1,
                    CompareEnable = false,
                    UnnormalizedCoordinates = false
                };
                Handle = Device.Handle.CreateSampler(&info, Instance.AllocationCallbacks);
            }
        }
    }
}