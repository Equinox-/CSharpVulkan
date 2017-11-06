using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class VkStructOrUnion : VkType
    {
        public readonly IReadOnlyList<VkMember> Members;
        
        public VkStructOrUnion(string typeName, string comment, params VkMember[] members) : base(typeName, comment)
        {
            Members = new List<VkMember>(members);
        }

        public override string ToString()
        {
            return $"{GetType().Name} {TypeName} {{ {string.Join(", ", Members)} }}";
        }
    }
}