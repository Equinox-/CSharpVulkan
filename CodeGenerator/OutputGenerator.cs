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

        private const string MiscFile = "Unmanaged/Vulkan.cs";
        private const string VkResultType = "VkResult";

        private readonly string[] _namespaceSuffixes = {"", ".Unmanaged", ".Unmanaged.Handles"};

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
            writer.WriteLine("using System.Runtime.CompilerServices;");
            writer.WriteLine("using System.Diagnostics;");
            foreach (var nssuffix in _namespaceSuffixes)
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
                fileName = "Unmanaged/Handles/" + handle.TypeName + ".cs";
            else
                fileName = "Unmanaged/" + type.GetType().Name + ".cs";
            return GetTextWriter(fileName, (writer) =>
            {
                if (fileName == MiscFile)
                {
                    writer.WriteLineIndent("public static partial class Vulkan {");
                    writer.IncreaseIndent();
                }
                else if (type is VkHandle handle)
                {
                    EmitComment(writer, type.Comment, null, null);
                    writer.WriteLineIndent(
                        $"public partial struct {handle.TypeName} : IEquatable<{handle.TypeName}> {{");
                    writer.IncreaseIndent();
                    writer.WriteLineIndent($"// Parents are {string.Join(", ", handle.ParentHandles)}");
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
                    writer.WriteLineIndent(
                        $"public static readonly {handle.TypeName} Null = new {handle.TypeName}() {{_handle = {handleNull} }};");
                    writer.WriteLine("#pragma warning restore 649");
                    writer.WriteLine();
                    writer.WriteLineIndent("/// <inheritdoc/>");
                    writer.WriteLineIndent($"public bool Equals({handle.TypeName} other) => other._handle == _handle;");
                    writer.WriteLineIndent(
                        $"public override bool Equals(object obj) => obj is {handle.TypeName} other && other._handle == _handle;");

                    writer.WriteLineIndent(
                        $"public static bool operator== ({handle.TypeName} a, {handle.TypeName} b) => a._handle == b._handle;");
                    writer.WriteLineIndent(
                        $"public static bool operator!= ({handle.TypeName} a, {handle.TypeName} b) => a._handle != b._handle;");
                    writer.WriteLineIndent("/// <inheritdoc/>");
                    writer.WriteLineIndent($"public override int GetHashCode() => _handle.GetHashCode();");
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
            if (name is VkEnum @enum)
            {
                var typeName = @enum.TypeName;
                if (typeName.EndsWith("Bits") && @enum.IsBitmask)
                    typeName = typeName.Substring(0, typeName.Length - 4);
                return typeName;
            }
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
            WriteUnmanagedTypes();

            WriteManagedTypes();
        }

        private static string ResolveManagedTypeName(VkType type)
        {
            if (type.TypeName.StartsWith("Vk"))
                return type.TypeName.Substring(2);
            throw new Exception(type.TypeName);
        }

        private void WriteManagedTypes()
        {
            {
                var parentTypes = _spec.TypeDefs.Values.OfType<VkHandle>().SelectMany(x => x.ParentHandles)
                    .Where(x => x != null)
                    .Select(ResolveType).OfType<VkHandle>().ToHashSet();
                var ownershipWriter = GetTextWriter("Managed/Utilities/IOwnership.cs");
                foreach (var parentType in parentTypes)
                {
                    var managedType = ResolveManagedTypeName(parentType);
                    ownershipWriter.WriteLineIndent("/// <summary>");
                    ownershipWriter.WriteLineIndent(
                        $"/// Indicates that this resource is owned by a <see cref=\"Handles.{managedType}\"/>");
                    ownershipWriter.WriteLineIndent("/// </summary>");
                    if (parentType.ParentHandles.Count > 0)
                        ownershipWriter.WriteLineIndent("/// <inheritdoc />");
                    ownershipWriter.WriteIndent();
                    ownershipWriter.Write($"public interface I{managedType}Owned");
                    {
                        var first = true;
                        foreach (var parent in parentType.ParentHandles.Select(ResolveType).OfType<VkHandle>())
                        {
                            ownershipWriter.Write(first ? " : " : ", ");
                            first = false;
                            ownershipWriter.Write($"I{ResolveManagedTypeName(parent)}Owned");
                        }
                    }
                    ownershipWriter.WriteLine();
                    ownershipWriter.WriteLineIndent("{");
                    ownershipWriter.IncreaseIndent();
                    ownershipWriter.WriteLineIndent("/// <summary>");
                    ownershipWriter.WriteLineIndent(
                        $"/// Gets the <see cref=\"Handles.{managedType}\"/> that owns this resource");
                    ownershipWriter.WriteLineIndent("/// </summary>");
                    ownershipWriter.WriteLineIndent($"Handles.{managedType} {managedType} {{ get; }}");
                    ownershipWriter.DecreaseIndent();
                    ownershipWriter.WriteLineIndent("}");
                }
            }
            {
                var parentTypes = new HashSet<VkHandle>();
                var parentQueue = new Queue<VkHandle>();
                foreach (var handle in _spec.TypeDefs.Values.OfType<VkHandle>())
                {
                    var parents = handle.ParentHandles.Select(ResolveType).OfType<VkHandle>().ToArray();
                    var localParentBases = new List<VkHandle>[parents.Length];
                    parentTypes.Clear();
                    for (var i = 0; i < parents.Length; i++)
                    {
                        localParentBases[i] = new List<VkHandle>() {parents[i]};
                        parentQueue.Clear();
                        parentQueue.Enqueue(parents[i]);
                        if (!parentTypes.Add(parents[i])) continue;
                        while (parentQueue.TryDequeue(out VkHandle parent))
                        {
                            foreach (var pp in parent.ParentHandles)
                            {
                                var parentType = ResolveType(pp) as VkHandle;
                                Debug.Assert(parentType != null);
                                if (!parentTypes.Add(parentType)) continue;
                                parentQueue.Enqueue(parentType);
                                localParentBases[i].Add(parentType);
                            }
                        }
                    }

                    var managedType = ResolveManagedTypeName(handle);
                    var unmanagedType = handle.TypeName;
                    var writer = GetTextWriter($"Managed/Handles/{managedType}.cs");
                    writer.WriteLineIndent("/// <summary>");
                    writer.WriteLineIndent($"/// Managed handle of <see cref=\"{unmanagedType}\"/>");
                    writer.WriteLineIndent("/// </summary>");
                    writer.WriteIndent();
                    writer.Write(
                        $"public partial class {managedType} : Utilities.VulkanHandle<{unmanagedType}>");
                    foreach (var parentType in parents)
                        writer.Write($", Utilities.I{ResolveManagedTypeName(parentType)}Owned");
                    writer.WriteLine();
                    writer.WriteLineIndent("{");
                    writer.IncreaseIndent();
                    for (var p = 0; p < parents.Length; p++)
                    {
                        var resolvedName = ResolveManagedTypeName(parents[p]);
                        foreach (var k in localParentBases[p])
                        {
                            var keyName = ResolveManagedTypeName(k);
                            writer.WriteLineIndent($"/// <inheritdoc cref=\"Utilities.I{keyName}Owned.{keyName}\" />");
                            writer.WriteIndent();
                            writer.Write($"public {keyName} {keyName} {{ ");
                            writer.Write("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                            if (k != parents[p])
                                writer.Write($" get => {resolvedName}.{keyName}");
                            else
                                writer.Write(" get; private set");
                            writer.WriteLine("; }");
                            writer.WriteLine();
                        }
                    }
                    if (parents.Length > 0)
                    {
                        writer.WriteLineIndent($"/// <inheritdoc cref=\"Utilities.VulkanHandle.AssertValid\" />");
                        writer.WriteLineIndent("public override void AssertValid()");
                        {
                            writer.WriteLineIndent("{");
                            writer.IncreaseIndent();
                            writer.WriteLineIndent("base.AssertValid();");
                            foreach (VkHandle t in parents)
                                writer.WriteLineIndent($"{ResolveManagedTypeName(t)}.AssertValid();");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                        }
                    }
                    // try to implement free()
                    var freeMethod = _spec.TypeDefs.Values.OfType<VkCommand>().Where(x =>
                            (x.TypeName.IndexOf("destroy", StringComparison.OrdinalIgnoreCase) != -1
                             || x.TypeName.IndexOf("free", StringComparison.OrdinalIgnoreCase) != -1
                             || x.TypeName.IndexOf("release", StringComparison.OrdinalIgnoreCase) != -1)
                            && x.Arguments.All(y =>
                            {
                                var type = ResolveType(y.TypeName);
                                // icky, icky
                                return parentTypes.Contains(type) || type == handle ||
                                       y.TypeName.IndexOf("AllocationCallback", StringComparison.OrdinalIgnoreCase) !=
                                       -1
                                       || x.Arguments.Any(z =>
                                           z.AnnotatedPointerLengths != null &&
                                           z.AnnotatedPointerLengths.Contains(y.Name));
                            })
                            && x.Arguments.Any(y => ResolveType(y.TypeName) == handle))
                        .OrderBy(x => x.Arguments.Count).FirstOrDefault();
                    writer.WriteLineIndent($"/// <inheritdoc cref=\"Utilities.VulkanHandle.Free\" />");
                    writer.WriteLineIndent("protected override void Free()");
                    {
                        writer.WriteLineIndent("{");
                        writer.IncreaseIndent();
                        if (freeMethod != null)
                        {
                            #region Emit Free

                            var isUnsafe = freeMethod.Arguments.Any(x => x.PointerLevels > 0);
                            if (isUnsafe)
                            {
                                writer.WriteLineIndent("unsafe {");
                                writer.IncreaseIndent();
                            }
                            var argTypes = freeMethod.Arguments.Select(x => ResolveType(x.TypeName)).ToArray();
                            var rootType = argTypes[0] is VkHandle ? argTypes[0].TypeName : "Vulkan";
                            var argCache = new HashSet<VkMember>();
                            for (var i = 0; i < freeMethod.Arguments.Count; i++)
                            {
                                var arg = freeMethod.Arguments[i];
                                if (arg.PointerLevels <= 0)
                                    continue;
                                if (argTypes[i] == handle)
                                    writer.WriteLineIndent($"var arg{i} = Handle;");
                                else if (parentTypes.Contains(argTypes[i]))
                                    writer.WriteLine($"var arg{i} = {ResolveManagedTypeName(argTypes[i])}.Handle;");
                                else if (!arg.Optional)
                                    writer.WriteLine($"var arg{i} = {argTypes[i].TypeName}.Null;");
                                else
                                    continue;
                                argCache.Add(arg);
                            }
                            writer.WriteIndent();
                            writer.Write($"{rootType}.{freeMethod.TypeName}(");
                            for (var i = 0; i < freeMethod.Arguments.Count; i++)
                            {
                                if (i > 0)
                                    writer.Write(", ");
                                var arg = freeMethod.Arguments[i];
                                if (arg.PointerLevels <= 0)
                                {
                                    if (argTypes[i] == handle)
                                        writer.Write("Handle");
                                    else if (parentTypes.Contains(argTypes[i]))
                                        writer.Write($"{ResolveManagedTypeName(argTypes[i])}.Handle");
                                    else if (freeMethod.Arguments.Any(z =>
                                        z.AnnotatedPointerLengths != null &&
                                        z.AnnotatedPointerLengths.Contains(arg.Name)))
                                        writer.Write($"({argTypes[i].TypeName}) 1");
                                    else
                                        writer.Write($"{argTypes[i].TypeName}.Null");
                                }
                                else if (argCache.Contains(arg))
                                {
                                    writer.Write($"&arg{i}");
                                }
                                else if (parentTypes.Any(x => x.TypeName.Equals("VkInstance")) &&
                                         arg.TypeName.Equals("VkAllocationCallbacks"))
                                {
                                    writer.Write($"Instance.AllocationCallbacks");
                                }
                                else if (handle.TypeName.Equals("VkInstance") &&
                                         arg.TypeName.Equals("VkAllocationCallbacks"))
                                {
                                    writer.Write($"AllocationCallbacks");
                                }
                                else
                                {
                                    writer.Write($"({MemberTypeWithPtr(arg)}) 0");
                                }
                            }
                            writer.WriteLine(");");
                            if (isUnsafe)
                            {
                                writer.DecreaseIndent();
                                writer.WriteLineIndent("}");
                            }

                            #endregion Emit Free
                        }
                        else
                        {
                            writer.WriteLineIndent("// Can't auto generate free method: No canidates");
                        }
                        writer.WriteLineIndent($"Handle = {handle.TypeName}.Null;");
                        foreach (VkHandle t in parents)
                        {
                            var resolvedName = ResolveManagedTypeName(t);
                            writer.WriteLineIndent($"{resolvedName} = null;");
                        }
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                    }
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
            }
        }

        private readonly Dictionary<VkExtension, string> _extensionLookupTable = new Dictionary<VkExtension, string>();

        private static string CleanExtensionName(string name)
        {
            var cleanName = UnderscoresToCamelCase(name);
            if (cleanName.StartsWith("Vk"))
                cleanName = cleanName.Substring(2);
            return cleanName;
        }

        private void RequiresExtension(TextWriter writer, VkExtension ext)
        {
            if (ext == null)
                return;
            writer.WriteLineIndent($"[ExtensionRequired(VkExtension.{_extensionLookupTable[ext]})]");
        }

        private void WriteUnmanagedTypes()
        {
            _constantLookupTable.Clear();
            _extensionLookupTable.Clear();
            {
                var writer = GetTextWriter("Unmanaged/VkExtension.cs", (x) =>
                {
                    x.WriteLineIndent("/// <summary>");
                    x.WriteLineIndent("/// Describes the extensions supported by this library");
                    x.WriteLineIndent("/// </summary>");
                    x.WriteLineIndent("public enum VkExtension {");
                    x.IncreaseIndent();
                });
                foreach (var extension in _spec.Extensions.Values)
                {
                    var cleanName = CleanExtensionName(extension.Name);
                    _extensionLookupTable[extension] = cleanName;
                    var required = string.Join("", extension.Required.Select(CleanExtensionName).Select(x => ", " + x));
                    var extType = extension.Type;
                    if (extType == null)
                        extType = "Unknown";
                    else
                        extType = char.ToUpper(extType[0]) + extType.Substring(1);
                    writer.WriteLineIndent(
                        $"[ExtensionDescriptionAttribute(\"{extension.Name}\", {extension.Number}, ExtensionType.{extType}, {extension.Version}{required})]");
                    writer.WriteLineIndent($"{cleanName} = {extension.Number},");
                }
            }

            var nonEnumConstants =
                _spec.Constants.Values.Except(_spec.TypeDefs.Values.OfType<VkEnum>().SelectMany(x => x.Values))
                    .ToList();
            for (var stage = 0; stage <= 1; stage++)
            {
                var write = stage != 0;
                foreach (var entry in _spec.TypeDefs.Values.OfType<VkEnum>())
                {
                    var writer = WriterFor(entry);
                    WriteEnum(writer, entry, write);
                    if (write)
                        writer.WriteLine();
                }
                {
                    var constants = WriterFor(null);
                    foreach (var entry in nonEnumConstants)
                    {
                        WriteConstant(constants, entry, write);
                    }
                    if (write)
                        constants.WriteLine();
                }
            }

            foreach (var entry in _spec.TypeDefs.Values.Where(x => !(x is VkEnum)))
            {
                switch (entry)
                {
                    case VkHandle handle:
                        WriterFor(handle);
                        break;
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

        private void WriteCommandArgs(TextWriter writer, Dictionary<VkMember, string> dummies, VkCommand cmd)
        {
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
        }

        private void WriteCommand(TextWriter writer, VkCommand cmd)
        {
            var dummies = new Dictionary<VkMember, string>();
            if (cmd.ReturnType.FixedBufferSize != null)
                dummies.Add(cmd.ReturnType, EmitDummyStruct(cmd.ReturnType));
            foreach (var a in cmd.Arguments)
                if (a.FixedBufferSize != null)
                    dummies.Add(a, EmitDummyStruct(a));

            #region Normal version

            {
                EmitComment(writer, cmd.Comment, cmd.Arguments, cmd.ReturnType, cmd.ErrorCodes);
                var exportUsingGetProc = false;
                if (cmd.Extension?.Type != null)
                {
                    if (cmd.Extension.Type.IndexOf("Instance",
                            StringComparison.OrdinalIgnoreCase) == 0)
                        exportUsingGetProc = cmd.Arguments.Any(x => x.TypeName.Equals("VkInstance"));
                    else
                        exportUsingGetProc = cmd.Arguments.Any(x => x.TypeName.Equals("VkDevice"));
                }
                if (cmd.Extension != null)
                    RequiresExtension(writer, cmd.Extension);
                if (!exportUsingGetProc)
                    writer.WriteLineIndent(
                        $"[DllImport(\"{VulkanLibraryName}\", CallingConvention = CallingConvention.StdCall)]");
                writer.WriteIndent();
                writer.Write("public unsafe ");
                if (exportUsingGetProc)
                    writer.Write("delegate ");
                else
                    writer.Write("static extern ");
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (cmd.ReturnType.FixedBufferSize != null)
                    writer.Write(dummies[cmd.ReturnType]);
                else
                    writer.Write(MemberTypeWithPtr(cmd.ReturnType));
                writer.Write(" ");
                writer.Write(cmd.TypeName);
                if (exportUsingGetProc)
                    writer.Write("Delegate");
                writer.Write("(");
                WriteCommandArgs(writer, dummies, cmd);
                writer.WriteLine(");");
                if (exportUsingGetProc)
                {
                    writer.WriteLine();
                    writer.WriteLineIndent($"private static {cmd.TypeName}Delegate _{cmd.TypeName} = null;");
                    writer.WriteLine();
                    RequiresExtension(writer, cmd.Extension);
                    writer.WriteIndent();
                    writer.Write($"public static unsafe ");
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (cmd.ReturnType.FixedBufferSize != null)
                        writer.Write(dummies[cmd.ReturnType]);
                    else
                        writer.Write(MemberTypeWithPtr(cmd.ReturnType));
                    writer.Write(" ");
                    writer.Write(cmd.TypeName);
                    writer.Write("(");
                    WriteCommandArgs(writer, dummies, cmd);
                    writer.WriteLine(") {");
                    writer.IncreaseIndent();
                    try
                    {
                        var getProcInvoke =
                            cmd.Extension.Type.IndexOf("Instance", StringComparison.OrdinalIgnoreCase) == 0
                                ? cmd.Arguments.First(x => x.TypeName.Equals("VkInstance")).Name +
                                  ".GetInstanceProcAddr"
                                : cmd.Arguments.First(x => x.TypeName.Equals("VkDevice")).Name + ".GetDeviceProcAddr";
                        writer.WriteLineIndent($"if (_{cmd.TypeName} == null)");
                        writer.IncreaseIndent();
                        writer.WriteLineIndent(
                            $"_{cmd.TypeName} = Marshal.GetDelegateForFunctionPointer<{cmd.TypeName}Delegate>({getProcInvoke}(\"{cmd.TypeName}\"));");
                        writer.DecreaseIndent();
                        writer.WriteLine();
                        writer.WriteIndent();
                        if (GetTypeName(ResolveType(cmd.ReturnType.TypeName)) != "void")
                            writer.Write("return ");
                        writer.Write($"_{cmd.TypeName}(");
                        writer.Write(string.Join(", ", cmd.Arguments.Select(x => SanitizeVariableName(x.Name))));
                        writer.WriteLine(");");
                        writer.DecreaseIndent();
                        writer.WriteLineIndent("}");
                    }
                    catch
                    {
                        Console.WriteLine(cmd);
                        throw;
                    }
                }
            }

            #endregion

            if (!cmd.TypeName.StartsWith("vk")) return;

            VkHandle target = null;
            if (cmd.Arguments.Count > 0)
            {
                var ftype = ResolveType(cmd.Arguments[0].TypeName);
                if (ftype is VkHandle handle)
                    target = handle;
            }
            var proxyName = cmd.TypeName.Substring(cmd.TypeName.StartsWith("vkCmd") ? 5 : 2);

            #region Proxy 1, Assured

            {
                writer.WriteLine();
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
                if (cmd.Extension != null)
                    RequiresExtension(writer, cmd.Extension);
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
                writer.Write(proxyName);
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
                        var variableType = MemberTypeWithPtr(arg,
                            lengthArgSupplier.ContainsKey(arg) && arg.PointerLevels > 0 ? 1 : 0);
                        var declaration = "";
                        {
                            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                            if (arg.FixedBufferSize != null)
                                declaration += dummies[arg];
                            else
                                declaration += variableType;
                            declaration += " " + variableName;
                        }
                        if (lengthArgSupplier.TryGetValue(arg, out var suppliers) && suppliers.Count != 0)
                        {
                            using (var enumerator = suppliers.GetEnumerator())
                            {
                                Debug.Assert(enumerator.MoveNext());
                                writer.WriteLineIndent(
                                    $"{declaration} = ({MemberTypeWithPtr(arg, lengthArgSupplier.ContainsKey(arg) && arg.PointerLevels > 0 ? 1 : 0)}) ({DimensionalLookup(SanitizeVariableName(enumerator.Current.Key.Name), enumerator.Current.Value)});");
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
                                    $"{declaration} = {sanitaryName} != null ? ({MemberTypeWithPtr(arg)}) Marshal.StringToHGlobalAnsi({sanitaryName}).ToPointer() : ({MemberTypeWithPtr(arg)}) 0;");
                                ansiStrings.Add(variableName);
                            }
                            else
                            {
                                writer.WriteLineIndent($"fixed ({declaration} = {sanitaryName}) {{");
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
                        writer.WriteLineIndent(
                            $"{MemberTypeWithPtr(ret, 1)} retval = default({MemberTypeWithPtr(ret, 1)});");
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
                            writer.Write($"VkException.Check(");
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
                            if (arg.PointerLevels > 0 && lengthArgSupplier.ContainsKey(arg))
                                writer.Write("&");
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
                                writer.WriteLineIndent(
                                    $"if ({str} != (byte*) 0) Marshal.FreeHGlobal(new IntPtr({str}));");
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

            #endregion

            #region Proxy 2, Heuristic Get-into-struct

            if (proxyName.StartsWith("Get") && cmd.Arguments.Count > 0 &&
                (cmd.ReturnType.TypeName.Equals(VkResultType) || cmd.ReturnType.TypeName.Equals("void")))
            {
                var lastArg = cmd.Arguments[cmd.Arguments.Count - 1];
                var level = 0;
                if (lastArg.Comment != null && lastArg.PointerLevels == 1 && lastArg.FixedBufferSize == null)
                {
                    if (lastArg.Comment.IndexOf("in which", StringComparison.OrdinalIgnoreCase) != -1)
                        level++;
                    if (lastArg.Comment.IndexOf("are returned", StringComparison.OrdinalIgnoreCase) != -1)
                        level++;
                    if (lastArg.Comment.IndexOf("will be returned", StringComparison.OrdinalIgnoreCase) != -1)
                        level++;
                    if (lastArg.Comment.IndexOf("will be filled", StringComparison.OrdinalIgnoreCase) != -1)
                        level += 2;
                    if (lastArg.Comment.IndexOf("which is set to", StringComparison.OrdinalIgnoreCase) != -1)
                        level += 2;
                }
                // ReSharper disable once InvertIf
                if (level >= 2)
                {
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    string returnTypeName = GetTypeName(ResolveType(lastArg.TypeName));

                    List<VkMember> argList;
                    {
                        var enumerable = cmd.Arguments.SkipLast(1);
                        if (target != null)
                            enumerable = enumerable.Skip(1);
                        argList = enumerable.ToList();
                    }
                    EmitComment(writer, cmd.Comment, argList, null, cmd.ErrorCodes);
                    if (cmd.Extension != null)
                        RequiresExtension(writer, cmd.Extension);
                    writer.WriteIndent();
                    if (target != null)
                        writer.Write("public ");
                    else
                        writer.Write("public static ");
                    var unsafeFn = returnTypeName.Contains('*') || argList.Any(x => x.PointerLevels > 0);
                    if (unsafeFn)
                        writer.Write("unsafe ");
                    writer.Write(returnTypeName);
                    writer.Write(" ");
                    writer.Write(proxyName);
                    writer.Write("(");

                    var pureArguments = new HashSet<VkMember>();
                    {
                        var writtenArguments = 0;
                        foreach (VkMember arg in argList)
                        {
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
                        writer.WriteLineIndent("unsafe {");
                        writer.IncreaseIndent();
                        indents++;
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
                            if (arg.AnnotatedPointerLengths != null && arg.AnnotatedPointerLengths.Count == 1)
                            {
                                if (arg.AnnotatedPointerLengths[0].Equals("null-terminated"))
                                {
                                    writer.WriteLineIndent(
                                        $"{declaration} = {sanitaryName} != null ? ({MemberTypeWithPtr(arg)}) Marshal.StringToHGlobalAnsi({sanitaryName}).ToPointer() : ({MemberTypeWithPtr(arg)}) 0;");
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
                        writer.WriteIndent();
                        writer.Write($"{returnTypeName} retval = ");
                        if (lastArg.PointerLevels > 1)
                        {
                            writer.WriteLine($"({MemberTypeWithPtr(lastArg, 1)}) IntPtr.Zero;");
                        }
                        else if (ResolveType(lastArg.TypeName) is VkStructOrUnion sl && IsConstrainedDefault(sl))
                        {
                            writer.WriteLine($"{sl.TypeName}.Default;");
                        }
                        else
                        {
                            writer.WriteLine($"default({returnTypeName});");
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
                            if (writtenCount++ > 0)
                                writer.Write(", ");
                            writer.Write("&retval");
                            if (cmd.ReturnType.TypeName.Equals(VkResultType))
                            {
                                writer.WriteLine("));");
                                writer.WriteLineIndent("if (result != null) throw result;");
                            }
                            else
                                writer.WriteLine(");");
                            writer.WriteLineIndent("return retval;");

                            if (ansiStrings.Count > 0)
                            {
                                writer.DecreaseIndent();
                                writer.WriteLineIndent("} finally {");
                                writer.IncreaseIndent();
                                foreach (var str in ansiStrings)
                                    writer.WriteLineIndent(
                                        $"if ({str} != (byte*) 0) Marshal.FreeHGlobal(new IntPtr({str}));");
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

            #endregion
        }

        private static readonly Regex DoubleLongExpressionRegex = new Regex(@"([0-9Uu][lL])[lL](\b)");
        private static readonly Regex ConstantExpressionRegex = new Regex(@"\(constant::(.*?)\)");

        private string SubstituteConstantExpression(string expr, bool all)
        {
            expr = DoubleLongExpressionRegex.Replace(expr, (a) => a.Groups[1].Value + a.Groups[2].Value);
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
            if (!all)
                return result.ToString();
            foreach (var kv in _constantLookupTable)
                result = result.Replace(kv.Key.Name, kv.Value);
            return result.ToString();
        }

        private long EvaluateConstantExpr(string expr)
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
            if (!long.TryParse(res, out long count))
            {
                // avoid boxing issues
                count = long.Parse(new DataTable().Compute(res, "").ToString());
            }
            return count;
        }

        private readonly HashSet<string> _emittedDummies = new HashSet<string>();


        private static readonly Regex DocumentationPattern = new Regex(@"([a-z_]+):([a-zA-Z_0-9]+)");
        private static readonly Regex DocumentationLinkPattern = new Regex(@"<<([^,]*?),([^,]*?)>>");

        private string RewriteComment(string comment)
        {
            return DocumentationLinkPattern.Replace(DocumentationPattern.Replace(comment, (match) =>
            {
                var doctype = match.Groups[1].Value;

                var docval = match.Groups[2].Value;
                switch (doctype)
                {
                    case "sname":
                    case "slink":
                    case "basetype":
                        return $"<see cref=\"{docval}\"/>";
                        break;
                    case "elink":
                    case "ename":
                        return _spec.Constants.TryGetValue(docval, out var cst) &&
                               _constantLookupTable.TryGetValue(cst, out var ncst)
                            ? $"<see cref=\"{ncst}\"/>"
                            : $"<c>{docval}</c>";
                    case "pname":
                        return docval;
                    case "flink":
                    case "fname":
                    {
                        var type = _spec.TypeDefs.GetValueOrDefault(docval);
                        if (type is VkCommand cmd)
                        {
                            string prefix;
                            if (cmd.Arguments.Count > 0 &&
                                _spec.TypeDefs.TryGetValue(cmd.Arguments[0].TypeName, out VkType asdf) &&
                                asdf is VkHandle)
                                prefix = asdf.TypeName;
                            else
                                prefix = "Vulkan";
                            return $"<see cref=\"{prefix}.{docval}\"/>";
                        }
                        else if (type is VkTypeFunctionPointer ptr)
                            return $"<see cref=\"{docval}\"/>";
                        break;
                    }
                    case "code":
                        return $"<c>{docval}</c>";
                }

                return "";
            }), (match) =>
            {
                var id = match.Groups[1].Value;

                var name = match.Groups[2].Value;
                return $"<a href='https://www.khronos.org/registry/vulkan/specs/1.0/html/vkspec.html#{id}'>{name}</a>";
            });
        }

        private string EmitDummyStruct(VkMember cst)
        {
            var count = EvaluateConstantExpr(cst.FixedBufferSize);

            var childType = MemberTypeWithPtr(cst);
            return EmitDummyStruct(checked((int) count), childType);
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
                for (var i = 0;
                    i < count;
                    i++)
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

        private bool IsConstrainedDefault(VkStructOrUnion @struct)
        {
            foreach (var entry in @struct.Members)
            {
                var type = ResolveType(entry.TypeName);
                if (entry.PointerLevels != 0 && entry.Optional)
                    continue;
                if (entry.PossibleValueExpressions != null && entry.PossibleValueExpressions.Count > 0)
                {
                    var expr = SubstituteConstantExpression(entry.PossibleValueExpressions[0], true);
                    try
                    {
                        if (EvaluateConstantExpr(expr) != 0)
                            return true;
                    }
                    catch
                    {
                        continue;
                    }
                }
                if (type is VkStructOrUnion child && IsConstrainedDefault(child))
                    return true;
            }
            return false;
        }

        private void WriteStructOrUnion(TextWriter writer, VkStructOrUnion @struct)
        {
            EmitComment(writer, @struct.Comment, null, null);
            var isUnsafe = IsUnsafe(@struct);
            var unsafeAttr = isUnsafe ? "unsafe " : "";
            if (@struct is VkUnion)
                writer.WriteLineIndent("[StructLayout(LayoutKind.Explicit)]");
            if (@struct.Extension != null)
                RequiresExtension(writer, @struct.Extension);
            writer.WriteLineIndent($"public {unsafeAttr}struct {@struct.TypeName} {{");
            writer.IncreaseIndent();
            if (IsNullableStruct(@struct))
            {
                writer.WriteLineIndent("/// <summary>");
                writer.WriteLineIndent("/// Null value for this structure");
                writer.WriteLineIndent("/// </summary>");
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
            if (IsConstrainedDefault(@struct))
            {
                writer.WriteLineIndent("/// <summary>");
                writer.WriteLineIndent("/// Default value for this structure");
                writer.WriteLineIndent("/// </summary>");
                writer.WriteLineIndent(
                    $"public static readonly {@struct.TypeName} Default = new {@struct.TypeName}() {{");
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
                            case VkStructOrUnion s:
                                if (IsConstrainedDefault(s))
                                    writer.WriteLineIndent(
                                        $"{SanitizeStructMemberName(member.Name)} = {type.TypeName}.Default,");
                                break;
                            default:
                                if (member.PossibleValueExpressions != null &&
                                    member.PossibleValueExpressions.Count > 0)
                                {
                                    var expr = SubstituteConstantExpression(member.PossibleValueExpressions[0], true);
                                    writer.WriteLineIndent($"{SanitizeStructMemberName(member.Name)} = {expr},");
                                }
                                break;
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
                    {
                        var fixedBufferSize = SubstituteConstantExpression(cst.FixedBufferSize, true);
                        writer.WriteLineIndent(
                            $"public fixed {MemberTypeWithPtr(cst)} {name}[{fixedBufferSize}];");
                        if (cst.TypeName.Equals("char"))
                        {
                            writer.WriteLine();
                            EmitComment(writer, cst.Comment, null, null);
                            writer.WriteLineIndent($"public string {name}String {{");
                            writer.IncreaseIndent();
                            writer.WriteLineIndent("get");
                            writer.WriteLineIndent("{");
                            writer.IncreaseIndent();
                            writer.WriteLineIndent($"fixed (byte* ptr = {name}) {{");
                            writer.IncreaseIndent();
                            writer.WriteLineIndent("return Marshal.PtrToStringAnsi(new IntPtr(ptr));");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                            writer.WriteLineIndent("set");
                            writer.WriteLineIndent("{");
                            writer.IncreaseIndent();
                            writer.WriteLineIndent($"fixed (byte* ptr = {name}) {{");
                            writer.IncreaseIndent();
                            writer.WriteLineIndent($"var data = System.Text.Encoding.ASCII.GetBytes(value);");
                            writer.WriteLineIndent($"var count = Math.Min(data.Length, {fixedBufferSize} - 1);");
                            writer.WriteLineIndent($"Marshal.Copy(data, 0, new IntPtr(ptr), count);");
                            writer.WriteLineIndent($"ptr[count] = 0;");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                            writer.DecreaseIndent();
                            writer.WriteLineIndent("}");
                        }
                    }
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
            return SanitizeVariableName(char.ToUpper(name[0]) + name.Substring(1));
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
                foreach (var line in RewriteComment(comment).Split("\n"))
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
                        w.Write(RewriteComment(p.Comment).Trim('/', ' ', '\t'));
                    w.WriteLine("</param>");
                }
            if (result != null)
            {
                w.WriteIndent();
                w.Write("/// <returns>");
                if (result.Comment != null)
                    w.Write(RewriteComment(result.Comment).Trim('/', ' ', '\t'));
                w.WriteLine("</returns>");
            }
            if (exceptions == null) return;
            foreach (var except in exceptions)
                if (_vkExceptionTypes.TryGetValue(except, out string exceptionName))
                    w.WriteLineIndent($"/// <exception cref=\"VulkanLibrary.Unmanaged.{exceptionName}\"></exception>");
        }

        private void WriteEnum(TextWriter writer, VkEnum @enum, bool write)
        {
            if (write)
            {
                EmitComment(writer, @enum.Comment, null, null);
                if (@enum.IsBitmask)
                    writer.WriteLineIndent("[Flags]");
            }
            var typeName = GetTypeName(@enum);
            if (write)
            {
                writer.WriteLineIndent($"public enum {typeName} : {GetTypeName(ResolveType(@enum.BackingType))} {{");
                writer.IncreaseIndent();
            }
            foreach (var cst in @enum.Values)
            {
                var newName =
                    AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), @enum.TypeName));
                if (newName.EndsWith("Bit") && @enum.IsBitmask)
                    newName = newName.Substring(0, newName.Length - 3);
                _constantLookupTable[cst] = typeName + "." + newName;
                if (write)
                {
                    EmitComment(writer, cst.Comment, null, null);
                    if (cst.Extension != null)
                        RequiresExtension(writer, cst.Extension);
                    writer.WriteLineIndent($"{newName} = {SubstituteConstantExpression(cst.Expression, true)},");
                }
            }
            if (write && @enum.IsBitmask)
            {
                if (!_constantLookupTable.ContainsValue(typeName + ".None"))
                {
                    writer.WriteLineIndent("/// <summary>");
                    writer.WriteLineIndent("/// No bits");
                    writer.WriteLineIndent("/// </summary>");
                    writer.WriteLineIndent($"None = 0,");
                }
                if (@enum.Values.Any(x => x.Extension == null))
                {
                    writer.WriteLineIndent("/// <summary>");
                    writer.WriteLineIndent("/// All bits, except extensions");
                    writer.WriteLineIndent("/// </summary>");
                    writer.WriteIndent();
                    writer.Write("AllExceptExt = ");
                    var first = true;
                    foreach (var cst in @enum.Values)
                    {
                        if (cst.Extension != null)
                            continue;
                        if (!first)
                            writer.Write(" | ");
                        writer.Write(_constantLookupTable[cst].Substring(typeName.Length + 1));
                        first = false;
                    }
                    writer.WriteLine();
                }
            }
            if (!write)
                return;
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");
            if (!@enum.TypeName.Equals(VkResultType))
                return;
            writer.WriteLineIndent($"public class VkException : Exception {{");
            writer.IncreaseIndent();
            {
                writer.WriteLineIndent("public readonly VkResult Result;");
                writer.WriteLineIndent("public VkException(VkResult result) { Result = result; }");
                writer.WriteLineIndent("public static VkException Create(VkResult result) {");
                writer.IncreaseIndent();
                {
                    writer.WriteLineIndent("if ((int) result >= 0) return null;");
                    writer.WriteLineIndent("switch (result) {");
                    writer.IncreaseIndent();
                    foreach (var cst in @enum.Values)
                    {
                        var res = EvaluateConstantExpr(cst.Expression);
                        if (res < 0)
                        {
                            var newName =
                                AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name),
                                    @enum.TypeName));
                            if (newName.EndsWith("Bit") && @enum.IsBitmask)
                                newName = newName.Substring(0, newName.Length - 3);
                            writer.WriteLineIndent($"case VkResult.{newName}: return new Vk{newName}();");
                        }
                    }
                    writer.WriteLineIndent($"default: return new VkException(result);");
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
                writer.DecreaseIndent();
                writer.WriteLineIndent("}");
                writer.WriteLineIndent("public static VkResult Check(VkResult result) {");
                writer.IncreaseIndent();
                {
                    writer.WriteLineIndent("var except = Create(result);");
                    writer.WriteLineIndent("if (except != null) throw except;");
                    writer.WriteLineIndent("return result;");
                    writer.DecreaseIndent();
                    writer.WriteLineIndent("}");
                }
            }
            writer.DecreaseIndent();
            writer.WriteLineIndent("}");
            _vkExceptionTypes.Clear();
            foreach (var cst in @enum.Values)
            {
                var res = EvaluateConstantExpr(cst.Expression);
                if (res >= 0) continue;
                var newName =
                    AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), @enum.TypeName));
                if (newName.EndsWith("Bit") && @enum.IsBitmask)
                    newName = newName.Substring(0, newName.Length - 3);
                EmitComment(writer, cst.Comment, null, null);
                _vkExceptionTypes.Add(cst.Name, "Vk" + newName);
                if (cst.Extension != null)
                    RequiresExtension(writer, cst.Extension);
                writer.WriteLineIndent($"public class Vk{newName} : VkException {{");
                writer.IncreaseIndent();
                writer.WriteLineIndent($"public Vk{newName}():base(VkResult.{newName}) {{}}");
                writer.DecreaseIndent();
                writer.WriteLineIndent("}");
            }
        }

        private readonly Dictionary<string, string> _vkExceptionTypes = new Dictionary<string, string>();

        private static readonly Regex UnsignedExpressionRegex = new Regex(@"[0-9L]U", RegexOptions.IgnoreCase);
        private static readonly Regex DoubleExpressionRegex = new Regex(@"[0-9.]D", RegexOptions.IgnoreCase);
        private static readonly Regex FloatExpressionRegex = new Regex(@"[0-9.]F", RegexOptions.IgnoreCase);
        private static readonly Regex LongExpressionRegex = new Regex(@"[0-9U]L", RegexOptions.IgnoreCase);

        private static readonly Regex NumericExpressionRegex =
            new Regex(@"^[0-9FLUD.~()-+|&^]*$", RegexOptions.IgnoreCase);

        private void WriteConstant(TextWriter writer, VkConstant cst, bool write)
        {
            var newName =
                AlphabetizeLeadingNumber(RemoveCommonPrefix(UnderscoresToCamelCase(cst.Name), "Vk"));
            _constantLookupTable[cst] = "Vulkan." + newName;
            if (!write)
                return;
            EmitComment(writer, cst.Comment, null, null);
            var expr = SubstituteConstantExpression(cst.Expression, true);
            string valueType = null;
            var unsigned = UnsignedExpressionRegex.Match(expr).Success;
            if (DoubleExpressionRegex.Match(expr).Success)
                valueType = "double";
            else if (FloatExpressionRegex.Match(expr).Success)
                valueType = "float";
            else if (LongExpressionRegex.Match(expr).Success)
                valueType = unsigned ? "ulong" : "long";
            else
                valueType = unsigned ? "uint" : "int";
            if (NumericExpressionRegex.IsMatch(expr))
                writer.WriteLineIndent($"public const {valueType} {newName} = {expr};");
            else
                writer.WriteLineIndent($"public const {valueType} {newName} = ({valueType}) {expr};");
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