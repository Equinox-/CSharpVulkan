using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class CommandBuffer
    {
        protected enum State
        {
            Allocated,
            Building,
            Built,
            Submitted
        }

        protected State _state;

        public CommandBuffer(CommandPool pool, VkCommandBufferLevel level)
        {
            CommandPool = pool;
            unsafe
            {
                VkCommandBuffer handle = VkCommandBuffer.Null;
                VkCommandBufferAllocateInfo info = new VkCommandBufferAllocateInfo()
                {
                    SType = VkStructureType.CommandBufferAllocateInfo,
                    PNext = IntPtr.Zero,
                    CommandPool = pool.Handle,
                    CommandBufferCount = 1,
                    Level = level
                };
                VkException.Check(VkDevice.vkAllocateCommandBuffers(Device.Handle, &info, &handle));
                Debug.Assert(handle != VkCommandBuffer.Null);
                Handle = handle;
                _state = State.Allocated;
            }
        }

        public bool IsBuilt => _state == State.Built;
        public bool IsBuilding => _state == State.Building;

        /// <summary>
        /// Resets this command buffer
        /// </summary>
        /// <param name="flag">reset flag</param>
        public virtual void Reset(VkCommandBufferResetFlag flag)
        {
            AssertValid();
            Handle.ResetCommandBuffer(flag);
            _state = State.Allocated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssertBuilding()
        {
            AssertValid();
            Debug.Assert(_state == State.Building, "Buffer is not being built");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssertBuilt()
        {
            AssertValid();
            Debug.Assert(_state == State.Built, "Buffer hasn't been built");
        }

        protected void BeginRecording(VkCommandBufferUsageFlag usage,
            VkCommandBufferInheritanceInfo? inheritance = null)
        {
            AssertValid();
            Debug.Assert(_state == State.Allocated);
            unsafe
            {
                var inherit = inheritance ?? default(VkCommandBufferInheritanceInfo);
                var info = new VkCommandBufferBeginInfo()
                {
                    SType = VkStructureType.CommandBufferBeginInfo,
                    PNext = IntPtr.Zero,
                    Flags = usage,
                    PInheritanceInfo = inheritance.HasValue ? &inherit : (VkCommandBufferInheritanceInfo*) 0
                };
                Handle.BeginCommandBuffer(&info);
                _state = State.Building;
            }
        }

        public CommandBufferRecorder<CommandBuffer> RecordCommands(VkCommandBufferUsageFlag usage,
            VkCommandBufferInheritanceInfo? inheritance = null)
        {
            BeginRecording(usage, inheritance);
            return new CommandBufferRecorder<CommandBuffer>(this);
        }

        internal void FinishBuild()
        {
            AssertBuilding();
            Handle.EndCommandBuffer();
            _state = State.Built;
        }
    }
}