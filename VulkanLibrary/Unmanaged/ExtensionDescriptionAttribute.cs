using System;
using System.Collections.Generic;

namespace VulkanLibrary.Unmanaged
{
    public class ExtensionDescriptionAttribute : Attribute
    {
        /// <summary>
        /// The ID of this extension.  Auto set by <see cref="VkExtensionDatabase"/>
        /// </summary>
        public VkExtension ExtensionId { get; internal set; }
        
        /// <summary>
        /// Required extension for this entry
        /// </summary>
        public string Extension { get; }
        
        /// <summary>
        /// Extension number
        /// </summary>
        public int Number { get; }
        
        /// <summary>
        /// Required extensions
        /// </summary>
        public IReadOnlyList<VkExtension> Requires { get; }

        /// <summary>
        /// The specification version
        /// </summary>
        public long Version { get; }
        
        /// <summary>
        /// Gets the type of handle the extension applies to
        /// </summary>
        public ExtensionType Type { get; }
        
        internal ExtensionDescriptionAttribute(string ext, int number, ExtensionType type, long version, params VkExtension[] requires)
        {
            Extension = ext;
            Type = type;
            Number = number;
            Version = version;
            Requires = new List<VkExtension>(requires);
        }
    }
}