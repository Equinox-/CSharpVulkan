using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using CodeGenerator.Specification;
using CodeGenerator.Utils;

namespace CodeGenerator
{
    public class OutputGenerator : IDisposable
    {
        private const string VulkanLibraryName = "vulkan-1.dll";

        private static readonly HashSet<string> ReservedKeywords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "generic",
            "generic",
            "goto",
            "if",
            "implicit",
            "in",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "modifier",
            "modifier",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

        private static readonly HashSet<string> FixedTypes = new HashSet<string>()
        {
            "bool",
            "byte",
            "short",
            "int",
            "long",
            "char",
            "sbyte",
            "ushort",
            "uint",
            "ulong",
            "float",
            "double"
        };

        private static readonly Dictionary<string, VkType> CSharpTypes = new Dictionary<string, VkType>();

        private static void PrimitiveType(string type)
        {
            CSharpTypes.Add(type, new VkPrimitiveType(type, null));
        }

        private static void AliasType(string input, string output)
        {
            if (!CSharpTypes.ContainsKey(output))
                PrimitiveType(output);
            CSharpTypes.Add(input, new VkTypeAlias(input, output, null));
        }

        static OutputGenerator()
        {
            PrimitiveType("float");
            PrimitiveType("double");
            PrimitiveType("void");
            PrimitiveType("VkBool32");
            // C++ char is C# byte
            AliasType("char", "byte");
            AliasType("uint8_t", "byte");
            AliasType("uint16_t", "ushort");
            AliasType("uint32_t", "uint");
            AliasType("uint64_t", "ulong");
            AliasType("int8_t", "sbyte");
            AliasType("int16_t", "short");
            AliasType("int32_t", "int");
            AliasType("int64_t", "long");
            AliasType("size_t", typeof(IntPtr).Name);
        }

        private readonly string _outputPath, _namespace;
        private readonly Dictionary<string, TextWriter> _writers;
        private readonly Specification.Specification _spec;

        public OutputGenerator(Specification.Specification spec, string outputPath, string @namespace)
        {
            _spec = spec;
            _writers = new Dictionary<string, TextWriter>();
            _outputPath = outputPath;
            _namespace = @namespace;
        }

        private const string MiscFile = "Vulkan.cs";
        private const string VkResultType = "VkResult";

        private readonly string[] NamespaceSuffixes = {"", ".Handles"};

        private TextWriter GetTextWriter(string fileName, Action<TextWriter> extra = null)
        {
            if (_writers.TryGetValue(fileName, out TextWriter writer)) return writer;
            var path = Path.Combine(_outputPath, fileName);
            {
                var j = 0;
                while (true)
                {
                    var end = path.IndexOfAny(new[] {'/', '\\'}, j);
                    if (end == -1)
                        break;
                    var dir = path.Substring(0, end);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    j = end + 1;
                }
            }
            _writers.Add(fileName, writer = File.CreateText(path));
            var ns = _namespace;
            {
                var split = fileName.Split('\\', '/');
                for (var j = 0; j < split.Length - 1; j++)
                    if (!string.IsNullOrWhiteSpace(split[j]))
                        ns = ns + "." + split[j];
            }
            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using System.Runtime.InteropServices;");
            writer.WriteLine("using System.Diagnostics;");
            foreach (var nssuffix in NamespaceSuffixes)
            {
                var nss = _namespace + nssuffix;
                if (ns != nss)
                    writer.WriteLine($"using {nss};");
            }
            writer.WriteLine();
            writer.WriteLine($"namespace {ns} {{");
            writer.IncreaseIndent();
            extra?.Invoke(writer);
            return writer;
        }

        private TextWriter WriterFor(VkType type)
        {
            string fileName;
            if (type == null)
                fileName = MiscFile;
            else if (type is VkHandle handle)
                fileName = "Handles/" + handle.TypeName + ".cs";
            else
                fileName = type.GetType().Name + ".cs";
            return GetTextWriter(fileName, (writer) =>
            {
                if (fileName == MiscFile)
                {
                    writer.WriteLineIndent("public static class Vulkan {");
                    writer.IncreaseIndent();
                }
                else if (type is VkHandle handle)
                {
                    writer.WriteLineIndent($"public struct {handle.TypeName} : IEquatable<{handle.TypeName}> {{");
                    writer.IncreaseIndent();
                    writer.WriteLine("#pragma warning disable 649");
                    string handleNull;
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (handle.IsDispatchable)
                    {
                        writer.WriteLineIndent("private IntPtr _handle;");
                        handleNull = "IntPtr.Zero";
                    }
                    else
                    {
                        writer.WriteLineIndent("private ulong _handle;");
                        handleNull = "0UL";
                    }
                    writer.WriteLineIndent($"public static readonly {handle.TypeName} Null = new {handle.TypeName}() {{_handle = {handleNull} }};");
                    writer.WriteLine("#pragma warning restore 649");
                    writer.WriteLine();
                    writer.WriteLineIndent("/// <inheritdoc/>");
                    writer.WriteLineIndent(
                        $"public bool Equals({handle.TypeName} other) => other._handle == _handle;");
                }
            });
        }

