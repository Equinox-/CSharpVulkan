using System;
using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class VkStruct : VkStructOrUnion
    {
        public VkStruct(string typeName, string comment, params VkMember[] members) : base(typeName, comment, members)
        {
        }
    }
}