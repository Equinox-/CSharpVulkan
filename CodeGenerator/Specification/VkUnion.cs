using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class VkUnion : VkStructOrUnion
    {
        public VkUnion(string typeName, string comment, params VkMember[] members) : base(typeName, comment, members)
        {
        }
    }
}