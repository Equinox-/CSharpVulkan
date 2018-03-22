using System;
using System.Runtime.CompilerServices;

namespace VulkanLibrary.Managed.Utilities
{
    public class ResettingEvent
    {
        private event Action _backing;

        public void Add(Action e)
        {
            lock (this)
                _backing += e;
        }

        public void Remove(Action e)
        {
            lock (this)
                _backing -= e;
        }

        public void RaiseAndReset()
        {
            Action evt;
            lock (this)
            {
                evt = _backing;
                _backing = null;
            }

            evt?.Invoke();
        }

        public void Reset()
        {
            lock (this)
                _backing = null;
        }
    }
}