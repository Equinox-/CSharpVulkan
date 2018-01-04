using System;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Utilities
{
    /// <summary>
    /// Represents a RAII handle to a Vulkan resource.
    /// </summary>
    public abstract class VulkanHandle : CriticalFinalizerObject, IDisposable, IPinnable
    {
        private enum LifeState : int
        {
            Alive,
            Dying,
            Dead
        }


        protected VulkanHandle()
        {
            IncreasePins();
        }

        private int _pins;

        /// <inheritdoc />
        public DisposePin PinUsing()
        {
            return new DisposePin(this);
        }

        /// <inheritdoc />
        public void IncreasePins()
        {
            Interlocked.Increment(ref _pins);
        }

        /// <inheritdoc />
        public void DecreasePins()
        {
            if (Interlocked.Decrement(ref _pins) == 0)
                ForceDispose(true);
        }

        /// <summary>
        /// Flag indicating that this specific resource has been freed.
        /// </summary>
        private int _disposed = (int) LifeState.Alive;

        /// <summary>
        /// Is this handle allocated
        /// </summary>
        public bool IsAllocated => _disposed == (int) LifeState.Alive;

        private int _hasUserDisposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            _hasUserDisposed = 1;
            if (Interlocked.Decrement(ref _pins) == 0)
                ForceDispose();
        }

        private void ForceDispose(bool supressErrors = false)
        {
            Debug.Assert(_hasUserDisposed != 0,
                "Destroying object the user didn't mark as disposed.  Someone unpinned twice.");
            Debug.Assert(supressErrors || _disposed == (int) LifeState.Alive,
                $"Resource {GetType()} was already disposed");
            if (_disposed != (int) LifeState.Alive)
                return;

            // If current state is alive, set to dying and proceed.
            if (Interlocked.CompareExchange(ref _disposed, (int) LifeState.Dying, (int) LifeState.Alive) !=
                (int) LifeState.Alive)
            {
                if (!supressErrors)
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
            if (Interlocked.CompareExchange(ref _disposed, (int) LifeState.Dying, (int) LifeState.Alive) !=
                (int) LifeState.Alive)
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