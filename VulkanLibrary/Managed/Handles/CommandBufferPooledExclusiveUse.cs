using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class CommandBufferPooledExclusiveUse : CommandBuffer
    {
        private readonly Fence _fence;

        /// <summary>
        /// Event raised once the contents of this buffer are executed.  Reset after raising.
        /// </summary>
        /// <remarks>
        /// This event can be delayed by up to <see cref="CommandPoolCached.ForceRotateTimeMs"/>.
        /// </remarks>
        public event Action SubmissionFinished;

        public CommandBufferPooledExclusiveUse(CommandPoolCached pool) : base(pool, VkCommandBufferLevel.Primary)
        {
            Logging.Allocations?.Trace($"Creating new pooled command buffer for {pool.GetHashCode():X}");
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
                if (_state != State.Submitted || !_fence.IsSet)
                    return _state == State.Submitted;
                SubmissionFinished?.Invoke();
                SubmissionFinished = null;
                _state = State.Built;
                return false;
            }
        }

        /// <inheritdoc/>
        public override void Reset(VkCommandBufferResetFlag flag)
        {
            base.Reset(flag);
            SubmissionFinished = null;
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