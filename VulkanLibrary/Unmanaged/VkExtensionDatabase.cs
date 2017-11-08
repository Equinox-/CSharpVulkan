using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace VulkanLibrary.Unmanaged
{
    public static class VkExtensionDatabase
    {
        private static readonly ExtensionDescriptionAttribute[] _defById;

        private static readonly IReadOnlyDictionary<string, ExtensionDescriptionAttribute> _defByName;

        static VkExtensionDatabase()
        {
            var defByName = new Dictionary<string, ExtensionDescriptionAttribute>();
            var array = (VkExtension[]) Enum.GetValues(typeof(VkExtension));
            var maxId = 0;
            foreach (var extI in array)
            {
                var def = typeof(VkExtension).GetMember(extI.ToString())[0]
                    .GetCustomAttribute<ExtensionDescriptionAttribute>();
                Debug.Assert(def.Number == (int) extI);
                def.ExtensionId = extI;
                defByName.Add(def.Extension, def);
                maxId = Math.Max(def.Number + 1, maxId);
            }
            _defById = new ExtensionDescriptionAttribute[maxId];
            foreach (var def in defByName.Values)
                _defById[def.Number] = def;
            _defByName = defByName;
        }

        public static ExtensionDescriptionAttribute Extension(VkExtension id)
        {
            return _defById[(int) id];
        }

        public static ExtensionDescriptionAttribute Extension(string id)
        {
            return _defByName.TryGetValue(id, out var res) ? res : null;
        }
    }
}