        private VkType ResolveType(string name)
        {
            while (true)
            {
                if (!CSharpTypes.TryGetValue(name, out var type))
                    if (!_spec.TypeDefs.TryGetValue(name, out type))
                        return null;
                if (!(type is VkTypeAlias alias))
                    return type;
                name = alias.ActualType;
            }
        }

        private static string GetTypeName(VkType name)
        {
            return name.TypeName;
        }

        private bool IsUnsafe(VkType type)
        {
            switch (type)
            {
                case VkStructOrUnion sou:
                    return sou.Members.Any(x =>
                        x.FixedBufferSize != null || x.PointerLevels >= 1 || IsUnsafe(ResolveType(x.TypeName)));
                case VkTypeFunction tf:
                    return tf.ReturnType.FixedBufferSize != null ||
                           tf.ReturnType.PointerLevels >= 1 ||
                           IsUnsafe(ResolveType(tf.ReturnType.TypeName)) ||
                           tf.Arguments.Any(x =>
                               x.FixedBufferSize != null || x.PointerLevels >= 1 || IsUnsafe(ResolveType(x.TypeName)));
            }
            return false;
        }

        private readonly Dictionary<VkConstant, string> _constantLookupTable = new Dictionary<VkConstant, string>();

        public void Write()
        {
            _constantLookupTable.Clear();
            foreach (var entry in _spec.TypeDefs.Values.OfType<VkEnum>())
            {
                var writer = WriterFor(entry);
                WriteEnum(writer, entry);
                writer.WriteLine();
            }
            {
                var constants = WriterFor(null);
                foreach (var entry in _spec.Constants.Values.Except(_constantLookupTable.Keys))
                {
                    WriteConstant(constants, entry);
                }
                constants.WriteLine();
            }

            foreach (var entry in _spec.TypeDefs.Values.Where(x => !(x is VkEnum)))
            {
                switch (entry)
                {
                    case VkStructOrUnion @struct:
                    {
                        var writer = WriterFor(entry);
                        WriteStructOrUnion(writer, @struct);
                        writer.WriteLine();
                        break;
                    }
                    case VkTypeFunctionPointer func:
                    {
                        var writer = WriterFor(entry);
                        WriteFunctionPointer(writer, func);
                        writer.WriteLine();
                        break;
                    }
                    case VkCommand cmd:
                    {
                        VkType target = null;
                        if (cmd.Arguments.Count > 0)
                        {
                            var ftype = ResolveType(cmd.Arguments[0].TypeName);
                            if (ftype is VkHandle)
                                target = ftype;
                        }
                        var writer = WriterFor(target);
                        WriteCommand(writer, cmd);
                        writer.WriteLine();
                        break;
                    }
                }
            }
        }

        private static string DimensionalLookup(string name, int level)
        {
            if (level == 0)
                return $"{name}.Length";
            var sb = new StringBuilder();
            for (var i = 0; i < level; i++)
            {
                if (i > 0) sb.Append(" && ");
                sb.Append(name);
                for (var j = 0; j < i; j++)
                    sb.Append("[0]");
                sb.Append(".Length > 0");
            }
            sb.Append("? ");
            sb.Append(name);
            for (var j = 0; j < level; j++)
                sb.Append("[0]");
            sb.Append(".Length : 0");
            return sb.ToString();
        }

