using VulkanLibrary.Managed.Handles;

namespace VulkanLibrary.Managed.Memory.Pool
{
    /// <summary>
    /// Represents pooled memory from a vulkan device.
    /// </summary>
    public interface IPooledDeviceMemory : IPooledMemory
    {
        /// <summary>
        /// Gets the memory resource backing this pooled memory.
        /// </summary>
        DeviceMemory BackingMemory { get; }
    }
}