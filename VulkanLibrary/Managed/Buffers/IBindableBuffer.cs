using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Buffers
{
    public interface IBindableBuffer
    {
        VkBuffer BindingHandle { get; }
        ulong Offset { get; }
    }
}