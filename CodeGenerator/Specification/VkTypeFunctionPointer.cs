using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class VkTypeFunctionPointer : VkTypeFunction
    {
        public VkTypeFunctionPointer(string typeName, VkMember returnType, string comment, params VkMember[] arguments) : base(typeName, returnType, comment, arguments)
        {
        }

        public VkTypeFunctionPointer(string typeName, VkMember returnType, string comment, IEnumerable<string> errors, IEnumerable<string> success, params VkMember[] arguments) : base(typeName, returnType, comment, errors, success, arguments)
        {
        }
    }
}