        private void WriteCommand(TextWriter writer, VkCommand cmd)
        {
            // Normal version
            {
                var dummies = new Dictionary<VkMember, string>();
                if (cmd.ReturnType.FixedBufferSize != null)
                    dummies.Add(cmd.ReturnType, EmitDummyStruct(cmd.ReturnType));
                foreach (var a in cmd.Arguments)
                    if (a.FixedBufferSize != null)
                        dummies.Add(a, EmitDummyStruct(a));

                writer.WriteLineIndent(
                    $"[DllImport(\"{VulkanLibraryName}\", CallingConvention = CallingConvention.StdCall)]");
                writer.WriteIndent();
                writer.Write("internal static extern unsafe ");
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (cmd.ReturnType.FixedBufferSize != null)
                    writer.Write(dummies[cmd.ReturnType]);
                else
                    writer.Write(MemberTypeWithPtr(cmd.ReturnType));
                writer.Write(" ");
                writer.Write(cmd.TypeName);
                writer.Write("(");
                for (var i = 0; i < cmd.Arguments.Count; i++)
                {
                    if (i > 0)
                        writer.Write(", ");
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (cmd.Arguments[i].FixedBufferSize != null)
                        writer.Write(dummies[cmd.Arguments[i]]);
                    else
                        writer.Write(MemberTypeWithPtr(cmd.Arguments[i]));
                    writer.Write(" ");
                    writer.Write(SanitizeVariableName(cmd.Arguments[i].Name));
                }
                writer.WriteLine(");");
            }

            if (!cmd.TypeName.StartsWith("vk")) return;
            // Write proxy version of command.
            {
                VkHandle target = null;
                if (cmd.Arguments.Count > 0)
                {
                    var ftype = ResolveType(cmd.Arguments[0].TypeName);
                    if (ftype is VkHandle handle)
                        target = handle;
                }

                // TODO handle these
                var dummies = new Dictionary<VkMember, string>();
                if (cmd.ReturnType.FixedBufferSize != null)
                    dummies.Add(cmd.ReturnType, EmitDummyStruct(cmd.ReturnType));
                foreach (var a in cmd.Arguments)
                    if (a.FixedBufferSize != null)
                        dummies.Add(a, EmitDummyStruct(a));

                var aliasReturn = cmd.Arguments[cmd.Arguments.Count - 1].PointerLevels == 1
                                  && cmd.Arguments[cmd.Arguments.Count - 1].FixedBufferSize == null
                                  && (cmd.TypeName.IndexOf("Allocate", StringComparison.OrdinalIgnoreCase) != -1
                                      || cmd.TypeName.IndexOf("Create", StringComparison.OrdinalIgnoreCase) != -1)
                                  && (cmd.Arguments[cmd.Arguments.Count - 1].AnnotatedPointerLengths?.Count ?? 0) == 0;

                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                string returnTypeName;
                if (cmd.ReturnType.FixedBufferSize != null)
                    returnTypeName = dummies[cmd.ReturnType];
                else if (!cmd.ReturnType.TypeName.Equals(VkResultType) || cmd.ReturnType.PointerLevels > 0)
                    returnTypeName = MemberTypeWithPtr(cmd.ReturnType);
                else if (aliasReturn)
                    returnTypeName = MemberTypeWithPtr(cmd.Arguments[cmd.Arguments.Count - 1], 1);
                else
                    returnTypeName = "void";

                var nameOffset = cmd.TypeName.StartsWith("vkCmd") ? 5 : 2;
                List<VkMember> argList;
                {
                    var enumerable = (IEnumerable<VkMember>) cmd.Arguments;
                    if (target != null)
                        enumerable = enumerable.Skip(1);
                    if (aliasReturn)
                        enumerable = enumerable.SkipLast(1);
                    argList = enumerable.ToList();
                }
                var argMap = argList.ToDictionary(x => x.Name, x => x);

                var lengthArgSupplier = new Dictionary<VkMember, HashSet<KeyValuePair<VkMember, int>>>();
                foreach (var arg in argList)
                    if (arg.AnnotatedPointerLengths != null && !arg.TypeName.Equals("void"))
                        for (var dim = 0; dim < arg.AnnotatedPointerLengths.Count; dim++)
                        {
                            var spec = arg.AnnotatedPointerLengths[dim];
                            if (!argMap.TryGetValue(spec, out VkMember mem)) continue;
                            if (!lengthArgSupplier.TryGetValue(mem, out HashSet<KeyValuePair<VkMember, int>> set))
                                lengthArgSupplier.Add(mem, set = new HashSet<KeyValuePair<VkMember, int>>());
                            set.Add(new KeyValuePair<VkMember, int>(arg, dim));
                        }

                EmitComment(writer, cmd.Comment, argList.Where(x => !lengthArgSupplier.ContainsKey(x)).ToList(), null,
                    cmd.ErrorCodes);
                writer.WriteIndent();
                if (target != null)
                    writer.Write("public ");
                else
                    writer.Write("public static ");
                var unsafeFn = returnTypeName.Contains('*') || argList.Any(x => x.PointerLevels > 0); 
                if (unsafeFn)
                    writer.Write("unsafe ");
                unsafeFn |= cmd.Arguments.Any(x => x.PointerLevels > 0) || cmd.ReturnType.PointerLevels >= 0;

                writer.Write(returnTypeName);
                writer.Write(" ");
                writer.Write(cmd.TypeName.Substring(nameOffset));
                writer.Write("(");

                var pureArguments = new HashSet<VkMember>();
                {
                    var writtenArguments = 0;
                    foreach (VkMember arg in argList)
                    {
                        if (lengthArgSupplier.ContainsKey(arg)) continue;
                        if (writtenArguments > 0)
                            writer.Write(", ");
                        writtenArguments++;
                        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                        if (arg.FixedBufferSize != null)
                        {
                            writer.Write(dummies[arg]);
                            pureArguments.Add(arg);
                        }
                        else if (arg.AnnotatedPointerLengths != null && arg.AnnotatedPointerLengths.Count > 0
                            && !arg.TypeName.Equals("void"))
                        {
                            var name = new StringBuilder(arg.TypeName.Length + arg.PointerLevels +
                                                         arg.AnnotatedPointerLengths.Count);
                            var isString = arg.TypeName.Equals("char");
                            var type = ResolveType(arg.TypeName);
                            if (isString && arg.PointerLevels > 0 && arg.AnnotatedPointerLengths.Count > 0)
                            {
                                name.Append("string");
                                name.Append('*', arg.PointerLevels - arg.AnnotatedPointerLengths.Count);
                                for (var j = 1; j < arg.AnnotatedPointerLengths.Count; j++)
                                    name.Append("[]");
                            }
                            else
                            {
                                if (type == null || type is VkTypeFunctionPointer)
                                    name.Append("IntPtr");
                                else
                                    name.Append(GetTypeName(type));
                                name.Append('*', arg.PointerLevels - arg.AnnotatedPointerLengths.Count);
                                for (var j = 0; j < arg.AnnotatedPointerLengths.Count; j++)
                                    name.Append("[]");
                            }
                            writer.Write(name.ToString());
                        }
                        else
                        {
                            pureArguments.Add(arg);
                            writer.Write(MemberTypeWithPtr(arg));
                        }
                        writer.Write(" ");
                        writer.Write(SanitizeVariableName(arg.Name));
                    }
                }
                writer.WriteLine(")");
                writer.WriteLineIndent("{");
                writer.IncreaseIndent();
                {
                    var ansiStrings = new List<string>();
                    int indents = 0;
                    if (!unsafeFn)
                    {
                        writer.WriteLineIndent("unsafe {");
                        writer.IncreaseIndent();
                        indents++;
                    }
                    foreach (VkMember arg in argList)
                    {
                        if (pureArguments.Contains(arg))
                            continue;
                        var sanitaryName = SanitizeVariableName(arg.Name);
                        var variableName = sanitaryName + "Real";
                        var declaration = "";
                        {
                            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                            if (arg.FixedBufferSize != null)
                                declaration += dummies[arg];
                            else
                                declaration += MemberTypeWithPtr(arg);
                            declaration += " " + variableName;
                        }
                        if (lengthArgSupplier.TryGetValue(arg, out var suppliers) && suppliers.Count != 0)
                        {
                            using (var enumerator = suppliers.GetEnumerator())
                            {
                                Debug.Assert(enumerator.MoveNext());
                                writer.WriteLineIndent(
                                    $"{declaration} = ({MemberTypeWithPtr(arg)}) ({DimensionalLookup(SanitizeVariableName(enumerator.Current.Key.Name), enumerator.Current.Value)});");
                                while (enumerator.MoveNext())
                                    writer.WriteLineIndent(
                                        $"Debug.Assert({variableName} == {DimensionalLookup(SanitizeVariableName(enumerator.Current.Key.Name), enumerator.Current.Value)});");
                            }
                        }
                        else if (arg.AnnotatedPointerLengths != null && arg.AnnotatedPointerLengths.Count == 1)
                        {
                            if (arg.AnnotatedPointerLengths[0].Equals("null-terminated"))
                            {
                                writer.WriteLineIndent(
                                    $"{declaration} = ({MemberTypeWithPtr(arg)}) Marshal.StringToHGlobalAnsi({sanitaryName}).ToPointer();");
                                ansiStrings.Add(variableName);
                            }
                            else
                            {
                                writer.WriteLineIndent($"fixed ({declaration} = &{sanitaryName}[0]) {{");
                                writer.IncreaseIndent();
                                indents++;
                            }
                        }
                        else
                        {
                            writer.WriteLineIndent("//" + declaration + ";");
                        }
                    }
                    if (aliasReturn)
                    {
                        var ret = cmd.Arguments[cmd.Arguments.Count - 1];
                        writer.WriteLineIndent($"{MemberTypeWithPtr(ret, 1)} retval = default({MemberTypeWithPtr(ret, 1)});");
                    }
                    {
                        if (ansiStrings.Count > 0)
                        {
                            writer.WriteLineIndent("try");
                            writer.WriteLineIndent("{");
                            writer.IncreaseIndent();
                        }
                        // Emit call
                        writer.WriteIndent();
                        if (cmd.ReturnType.TypeName.Equals(VkResultType))
                            writer.Write($"VkException result = VkException.Create(");
                        else if (!cmd.ReturnType.TypeName.Equals("void"))
                            writer.Write("return ");
                        writer.Write(cmd.TypeName);
                        writer.Write("(");
                        var writtenCount = 0;
                        if (target != null)
                        {
                            writer.Write("this");
                            writtenCount++;
                        }
                        foreach (VkMember arg in argList)
                        {
                            if (writtenCount++ > 0)
                                writer.Write(", ");
                            writer.Write(SanitizeVariableName(arg.Name));
                            if (!pureArguments.Contains(arg))
                                writer.Write("Real");
                        }
                        if (aliasReturn)
                        {
                            if (writtenCount++ > 0)
                                writer.Write(", ");
                            writer.Write("&retval");
                        }
                        if (cmd.ReturnType.TypeName.Equals(VkResultType))
                        {
                            writer.WriteLine("));");
                            writer.WriteLineIndent("if (result != null) throw result;");
                        }
                        else
                            writer.WriteLine(");");
                        if (aliasReturn)
                            writer.WriteLineIndent("return retval;");
                        
                        if (ansiStrings.Count > 0)
                        {
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("} finally {");
                            writer.IncreaseIndent();
                            foreach (var str in ansiStrings)
                                writer.WriteLineIndent($"Marshal.FreeHGlobal(new IntPtr({str}));");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                        }
                    }
                    for (var j = 0; j < indents; j++)
                    {
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                    }
                }
                writer.DecreaseIndent();
                writer.WriteLineIndent("}");
            }
        }

