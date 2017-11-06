using System.Collections.Generic;

namespace VulkanLibrary
{
    /// <summary>
    /// Represents a fixed array
    /// </summary>
    /// <typeparam name="T">Array type</typeparam>
    public interface IFixedArray<T> : IEnumerable<T>, System.Collections.IEnumerable, IReadOnlyCollection<T>
    {
        /// <summary>
        /// Gets the item at index i
        /// </summary>
        /// <param name="i">index</param>
        T this[int i] { get; set; }
    }
}