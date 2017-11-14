using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using VulkanLibrary.Managed.Handles;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary.Managed.Memory
{
    /// <summary>
    /// Represents a constraint for a requirement.
    /// </summary>
    public enum MemoryRequirementLevel
    {
        None,
        Preferred,
        Required
    }

    /// <summary>
    /// Represents a requirement with an enforcement level.
    /// </summary>
    /// <typeparam name="T">Type of requirement</typeparam>
    public struct MemoryRequirementFiltered<T>
    {
        /// <summary>
        /// Collection of two requirement levels.
        /// </summary>
        public static readonly IReadOnlyList<MemoryRequirementLevel> PreferredThenRequired =
            new List<MemoryRequirementLevel>() {MemoryRequirementLevel.Preferred, MemoryRequirementLevel.Required};

        /// <summary>
        /// Actual value of this requirement.
        /// </summary>
        public T Value;

        /// <summary>
        /// Enforcement level for this requirement
        /// </summary>
        public MemoryRequirementLevel RequirementLevel
        {
            get => _level;
            set
            {
                _level = value;
                if (_level == MemoryRequirementLevel.None)
                    Value = default(T);
            }
        }

        private MemoryRequirementLevel _level;
    }

    /// <summary>
    /// Represents a set of requirements for a memory allocation.
    /// </summary>
    public struct MemoryRequirements
    {
        ///<summary>
        /// Raw requirements for allocation
        /// </summary>
        public VkMemoryRequirements TypeRequirements;

        ///<summary>
        /// A set of flags the allocation is prefers to have.
        /// </summary>
        public VkMemoryPropertyFlag PreferredFlags;

        ///<summary>
        /// A set of flags the allocation must have.
        /// </summary>
        public VkMemoryPropertyFlag RequiredFlags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryRequirementLevel GetFlag(VkMemoryPropertyFlag flag)
        {
            if ((RequiredFlags & flag) != 0)
                return MemoryRequirementLevel.Required;
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if ((PreferredFlags & flag) != 0)
                return MemoryRequirementLevel.Preferred;
            return MemoryRequirementLevel.None;
        }

        private void SetFlag(VkMemoryPropertyFlag flag, MemoryRequirementLevel level)
        {
            if (level < MemoryRequirementLevel.Required)
                RequiredFlags &= ~flag;
            else
                RequiredFlags |= flag;
            if (level < MemoryRequirementLevel.Preferred)
                PreferredFlags &= ~flag;
            else
                PreferredFlags |= flag;
        }

        /// <see cref="VkMemoryPropertyFlag.DeviceLocal"/>
        public MemoryRequirementLevel DeviceLocal
        {
            get => GetFlag(VkMemoryPropertyFlag.DeviceLocal);
            set => SetFlag(VkMemoryPropertyFlag.DeviceLocal, value);
        }

        /// <see cref="VkMemoryPropertyFlag.HostVisible"/>
        public MemoryRequirementLevel HostVisible
        {
            get => GetFlag(VkMemoryPropertyFlag.HostVisible);
            set => SetFlag(VkMemoryPropertyFlag.HostVisible, value);
        }

        /// <see cref="VkMemoryPropertyFlag.HostCoherent"/>
        public MemoryRequirementLevel HostCoherent
        {
            get => GetFlag(VkMemoryPropertyFlag.HostCoherent);
            set => SetFlag(VkMemoryPropertyFlag.HostCoherent, value);
        }

        /// <see cref="VkMemoryPropertyFlag.HostCached"/>
        public MemoryRequirementLevel HostCached
        {
            get => GetFlag(VkMemoryPropertyFlag.HostCached);
            set => SetFlag(VkMemoryPropertyFlag.HostCached, value);
        }

        /// <see cref="VkMemoryPropertyFlag.LazilyAllocated"/>
        public MemoryRequirementLevel LazilyAllocated
        {
            get => GetFlag(VkMemoryPropertyFlag.LazilyAllocated);
            set => SetFlag(VkMemoryPropertyFlag.LazilyAllocated, value);
        }

        /// <summary>
        /// Constraint on if this memory should be dedicated.
        /// </summary>
        public MemoryRequirementFiltered<IDedicatedMemoryOwner> DedicatedMemory;

        /// <summary>
        /// Returns the union of these requirements
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static MemoryRequirements Union(MemoryRequirements a, MemoryRequirements b)
        {
            return new MemoryRequirements()
            {
                TypeRequirements = new VkMemoryRequirements()
                {
                    MemoryTypeBits = a.TypeRequirements.MemoryTypeBits & b.TypeRequirements.MemoryTypeBits,
                    Alignment = Math.Max(a.TypeRequirements.Alignment, b.TypeRequirements.Alignment),
                    Size = Math.Max(a.TypeRequirements.Size, b.TypeRequirements.Size)
                },
                PreferredFlags = a.PreferredFlags | b.PreferredFlags,
                RequiredFlags = a.RequiredFlags | b.RequiredFlags,
                DedicatedMemory = a.DedicatedMemory.RequirementLevel > b.DedicatedMemory.RequirementLevel
                    ? a.DedicatedMemory
                    : b.DedicatedMemory
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MemoryType FindMemoryTypeWithFlags(MemoryRequirements req, VkMemoryPropertyFlag desiredFlags,
            IReadOnlyList<MemoryType> memoryTypes)
        {
            foreach (var type in memoryTypes)
            {
                if (req.TypeRequirements.MemoryTypeBits != 0 &&
                    (req.TypeRequirements.MemoryTypeBits & (1 << (int) type.TypeIndex)) == 0)
                    continue;
                if ((type.Flags & desiredFlags) != desiredFlags)
                    continue;
                return type;
            }
            return null;
        }

        [Pure]
        public MemoryType FindMemoryType(PhysicalDevice physicalDevice)
        {
            return FindMemoryTypeWithFlags(this, this.PreferredFlags | this.RequiredFlags,
                       physicalDevice.MemoryTypes) ??
                   FindMemoryTypeWithFlags(this, this.RequiredFlags, physicalDevice.MemoryTypes);
        }
    }
}