        private static readonly Regex ConstantExpressionRegex = new Regex(@"\(constant::(.*?)\)");

        private string SubstituteConstantExpression(string expr)
        {
            var result = new StringBuilder(expr.Length);
            var match = ConstantExpressionRegex.Match(expr);
            var lastIndex = 0;
            while (match.Success)
            {
                result.Append(expr, lastIndex, match.Index);
                var constant = _spec.Constants[match.Groups[1].Value];
                result.Append(_constantLookupTable[constant]);
                lastIndex = match.Index + match.Length;
                match = match.NextMatch();
            }
            result.Append(expr, lastIndex, expr.Length - lastIndex);
            return result.ToString();
        }

        private int EvaluateConstantExpr(string expr)
        {
            var result = new StringBuilder(expr.Length);
            var match = ConstantExpressionRegex.Match(expr);
            var lastIndex = 0;
            while (match.Success)
            {
                result.Append(expr, lastIndex, match.Index);
                var constant = _spec.Constants[match.Groups[1].Value];
                result.Append(constant.Expression);
                lastIndex = match.Index + match.Length;
                match = match.NextMatch();
            }
            result.Append(expr, lastIndex, expr.Length - lastIndex);
            var res = result.ToString();
            if (!int.TryParse(res, out int count))
                count = (int) new DataTable().Compute(res, "");
            return count;
        }

