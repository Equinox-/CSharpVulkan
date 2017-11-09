using System;
using VulkanLibrary.Managed.Utilities;

namespace VulkanLibrary.Managed.Memory.Mapped
{
    /// <summary>
    /// Represents a section of mapped memory that is a proxy to another mapped region.
    /// </summary>
    public class ProxyMemory : VulkanHandle<IntPtr>, IMappedMemory
    {
        /// <summary>
        /// Offset of this mapped memory region.
        /// </summary>
        public ulong Offset { get; }

        /// <inheritdoc />
        public ulong Size { get; }

        private readonly IMappedMemory _parent;

        internal ProxyMemory(IMappedMemory parent, ulong offset, ulong size)
        {
            Offset = offset;
            Size = size;
            _parent = parent;
            Handle = new IntPtr(parent.Handle.ToInt64() + (long) offset);
        }
        
        /// <inheritdoc />
        public void Flush()
        {
            _parent.FlushRange(Offset, Size);
        }

        /// <inheritdoc />
        public void FlushRange(ulong offset, ulong count)
        {
            _parent.FlushRange(Offset + offset, count);
        }

        /// <inheritdoc />
        public void Invalidate()
        {
            _parent.InvalidateRange(Offset, Size);
        }

        /// <inheritdoc />
        public void InvalidateRange(ulong offset, ulong count)
        {
            _parent.InvalidateRange(Offset + offset, count);
        }
        
        /// <inheritdoc />
        protected override void Free()
        {
            Handle = IntPtr.Zero;
        }

        public override void AssertValid()
        {
            base.AssertValid();
            if (_parent is VulkanHandle v)
                v.AssertValid();
        }
    }
}