using System;
using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class VkEnum : VkType
    {
        public readonly bool IsBitmask;
        public readonly IReadOnlyList<VkConstant> Values;
        
        public VkEnum(string typeName, bool bitmask, string comment, params VkConstant[] values) : base(typeName, comment)
        {
            IsBitmask = bitmask;
            Values = new List<VkConstant>(values);
        }

        public override string ToString()
        {
            return $"enum {TypeName} {{ {string.Join(", ", Values)} }}";
        }
    }
}