        private readonly HashSet<string> _emittedDummies = new HashSet<string>();

        private string EmitDummyStruct(VkMember cst)
        {
            var count = EvaluateConstantExpr(cst.FixedBufferSize);
            var childType = MemberTypeWithPtr(cst);
            return EmitDummyStruct(count, childType);
        }

        private string EmitDummyStruct(int count, string type)
        {
            if (count == 4 && type == "float")
                return "VkColor";
            const string fileName = "ArrayStruct.cs";
            var writer = GetTextWriter(fileName);
            var dummyType = $"DummyArray{count}_{type.Replace("*", "Ptr")}";
            if (!_emittedDummies.Add(dummyType))
                return dummyType;
            writer.WriteLineIndent($"internal struct {dummyType} : IFixedArray<{type}> {{");

            writer.IncreaseIndent();
            {
                writer.WriteLine("#pragma warning disable 169");
                for (var i = 0; i < count; i++)
                    writer.WriteLineIndent($"private {type} _value{i};");
                writer.WriteLine("#pragma warning restore 169");
                writer.WriteLine();
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent($"public int Count => {count};");
                writer.WriteLine();
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent($"public {type} this[int i] {{");
                writer.IncreaseIndent();
                {
                    writer.WriteLineIndent("get {");
                    writer.IncreaseIndent();
                    {
                        writer.WriteLineIndent(
                            $"if (i < 0 || i >= {count}) throw new IndexOutOfRangeException(\"Index \" + i + \" >= {count}\");");
                        writer.WriteLineIndent("unsafe {");
                        writer.IncreaseIndent();
                        writer.WriteLineIndent($"fixed ({dummyType}* ptr = &this) {{");
                        writer.IncreaseIndent();
                        writer.WriteLineIndent($"return *(({type}*)ptr + i);");
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                    }
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                    writer.WriteLineIndent("set {");
                    writer.IncreaseIndent();
                    {
                        writer.WriteLineIndent(
                            $"if (i < 0 || i >= {count}) throw new IndexOutOfRangeException(\"Index \" + i + \" >= {count}\");");
                        writer.WriteLineIndent("unsafe {");
                        writer.IncreaseIndent();
                        writer.WriteLineIndent($"fixed ({dummyType}* ptr = &this) {{");
                        writer.IncreaseIndent();
                        writer.WriteLineIndent($"*(({type}*)ptr + i) = value;");
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                    }
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
                writer.DecreaseIndent();
                writer.WriteLineIndent("}");

                writer.WriteLine();
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent($"public IEnumerator<{type}> GetEnumerator() => new Enumerator(this);");

                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent(
                    $"System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new Enumerator(this);");

                writer.WriteLine();
                writer.WriteLineIndent("#region Enumerator");
                writer.WriteLineIndent($"private class Enumerator : IEnumerator<{type}> {{");
                writer.IncreaseIndent();
                writer.WriteLineIndent("private int _index;");
                writer.WriteLineIndent($"private {dummyType} _data;");
                writer.WriteLineIndent($"internal Enumerator({dummyType} data) {{ _data = data; _index = -1; }}");
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent($"public bool MoveNext() => (++_index) < {count};");
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent("public void Reset() => _index = 0;");
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent($"public {type} Current => _data[_index];");
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent($"object System.Collections.IEnumerator.Current => _data[_index];");
                writer.WriteLineIndent("/// <inheritdoc />");
                writer.WriteLineIndent("public void Dispose() {}");
                writer.DecreaseIndent();
                writer.WriteLineIndent("}");
                writer.WriteLineIndent("#endregion");
            }
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");
            return dummyType;
        }

        private bool IsNullableStruct(VkStructOrUnion @struct)
        {
            return @struct.Members.All((x) =>
            {
                if (x.PointerLevels > 0)
                    return true;
                var type = ResolveType(x.TypeName);
                if (type is VkStructOrUnion sou)
                    return IsNullableStruct(sou);
                return type is VkTypeFunctionPointer || type is VkHandle;
            });
        }

        private void WriteStructOrUnion(TextWriter writer, VkStructOrUnion @struct)
        {
            EmitComment(writer, @struct.Comment, null, null);
            var isUnsafe = IsUnsafe(@struct);
            var unsafeAttr = isUnsafe ? "unsafe " : "";
            if (@struct is VkUnion)
                writer.WriteLineIndent("[StructLayout(LayoutKind.Explicit)]");
            writer.WriteLineIndent($"public {unsafeAttr}struct {@struct.TypeName} {{");
            writer.IncreaseIndent();
            if (IsNullableStruct(@struct))
            {
                writer.WriteLineIndent($"public static readonly {@struct.TypeName} Null = new {@struct.TypeName}() {{");
                writer.IncreaseIndent();
                foreach (var member in @struct.Members)
                {
                    if (member.PointerLevels > 0)
                        writer.WriteLineIndent(
                            $"{SanitizeStructMemberName(member.Name)} = ({MemberTypeWithPtr(member)}) IntPtr.Zero,");
                    else
                    {
                        var type = ResolveType(member.TypeName);
                        switch (type)
                        {
                            case VkTypeFunctionPointer _:
                                writer.WriteLineIndent(
                                    $"{SanitizeStructMemberName(member.Name)} = IntPtr.Zero,");
                                break;
                            case VkStructOrUnion _:
                                writer.WriteLineIndent(
                                    $"{SanitizeStructMemberName(member.Name)} = {type.TypeName}.Null,");
                                break;
                            default:
                                    throw new Exception(type.TypeName);
                        }
                    }
                }
                writer.DecreaseIndent();
                writer.WriteLineIndent("};");
            }
            foreach (var cst in @struct.Members)
            {
                EmitComment(writer, cst.Comment, null, null);
                var name = SanitizeStructMemberName(cst.Name);
                if (@struct is VkUnion)
                    writer.WriteLineIndent("[FieldOffset(0)]");
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (!string.IsNullOrWhiteSpace(cst.FixedBufferSize))
                {
                    var type = ResolveType(cst.TypeName);
                    if (cst.PointerLevels == 0 && FixedTypes.Contains(GetTypeName(type)))
                        writer.WriteLineIndent(
                            $"public fixed {MemberTypeWithPtr(cst)} {name}[{SubstituteConstantExpression(cst.FixedBufferSize)}];");
                    else
                    {
                        var childType = MemberTypeWithPtr(cst);
                        var dummyType = EmitDummyStruct(cst);
                        writer.WriteLineIndent($"private {dummyType} _backing{name};");
                        writer.WriteLineIndent($"public IFixedArray<{childType}> {name} => _backing{name};");
                    }
                }
                else
                    writer.WriteLineIndent($"public {MemberTypeWithPtr(cst)} {name};");
            }

            foreach (var cst in @struct.Members)
            {
                var type = ResolveType(cst.TypeName);
                if (type is VkTypeFunctionPointer)
                {
                    writer.WriteLine();
                    EmitComment(writer, cst.Comment, null, null);
                    var name = SanitizeVariableName(char.ToUpper(cst.Name[0]) + cst.Name.Substring(1));
                    var delType = GetTypeName(type);
                    writer.WriteLineIndent($"public {delType} Managed{name} {{");
                    writer.IncreaseIndent();
                    writer.WriteLineIndent($"get => Marshal.GetDelegateForFunctionPointer<{delType}>({name});");
                    writer.WriteLineIndent($"set => {name} = Marshal.GetFunctionPointerForDelegate(value);");
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
            }
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");
        }

        private static string SanitizeStructMemberName(string name)
        {
            return SanitizeVariableName(char.ToUpper(name[0]) +name.Substring(1));
        }

        private string MemberTypeWithPtr(VkMember member, int reducePointers = 0)
        {
            var name = new StringBuilder(member.TypeName.Length + member.PointerLevels);
            var type = ResolveType(member.TypeName);
            if (type == null || type is VkTypeFunctionPointer)
                name.Append("IntPtr");
            else
                name.Append(GetTypeName(type));
            name.Append('*', member.PointerLevels - reducePointers);
            return name.ToString();
        }

        private static string SanitizeVariableName(string name)
        {
            if (ReservedKeywords.Contains(name))
                return "@" + name;
            return name;
        }

        private void EmitComment(TextWriter w, string comment, IReadOnlyCollection<VkMember> parameters,
            VkMember result, IReadOnlyCollection<string> exceptions = null)
        {
            if (comment == null && result == null && (parameters == null || parameters.Count == 0) &&
                (exceptions == null || exceptions.Count == 0))
                return;
            w.WriteLineIndent("/// <summary>");
            if (comment != null)
            {
                foreach (var line in comment.Split("\n"))
                {
                    w.WriteIndent();
                    w.Write("/// ");
                    w.WriteLine(line.Trim('/', ' ', '\t'));
                }
            }
            w.WriteLineIndent("/// </summary>");
            if (parameters != null)
                foreach (var p in parameters)
                {
                    w.WriteIndent();
                    w.Write($"/// <param name=\"{SanitizeVariableName(p.Name)}\">");
                    if (p.Comment != null)
                        w.Write(p.Comment.Trim('/', ' ', '\t'));
                    w.WriteLine("</param>");
                }
            if (result != null)
            {
                w.WriteIndent();
                w.Write("/// <returns>");
                if (result.Comment != null)
                    w.Write(result.Comment.Trim('/', ' ', '\t'));
                w.WriteLine("</returns>");
            }
            if (exceptions == null) return;
            foreach (var except in exceptions)
                if (_vkExceptionTypes.TryGetValue(except, out string exceptionName))
                    w.WriteLineIndent($"/// <exception cref=\"VulkanLibrary.Generated.{exceptionName}\"></exception>");
        }

        private void WriteEnum(TextWriter writer, VkEnum @enum)
        {
            EmitComment(writer, @enum.Comment, null, null);
            if (@enum.IsBitmask)
                writer.WriteLineIndent("[Flags]");
            writer.WriteLineIndent($"public enum {@enum.TypeName} {{");
            writer.IncreaseIndent();
            foreach (var cst in @enum.Values)
            {
                var newName =
                    AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), @enum.TypeName));
                _constantLookupTable.Add(cst, @enum.TypeName + "." + newName);
                EmitComment(writer, cst.Comment, null, null);
                writer.WriteLineIndent($"{newName} = {cst.Expression},");
            }
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");

