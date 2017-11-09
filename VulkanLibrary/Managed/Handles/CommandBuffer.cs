using System.Diagnostics;
using System.Runtime.CompilerServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public partial class CommandBuffer
    {
        private enum State
        {
            Allocated,
            Building,
            Built
        }

        private State _state;

        public CommandBuffer(CommandPool pool, VkCommandBufferLevel level)
        {
            CommandPool = pool;
            unsafe
            {
                VkCommandBuffer handle = VkCommandBuffer.Null;
                VkCommandBufferAllocateInfo info = new VkCommandBufferAllocateInfo()
                {
                    SType = VkStructureType.CommandBufferAllocateInfo,
                    PNext = (void*) 0,
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

        public CommandBufferRecorder RecordCommands(VkCommandBufferUsageFlag usage,
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
                    PNext = (void*) 0,
                    Flags = usage,
                    PInheritanceInfo = inheritance.HasValue ? &inherit : (VkCommandBufferInheritanceInfo*) 0
                };
                Handle.BeginCommandBuffer(&info);
                _state = State.Building;
            }
            return new CommandBufferRecorder(this);
        }

        internal void FinishBuild()
        {
            AssertBuilding();
            Handle.EndCommandBuffer();
            _state = State.Built;
        }
    }
}