using System;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Threading;

namespace VulkanLibrary.Managed.Utilities
{
    /// <summary>
    /// Represents a RAII handle to a Vulkan resource.
    /// </summary>
    public abstract class VulkanHandle : CriticalFinalizerObject, IDisposable
    {
        private enum LifeState : int
        {
            Alive,
            Dying,
            Dead
        }

        /// <summary>
        /// Flag indicating that this specific resource has been freed.
        /// </summary>
        private int _disposed = (int) LifeState.Alive;

        /// <inheritdoc/>
        public void Dispose()
        {
            Debug.Assert(_disposed == (int) LifeState.Alive, $"Resource {GetType()} was already disposed");
            if (_disposed != (int) LifeState.Alive)
                return;

            // If current state is alive, set to dying and proceed.
            if (Interlocked.CompareExchange(ref _disposed, (int) LifeState.Dying, (int) LifeState.Alive) != (int) LifeState.Alive)
            {
                Debug.Fail($"Resource {GetType()} was already disposed");
                return;
            }
            Free();
            GC.SuppressFinalize(this);
            _disposed = (int) LifeState.Dead;
        }

        /// <summary>
        /// Asserts that this object and any dependencies have not been disposed.
        /// Only called when debugging.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public virtual void AssertValid()
        {
            Debug.Assert(_disposed != (int) LifeState.Dead, "Resource was used after disposal");
        }

        /// <summary>
        /// Called to free this resource.  Once this has been called it will never be called again.
        /// </summary>
        protected abstract void Free();

        ~VulkanHandle()
        {
            if (_disposed != (int) LifeState.Alive)
                return;
            
            // If current state is alive, set to dying and proceed.
            if (Interlocked.CompareExchange(ref _disposed, (int) LifeState.Dying, (int) LifeState.Alive) != (int) LifeState.Alive)
                return;
            Free();
            _disposed = (int) LifeState.Dead;
            Debug.Fail($"Resource {GetType()} was leaked");
        }
    }
    
    /// <inheritdoc />
    /// <typeparam name="T">Type of native handle</typeparam>
    public abstract class VulkanHandle<T> : VulkanHandle
    {
        /// <summary>
        /// The native resource handle for this resource.
        /// </summary>
        public T Handle { get; protected set; }
    }
}