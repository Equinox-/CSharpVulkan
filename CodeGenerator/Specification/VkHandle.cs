namespace CodeGenerator.Specification
{
    public class VkHandle : VkType
    {
        public readonly string ParentHandle;
        public readonly bool IsDispatchable;
        
        public VkHandle(string typeName, bool isDispatchable, string comment, string parent) : base(typeName, comment)
        {
            ParentHandle = parent;
            IsDispatchable = IsDispatchable;
        }

        public override string ToString()
        {
            return $"{TypeName} of {ParentHandle}";
        }
    }
}