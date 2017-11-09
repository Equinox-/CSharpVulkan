using System;

namespace VulkanLibrary.Unmanaged
{
    /// <summary>
    /// Indicates that this feature requires the given extension
    /// </summary>
    public class ExtensionRequiredAttribute : Attribute
    {
        /// <summary>
        /// Required extension for this entry
        /// </summary>
        public VkExtension Extension { get; }

        internal ExtensionRequiredAttribute(VkExtension ext)
        {
            Extension = ext;
        }
    }
}