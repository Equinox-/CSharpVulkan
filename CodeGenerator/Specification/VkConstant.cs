using System;

namespace CodeGenerator.Specification
{
    public class VkConstant
    {
        public readonly string Name;
        public readonly string Comment;
        public readonly string BitPositionExpression;
        public readonly string Expression;
        public VkExtension Extension = null;

        private VkConstant(string name, string comment, string expression, string bitPositionExpression)
        {
            Name = name;
            Comment = comment;
            Expression = expression;
            BitPositionExpression = bitPositionExpression;
        }

        public static VkConstant AsBitPosition(string name, string summary, string bitExpression)
        {
            return new VkConstant(name, summary, $"1 << ({bitExpression})", bitExpression);
        }

        public static VkConstant AsValue(string name, string comment, string expression)
        {
            return new VkConstant(name, comment, expression, null);
        }

        public override string ToString()
        {
            return $"{Name} = {Expression}";
        }
    }
}