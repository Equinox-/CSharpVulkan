using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary
{
    public static class Extensions
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        public static VkClearValue Color(this VkClearValue value, VkColor color)
        {
            unsafe
            {
                value.Color.Float32[0] = color.R;
                value.Color.Float32[1] = color.G;
                value.Color.Float32[2] = color.B;
                value.Color.Float32[3] = color.A;
            }
            return value;
        }

        public static void DumpRecursive(this object o, string indent = "")
        {
            if (o == null)
            {
                Console.WriteLine(indent + "null");
                return;
            }
            if (o is System.Collections.IEnumerable e)
            {
                var j = 0;
                foreach (var i in e)
                {
                    Console.WriteLine(indent + "item[" + j + "]");
                    DumpRecursive(i, indent + "  ");
                    j++;
                }
                return;
            }
            foreach (var field in o.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var k = field.GetValue(o);
                Console.WriteLine(indent + " " + field.FieldType.Name + " " + field.Name + " " + k);
                if (field.FieldType.IsPointer)
                    unsafe
                    {
                        DumpRecursive(Marshal.PtrToStructure(new IntPtr(Pointer.Unbox(k)), field.FieldType.GetElementType()), indent + "  ");
                    }
                else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                    DumpRecursive(k, indent + "  ");
            }
        }
    }
}