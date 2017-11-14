﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Handles
{
    public class CommandPoolCached : CommandPool
    {
        private readonly ConcurrentQueue<CommandBufferPooledExclusiveUse> _available;
        private readonly List<CommandBufferPooledExclusiveUse> _unavailable;
        private readonly uint _capacity;

        public CommandPoolCached(Device dev, uint queueFamily,
            uint maxCacheSize = 32,
            VkCommandPoolCreateFlag flags = (VkCommandPoolCreateFlag) 0) : base(dev, queueFamily, flags | VkCommandPoolCreateFlag.ResetCommandBuffer)
        {
            _capacity = maxCacheSize;
            _available = new ConcurrentQueue<CommandBufferPooledExclusiveUse>();
            _unavailable = new List<CommandBufferPooledExclusiveUse>((int) maxCacheSize);
        }

        private void RotateAvailable()
        {
            lock (_unavailable)
            {
                var mov = 0;
                for (var i = 0; i < _unavailable.Count; i++)
                {
                    if (_unavailable[i].IsSubmitted)
                    {
                        _unavailable[i - mov] = _unavailable[i];
                        continue;
                    }
                    ReturnFreeBuffer(_unavailable[i]);
                    mov++;
                }
                if (mov > 0)
                    _unavailable.RemoveRange(_unavailable.Count - mov, mov);
            }
        }

        public CommandBufferPooledExclusiveUse Borrow()
        {
            if (!_available.TryDequeue(out CommandBufferPooledExclusiveUse res))
            {
                RotateAvailable();
                if (!_available.TryDequeue(out res))
                {
                    return new CommandBufferPooledExclusiveUse(this);
                }
            }
            {
                res.Reset(VkCommandBufferResetFlag.ReleaseResources);
                return res;
            }
        }

        private void ReturnFreeBuffer(CommandBufferPooledExclusiveUse buff)
        {
            int ccap;
            lock (_unavailable)
                ccap = _available.Count + _unavailable.Count;
            if (ccap > _capacity)
            {
                buff.Dispose();
                return;
            }
            _available.Enqueue(buff);
        }

        public void Return(CommandBufferPooledExclusiveUse buff)
        {
            buff.AssertValid();
            Debug.Assert(!buff.IsBuilding);
            Debug.Assert(buff.CommandPool == this);
            if (buff.IsSubmitted)
                lock (_unavailable)
                    _unavailable.Add(buff);
            else
                ReturnFreeBuffer(buff);
        }

        protected override void Free()
        {
            while (_available.TryDequeue(out var buff))
            {
                Debug.Assert(!buff.IsSubmitted);
                buff.Dispose();
            }
            lock (_unavailable)
            {
                foreach (var k in _unavailable)
                {
                    Debug.Assert(!k.IsSubmitted);
                    k.Dispose();
                }
                _unavailable.Clear();
            }
            base.Free();
        }
    }
}