using System.Collections.Generic;

namespace CodeGenerator.Specification
{
    public class Specification
    {
        public string FileHeader;
        public readonly Dictionary<string, VkType> TypeDefs = new Dictionary<string, VkType>();
        public readonly Dictionary<string, VkConstant> Constants = new Dictionary<string, VkConstant>();
        public readonly Dictionary<string, VkExtension> Extensions = new Dictionary<string, VkExtension>();

        public Specification()
        {
        }

        public void Add(VkType type)
        {
            TypeDefs.Add(type.TypeName, type);
        }

        public void Add(VkConstant constant)
        {
            Constants.Add(constant.Name, constant);
        }

        public void Add(VkExtension ext)
        {
            Extensions.Add(ext.Name, ext);
            foreach (var type in ext.ProvidedConstants)
                type.Extension = ext;
            foreach (var type in ext.ProvidedTypes)
                type.Extension = ext;
        }
    }
}