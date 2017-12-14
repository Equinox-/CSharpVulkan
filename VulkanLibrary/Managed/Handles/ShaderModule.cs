using System;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public partial class ShaderModule
    {
        public ShaderModule(Device dev, ReadOnlySpan<byte> code)
        {
            Device = dev;
            unsafe
            {
                fixed (byte* pCode = &code.DangerousGetPinnableReference())
                {
                    var info = new VkShaderModuleCreateInfo()
                    {
                        SType = VkStructureType.ShaderModuleCreateInfo,
                        Flags = 0,
                        CodeSize = new IntPtr(code.Length),
                        PCode = (uint*) pCode,
                        PNext = IntPtr.Zero
                    };
                    Handle =  Device.Handle.CreateShaderModule(&info, Instance.AllocationCallbacks);
                }
            }
        }
    }
}