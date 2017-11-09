using System;

namespace VulkanLibrary
{
    public struct VkBool32 : IEquatable<VkBool32>, IEquatable<bool>
    {
        private readonly uint _value;

        public VkBool32(bool val)
        {
            _value = val ? 1u : 0;
        }
        
        public bool Equals(VkBool32 other)
        {
            return _value == other._value;
        }

        public bool Equals(bool other)
        {
            return (_value != 0) == other;
        }

        public override bool Equals(object obj)
        {
            return (obj is VkBool32 bool32 && Equals(bool32)) ||
                    (obj is bool @bool && Equals(@bool));
        }

        public override int GetHashCode()
        {
            return (_value != 0 ? 1 : 0);
        }
        
        public static implicit operator bool(VkBool32 d)
        {
            return d._value != 0;
        }
        public static implicit operator VkBool32(bool d)
        {
            return new VkBool32(d);
        }
    }
}