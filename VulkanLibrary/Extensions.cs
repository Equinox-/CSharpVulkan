using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using VulkanLibrary.Unmanaged;

namespace VulkanLibrary
{
    public static class Extensions
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        private static readonly string[] SizeUnits = {"B", "KB", "MB", "GB", "TB"};

        // https://stackoverflow.com/questions/281640/how-do-i-get-a-human-readable-file-size-in-bytes-abbreviation-using-net
        // Thanks internet!
        public static string FormatFileSize(ulong size)
        {
            int order = 0;
            while (size >= 1024 && order < SizeUnits.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            return $"{size:0.##} {SizeUnits[order]}";
        }

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
        
        public static ulong GreatestCommonDenominator(ulong a, ulong b)
        {
            while (a != b)
            {
                if (a > b)
                    a = a - b;
                else
                    b = b - a;
            }
            return a;
        }

        public static ulong LeastCommonMultiple(ulong a, ulong b)
        {
            return (a * b) / GreatestCommonDenominator(a, b);
        }

        public static void DumpRecursive(this object o, string indent = "")
        {
            if (o == null)
            {
                Log.Info(indent + "null");
                return;
            }
            if (o is System.Collections.IEnumerable e)
            {
                var j = 0;
                foreach (var i in e)
                {
                    Log.Info(indent + "item[" + j + "]");
                    DumpRecursive(i, indent + "  ");
                    j++;
                }
                return;
            }
            foreach (var field in o.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var k = field.GetValue(o);
                Log.Info(indent + " " + field.FieldType.Name + " " + field.Name + " " + k);
                if (field.FieldType.IsPointer)
                    unsafe
                    {
                        DumpRecursive(
                            Marshal.PtrToStructure(new IntPtr(Pointer.Unbox(k)), field.FieldType.GetElementType()),
                            indent + "  ");
                    }
                else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                    DumpRecursive(k, indent + "  ");
            }
        }
    }
}