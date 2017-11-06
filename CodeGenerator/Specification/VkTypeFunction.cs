using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public abstract class VkTypeFunction : VkType
    {
        public readonly VkMember ReturnType;
        public readonly IReadOnlyList<VkMember> Arguments;
        public readonly IReadOnlyList<string> ErrorCodes, SuccessCodes;

        public VkTypeFunction(string typeName, VkMember returnType, string comment, params VkMember[] arguments) : base(
            typeName, comment)
        {
            ReturnType = returnType;
            Arguments = new List<VkMember>(arguments);
            ErrorCodes = null;
            SuccessCodes = null;
        }

        public VkTypeFunction(string typeName, VkMember returnType, string comment, IEnumerable<string> errors,
            IEnumerable<string> success, params VkMember[] arguments) : base(typeName, comment)
        {
            ReturnType = returnType;
            Arguments = new List<VkMember>(arguments);
            ErrorCodes = errors != null ? new List<string>(errors) : null;
            SuccessCodes = success != null ? new List<string>(success) : null;
        }

        public override string ToString()
        {
            return $"{TypeName}({string.Join(", ", Arguments)}) -> {ReturnType}";
        }
    }
}