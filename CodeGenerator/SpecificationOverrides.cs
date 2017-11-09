using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Specification;

namespace CodeGenerator
{
    public static class SpecificationOverrides
    {
        private static readonly string[,] ExtraParents =
        {
            {"VkDisplayKHR", "VkPhysicalDevice"},
            {"VkSwapchainKHR", "VkDevice"}
        };

        public static void OverrideSpecification(Specification.Specification spec)
        {
            for (var i = 0; i < ExtraParents.GetLength(0); i++)
                if (spec.TypeDefs.TryGetValue(ExtraParents[i, 0], out var type) && type is VkHandle h &&
                    !h.ParentHandles.Contains(ExtraParents[i, 1]))
                    ((IList<string>) h.ParentHandles).Add(ExtraParents[i, 1]);

            foreach (var alias in spec.TypeDefs.Values.OfType<VkTypeAlias>())
            {
                if (alias.TypeName.Contains("Flag") &&
                    (spec.TypeDefs.TryGetValue(alias.TypeName.Replace("Flags", "Bits"), out VkType mapped) ||
                     spec.TypeDefs.TryGetValue(alias.TypeName.Replace("Flags", "FlagBits"), out mapped) ||
                     spec.TypeDefs.TryGetValue(alias.TypeName.Replace("Flag", "Bits"), out mapped) ||
                     spec.TypeDefs.TryGetValue(alias.TypeName.Replace("Flag", "FlagBits"), out mapped))
                    && mapped is VkEnum @enum)
                {
                    @enum.BackingType = alias.ActualType;
                    alias.ActualType = @enum.TypeName;
                }
            }
        }
    }
}