            if (!@enum.TypeName.Equals(VkResultType))
                return;

            writer.WriteLineIndent($"public class VkException : Exception {{");
            writer.IncreaseIndent();
            writer.WriteLineIndent("public readonly VkResult Result;");
            writer.WriteLineIndent("public VkException(VkResult result) { Result = result; }");
            writer.WriteLineIndent("public static VkException Create(VkResult result) {");
            writer.IncreaseIndent();
            writer.WriteLineIndent("if ((int) result >= 0) return null;");
            writer.WriteLineIndent("switch (result) {");
            writer.IncreaseIndent();
            foreach (var cst in @enum.Values)
            {
                var res = EvaluateConstantExpr(cst.Expression);
                if (res < 0)
                {
                    var newName =
                        AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), @enum.TypeName));
                    writer.WriteLineIndent($"case VkResult.{newName}: return new Vk{newName}();");
                }
            }
            writer.WriteLineIndent($"default: return new VkException(result);");
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");

            _vkExceptionTypes.Clear();
            foreach (var cst in @enum.Values)
            {
                var res = EvaluateConstantExpr(cst.Expression);
                if (res < 0)
                {
                    var newName =
                        AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), @enum.TypeName));
                    EmitComment(writer, cst.Comment, null, null);
                    _vkExceptionTypes.Add(cst.Name, "Vk" + newName);
                    writer.WriteLineIndent($"public class Vk{newName} : VkException {{");
                    writer.IncreaseIndent();
                    writer.WriteLineIndent($"public Vk{newName}():base(VkResult.{newName}) {{}}");
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
            }
        }

        private readonly Dictionary<string, string> _vkExceptionTypes = new Dictionary<string, string>();

        private void WriteConstant(TextWriter writer, VkConstant cst)
        {
            var newName =
                AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), "Vk"));
            _constantLookupTable.Add(cst, "Vulkan." + newName);
            EmitComment(writer, cst.Comment, null, null);
            var expr = cst.Expression.Replace("ll", "l", StringComparison.OrdinalIgnoreCase);
            string valueType = null;
            var unsigned = expr.IndexOf("u", StringComparison.OrdinalIgnoreCase) != -1;
            if (expr.IndexOf("d", StringComparison.OrdinalIgnoreCase) != -1)
                valueType = "double";
            else if (expr.IndexOf("f", StringComparison.OrdinalIgnoreCase) != -1)
                valueType = "float";
            else if (expr.IndexOf("l", StringComparison.OrdinalIgnoreCase) != -1)
                valueType = unsigned ? "ulong" : "long";
            else
                valueType = unsigned ? "uint" : "int";
            writer.WriteLineIndent($"public const {valueType} {newName} = {expr};");
        }

        private void WriteFunctionPointer(TextWriter writer, VkTypeFunctionPointer ptr)
        {
            EmitComment(writer, ptr.Comment, ptr.Arguments, ptr.ReturnType);
            writer.WriteIndent();
            writer.Write("public ");
            if (IsUnsafe(ptr))
                writer.Write("unsafe ");
            writer.Write("delegate ");
            Debug.Assert(ptr.ReturnType.FixedBufferSize == null);
            writer.Write(MemberTypeWithPtr(ptr.ReturnType));
            writer.Write(" ");
            writer.Write(ptr.TypeName);
            writer.Write("(");
            for (var i = 0; i < ptr.Arguments.Count; i++)
            {
                if (i > 0)
                    writer.Write(", ");
                Debug.Assert(ptr.Arguments[i].FixedBufferSize == null);
                writer.Write(MemberTypeWithPtr(ptr.Arguments[i]));
                writer.Write(" ");
                writer.Write(SanitizeVariableName(ptr.Arguments[i].Name));
            }
            writer.WriteLine(");");
        }

        private static string RemoveCommonPrefix(string removeFrom, string other)
        {
            var lastBoundary = 0;
            for (var i = 0; i < removeFrom.Length; i++)
            {
                if (char.IsUpper(removeFrom[i]))
                    lastBoundary = i;
                if (i >= other.Length || removeFrom[i] != other[i])
                    return removeFrom.Substring(lastBoundary);
            }
            return "";
        }

        private static string AlphabetizeLeadingNumber(string name)
        {
            if (!char.IsDigit(name[0]))
                return name;
            return "Vk" + name;
        }

        private static string UnderscoresToCamelCase(string value)
        {
            var result = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '_')
                    continue;
                if (i == 0 || value[i - 1] == '_')
                    result.Append(char.ToUpper(value[i]));
                else
                    result.Append(char.ToLower(value[i]));
            }
            return result.ToString();
        }

        public void Dispose()
        {
            foreach (var writer in _writers.Values)
            {
                while (writer.IndentLevel() > 0)
                {
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
                writer.Close();
            }
        }
    }
}