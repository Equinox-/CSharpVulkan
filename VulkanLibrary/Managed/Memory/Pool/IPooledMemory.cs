namespace VulkanLibrary.Managed.Memory.Pool
{
    /// <summary>
    /// Represents a handle to pooled memory.
    /// </summary>
    public interface IPooledMemory
    {
        /// <summary>
        /// Offset of this memory handle
        /// </summary>
        ulong Offset { get; }
        
        /// <summary>
        /// Size of this memory handle
        /// </summary>
        ulong Size { get; }
    }
}