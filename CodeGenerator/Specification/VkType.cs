namespace CodeGenerator.Specification
{
    public abstract class VkType
    {
        public readonly string TypeName;
        public string Comment;

        public VkType(string typeName, string comment)
        {
            TypeName = typeName;
            Comment = comment;
        }
    }
}