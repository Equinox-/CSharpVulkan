using System.Collections.Generic;
using System.Text;

namespace CodeGenerator.Specification
{
    public class VkMember
    {
        // const TypeName[* by PointerLevels][FixedBufferSize] Name
        public readonly string Name;

        public string Comment;
        public readonly string TypeName;
        public readonly int PointerLevels;
        public readonly IReadOnlyList<string> AnnotatedPointerLengths;
        public readonly IReadOnlyList<string> PossibleValueExpressions;
        public readonly string FixedBufferSize;
        public readonly bool Constant, Optional;

        public VkMember(string name, string comment, string typeName, int pointers, IReadOnlyCollection<string> valueExpressions, 
            IReadOnlyCollection<string> annotatedPointerLengths,
            string fixedBufferSize, bool constant, bool optional)
        {
            PointerLevels = pointers;
            if (valueExpressions != null && valueExpressions.Count > 0)
                PossibleValueExpressions = new List<string>(valueExpressions);
            else
                PossibleValueExpressions = null;
            if (annotatedPointerLengths != null && annotatedPointerLengths.Count > 0)
                AnnotatedPointerLengths = new List<string>(annotatedPointerLengths);
            else
                AnnotatedPointerLengths = null;
            Comment = comment;
            Name = name;
            TypeName = typeName;
            FixedBufferSize = fixedBufferSize;
            Constant = constant;
            Optional = optional;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Constant)
                sb.Append("const ");
            if (Optional)
                sb.Append("opt ");
            sb.Append(TypeName);
            sb.Append('*', PointerLevels);
            if (FixedBufferSize != null)
                sb.Append('[').Append(FixedBufferSize).Append(']');
            sb.Append(' ');
            sb.Append(Name);
            return sb.ToString();
        }
    }
}