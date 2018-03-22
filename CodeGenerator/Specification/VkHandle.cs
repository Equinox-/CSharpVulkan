using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Specification
{
    public class VkHandle : VkType
    {
        public readonly IReadOnlyList<string> ParentHandles;
        public readonly bool IsDispatchable;
        
        public VkHandle(string typeName, bool isDispatchable, string comment, string parent) : base(typeName, comment)
        {
            ParentHandles = parent?.Split(',').Where(x=>!string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
            IsDispatchable = isDispatchable;
        }

        public override string ToString()
        {
            return $"{TypeName} of {string.Join(", ", ParentHandles)}";
        }
    }
}