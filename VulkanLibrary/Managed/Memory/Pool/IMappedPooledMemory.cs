using VulkanLibrary.Managed.Memory.Mapped;

namespace VulkanLibrary.Managed.Memory.Pool
{
    /// <summary>
    /// Pooled memory that is mapped.
    /// </summary>
    /// <inheritdoc cref="IPooledMemory"/>
    public interface IMappedPooledMemory : IPooledMemory
    {
        /// <summary>
        /// The mapped memory associated with this pooled memory.
        /// </summary>
        IMappedMemory MappedMemory { get; }
    }
}