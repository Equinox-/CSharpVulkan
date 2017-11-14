using System;
using System.Diagnostics;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class CommandBufferPooledExclusiveUse : CommandBuffer
    {
        private readonly Fence _fence;

        public CommandBufferPooledExclusiveUse(CommandPoolCached pool) : base(pool, VkCommandBufferLevel.Primary)
        {
            _fence = new Fence(pool.Device, 0);
        }

        internal unsafe void DoSubmit(VkQueue queue, VkSubmitInfo info)
        {
            AssertValid();
            Debug.Assert(!IsSubmitted);
            _fence.Reset();
            _state = State.Submitted;
            VkException.Check(VkQueue.vkQueueSubmit(queue, 1, &info, _fence.Handle));
            if (_return)
                ((CommandPoolCached) CommandPool).Return(this);
        }

        /// <summary>
        /// Is this queue waiting pending execution
        /// </summary>
        public bool IsSubmitted
        {
            get
            {
                base.AssertValid();
                if (_state == State.Submitted && _fence.IsSet)
                    _state = State.Built;
                return _state == State.Submitted;
            }
        }

        protected override void Free()
        {
            base.Free();
            _fence.Dispose();
        }

        private bool _return = false;

        public void ReturnOnSubmit()
        {
            AssertValid();
            _return = true;
        }
    }
}