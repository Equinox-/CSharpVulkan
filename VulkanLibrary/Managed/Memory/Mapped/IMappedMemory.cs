using System;

namespace VulkanLibrary.Managed.Memory.Mapped
{
    /// <summary>
    /// Represents a section of mapped memory
    /// </summary>
    public interface IMappedMemory : IDisposable
    {
        /// <summary>
        /// Gets the size of this mapped memory section
        /// </summary>
        ulong Size { get; }
        
        /// <summary>
        /// Pointer to the first byte in this mapped memory range.
        /// </summary>
        IntPtr Handle { get; }
        
        /// <summary>
        /// Flushes the entire mapped region.
        /// </summary>
        void Flush();

        /// <summary>
        /// Flushes part of the mapped region.
        /// </summary>
        /// <param name="offset">Start of flushed range</param>
        /// <param name="count">Size of flushed range</param>
        void FlushRange(ulong offset, ulong count);

        /// <summary>
        /// Invalidates the entire mapped region.
        /// </summary>
        void Invalidate();

        /// <summary>
        /// Invalidates part of the mapped region.
        /// </summary>
        /// <param name="offset">Start of invalidated range</param>
        /// <param name="count">Size of invalidated range</param>
        void InvalidateRange(ulong offset, ulong count);
    }
}