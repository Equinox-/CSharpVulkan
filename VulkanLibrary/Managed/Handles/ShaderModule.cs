using System;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class ShaderModule
    {
        public ShaderModule(Device dev, byte[] code)
        {
            Device = dev;
            unsafe
            {
                fixed (byte* pCode = &code[0])
                {
                    var info = new VkShaderModuleCreateInfo()
                    {
                        SType = VkStructureType.ShaderModuleCreateInfo,
                        Flags = 0,
                        CodeSize = new IntPtr(code.LongLength),
                        PCode = (uint*) pCode,
                        PNext = (void*)0
                    };
                    Handle =  Device.Handle.CreateShaderModule(&info, Instance.AllocationCallbacks);
                }
            }
        }
    }
}