using System.IO;
using System.Runtime.CompilerServices;

namespace CodeGenerator.Utils
{
    public static class TextWriterExtensions
    {
        private const string Indent = "    ";
        private static readonly ConditionalWeakTable<TextWriter, string> _indents = new ConditionalWeakTable<TextWriter, string>();

        public static void IncreaseIndent(this TextWriter w)
        {
            _indents.AddOrUpdate(w, _indents.GetValue(w, CreateValueCallback) + Indent);
        }

        public static int IndentLevel(this TextWriter w)
        {
            return _indents.GetValue(w, CreateValueCallback).Length / Indent.Length;
        }

        public static void DecreaseIndent(this TextWriter w)
        {
            var result = _indents.GetValue(w, CreateValueCallback);
            _indents.AddOrUpdate(w, result.Substring(0, result.Length - Indent.Length));
        }

        private static string CreateValueCallback(TextWriter x)
        {
            return "";
        }

        public static void WriteIndent(this TextWriter w)
        {
            w.Write(_indents.GetValue(w, CreateValueCallback));   
        }
        
        public static void WriteLineIndent(this TextWriter w, string val)
        {
            WriteIndent(w);
            w.WriteLine(val);
        }
    }
}