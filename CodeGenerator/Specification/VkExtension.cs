using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class VkExtension
    {
        public string Name;
        public readonly IReadOnlyList<string> Required;
        public readonly int Number;
        public readonly HashSet<VkConstant> ProvidedConstants = new HashSet<VkConstant>();
        public readonly HashSet<VkType> ProvidedTypes = new HashSet<VkType>();
        public string Comment;
        public readonly string Type;
        public long Version;

        public VkExtension(string name, string type, string comment, int number, params string[] required)
        {
            Type = type;
            Comment = comment;
            Name = name;
            Number = number;
            Required = new List<string>(required);
        }
    }
}