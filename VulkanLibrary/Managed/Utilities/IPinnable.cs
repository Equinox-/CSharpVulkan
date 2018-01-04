using System;

namespace VulkanLibrary.Managed.Utilities
{
    public interface IPinnable
    {
        /// <summary>
        /// Increases the number of <see cref="DisposePin"/> on this handle.
        /// </summary>
        /// <remarks>
        /// Disposable used to decrease the pin count again
        /// </remarks>
        DisposePin PinUsing();

        /// <summary>
        /// Increases the number of <see cref="DisposePin"/> on this handle.
        /// </summary>
        void IncreasePins();

        /// <summary>
        /// Decreases the number of <see cref="DisposePin"/> on this handle.
        /// </summary>
        void DecreasePins();
    }
    
    public readonly struct DisposePin : IDisposable
    {
        private readonly IPinnable _handle;

        public DisposePin(IPinnable h)
        {
            h.IncreasePins();
            _handle = h;
        }

        public void Dispose()
        {
            _handle.DecreasePins();
        }
    }
}