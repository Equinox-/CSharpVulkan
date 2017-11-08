namespace CodeGenerator.Specification
{
    public class VkTypeAlias : VkType
    {
        public string ActualType;
        
        public VkTypeAlias(string typeName, string actualType, string comment) : base(typeName, comment)
        {
            ActualType = actualType;
        }

        public override string ToString()
        {
            return $"typedef {ActualType} as {TypeName}";
        }
    }
}