using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Buffers
{
    public interface IBindableBuffer
    {
        Buffer BindingHandle { get; }
        ulong Offset { get; }
    }
}