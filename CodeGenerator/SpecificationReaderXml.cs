using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CodeGenerator.Specification;

namespace CodeGenerator
{
    public class SpecificationReaderXml
    {
        private readonly Specification.Specification _spec;

        public SpecificationReaderXml(Specification.Specification dest)
        {
            _spec = dest;
        }

        public void Read(XDocument doc)
        {
            foreach (var registry in doc.Elements("registry"))
            foreach (var k in registry.Elements())
            {
                if (k.Name.LocalName.Equals("types"))
                    ReadTypes(k);
                else if (k.Name.LocalName.Equals("commands"))
                    ReadCommands(k);
                else if (k.Name.LocalName.Equals("enums"))
                    ReadEnum(k);
            }
        }

        #region Types

        private static string StringifyElement(XElement e)
        {
            if (e.Name.LocalName.Equals("enum"))
                return $"(constant::{e.Value})";
            return e.Value;
        }

        private void ReadTypes(XElement ctr)
        {
            foreach (var def in ctr.Elements("type"))
                ReadType(def);
        }

        private void ReadType(XElement type)
        {
            var category = type.Attribute("category")?.Value;
            if (category == "define")
            {
            }
            else if (category == "struct" || category == "union")
            {
                ReadStructOrUnion(type);
            }
            else if (category == "funcpointer")
            {
                ReadFunctionPointer(type);
            }
            else if (category == "handle")
            {
                var name = type.Element("name")?.Value;
                Debug.Assert(name != null);
                var parent = type.Attribute("parent")?.Value;
                var dispatchable = type.Element("type")?.Value?.Equals("VK_DEFINE_HANDLE") ?? false;
                _spec.Add(new VkHandle(name, dispatchable, null, parent));
            }
            else if (type.Nodes().OfType<XText>()
                .Any(x => x.Value.IndexOf("typedef", StringComparison.OrdinalIgnoreCase) != -1))
            {
                // typedef
                var aliasName = type.Element("name")?.Value;
                var aliasType = type.Element("type")?.Value;
                Debug.Assert(aliasName != null);
                Debug.Assert(aliasType != null);
                _spec.Add(new VkTypeAlias(aliasName, aliasType, null));
            }
        }

        private void ReadFunctionPointer(XElement type)
        {
            var stringbuilder = new StringBuilder();
            List<string> argTypes = new List<string>();
            string funcName = null;
            foreach (var child in type.Nodes())
            {
                switch (child)
                {
                    case XElement e:
                        if (e.Name.LocalName.Equals("name"))
                            funcName = e.Value.Trim();
                        else if (e.Name.LocalName.Equals("type"))
                            argTypes.Add(e.Value.Trim());
                        stringbuilder.Append(StringifyElement(e));
                        break;
                    case XText t:
                        stringbuilder.Append(t.Value);
                        break;
                }
            }
            Debug.Assert(funcName != null);

            var content = stringbuilder.ToString();
            var argumentsReversed = new List<VkMember>();

            #region Parse Arguments

            {
                var i = stringbuilder.Length - 1;
                var paramId = argTypes.Count - 1;
                while (i >= 0 && paramId >= 0)
                {
                    var argEnd = content.LastIndexOfAny(new char[] {')', ','}, i);
                    var argBegin = content.LastIndexOfAny(new char[] {'(', ','}, argEnd - 1);
                    if (argEnd == -1 || argBegin == -1)
                        break;
                    i = argBegin;

                    var paramDesc = content.Substring(argBegin + 1, argEnd - argBegin - 1);
                    var typeName = argTypes[paramId--];
                    var typeData = paramDesc.IndexOf(typeName, StringComparison.Ordinal);
                    Debug.Assert(typeData != -1);
                    var isConstant = paramDesc.LastIndexOf("const", typeData, StringComparison.OrdinalIgnoreCase) != -1;
                    var isOptional = false;
                    var ptrInfo = PointerLevel(paramDesc, 0, typeData) +
                                  PointerLevel(paramDesc, typeData + typeName.Length);
                    var bufferSize = FixedBufferSize(paramDesc, typeData + typeName.Length);

                    var nameBegin = -1;
                    var nameEnd = -1;
                    for (var j = paramDesc.Length - 1; j >= typeData + typeName.Length; j--)
                    {
                        var boundary = paramDesc[j] == ',' || paramDesc[j] == '*' || char.IsWhiteSpace(paramDesc[j]);
                        if (nameEnd == -1 && !boundary)
                            nameEnd = j + 1;
                        else if (nameEnd != -1 && boundary)
                        {
                            nameBegin = j + 1;
                            break;
                        }
                    }
                    Debug.Assert(nameBegin != -1);
                    Debug.Assert(nameEnd != -1);
                    argumentsReversed.Add(new VkMember(paramDesc.Substring(nameBegin, nameEnd - nameBegin), null,
                        typeName,
                        ptrInfo, null, null,
                        bufferSize, isConstant, isOptional));
                }
            }

            #endregion

            VkMember returnType;

            #region Parse Return Type

            {
                var typedefIndex = content.IndexOf("typedef", StringComparison.OrdinalIgnoreCase);
                Debug.Assert(typedefIndex != -1);
                typedefIndex += "typedef".Length;
                var openParen = content.IndexOf('(', typedefIndex);
                Debug.Assert(openParen != -1);
                var returnInfo = content.Substring(typedefIndex, openParen - typedefIndex - 1).Trim();
                var ptrInfo = PointerLevel(returnInfo);
                var typeNoPtr = returnInfo.Trim('*', ' ', '\t', '\r', '\n');
                var fixedBufferSize = FixedBufferSize(typeNoPtr);
                if (fixedBufferSize != null)
                {
                    var openBracket = typeNoPtr.IndexOf('[');
                    Debug.Assert(openBracket != -1);
                    typeNoPtr = typeNoPtr.Substring(0, openBracket);
                }
                returnType = new VkMember("return", null, typeNoPtr.Trim(), ptrInfo, null, null, fixedBufferSize,
                    false, false);
            }

            #endregion

            _spec.Add(new VkTypeFunctionPointer(funcName, returnType, null,
                ((IEnumerable<VkMember>) argumentsReversed).Reverse().ToArray()));
        }

        private static readonly Regex FixedBufferRegex = new Regex(@"\[(.+)\]");

        private static string FixedBufferSize(string tag, int offset = 0, int count = int.MaxValue)
        {
            count = Math.Min(tag.Length - offset, count);
            var res = FixedBufferRegex.Match(tag, offset, count);
            if (!res.Success)
                return null;
            return res.Groups[1].Value;
        }

        private static int PointerLevel(string tag, int offset = 0, int count = int.MaxValue)
        {
            count = Math.Min(tag.Length - offset, count);
            var i = 0;
            for (var j = offset; j < offset + count; j++)
                if (tag[j] == '*')
                    i++;
            return i;
        }

        private void ReadStructOrUnion(XElement @struct)
        {
            var name = @struct.Attribute("name")?.Value;
            Debug.Assert(!string.IsNullOrWhiteSpace(name));
            var members = new List<VkMember>();
            foreach (var m in @struct.Elements("member"))
                members.Add(ReadMember(m));
            var cat = @struct.Attribute("category")?.Value;
            Debug.Assert(cat != null);
            var isUnion = cat.Equals("union");
            if (!isUnion)
                Debug.Assert(cat.Equals("struct"));
            if (isUnion)
                _spec.Add(new VkUnion(name, @struct.Attribute("comment")?.Value, members.ToArray()));
            else
                _spec.Add(new VkStruct(name, @struct.Attribute("comment")?.Value, members.ToArray()));
        }

        private void ReadEnum(XElement @enum)
        {
            var name = @enum.Attribute("name")?.Value;
            Debug.Assert(name != null);
            var enumType = @enum.Attribute("type")?.Value;
            var comment = @enum.Attribute("comment")?.Value;


            var fields = new List<VkConstant>();
            foreach (var child in @enum.Elements("enum"))
            {
                var cname = child.Attribute("name")?.Value;
                Debug.Assert(cname != name);
                var ccomment = child.Attribute("comment")?.Value;
                var bitpos = child.Attribute("bitpos")?.Value;
                var value = child.Attribute("value")?.Value;
                Debug.Assert(value != null || bitpos != null);
                var result = bitpos != null
                    ? VkConstant.AsBitPosition(cname, ccomment, bitpos)
                    : VkConstant.AsValue(cname, ccomment, value);
                fields.Add(result);
                _spec.Add(result);
            }
            if (enumType == null) return; // not an enum, just constants.
            var isBitmask = enumType == "bitmask";
            if (!isBitmask)
                Debug.Assert(enumType == "enum");
            _spec.Add(new VkEnum(name, isBitmask, comment, fields.ToArray()));
        }

        private static VkMember ReadMember(XElement m)
        {
            XElement typeName = null;
            XElement memberName = null;
            string commentExtra = null;
            StringBuilder contentSb = new StringBuilder();
            foreach (var n in m.Nodes())
            {
                switch (n)
                {
                    case XElement el:
                        if (el.Name.LocalName.Equals("type"))
                            typeName = el;
                        else if (el.Name.LocalName.Equals("name"))
                            memberName = el;
                        else if (el.Name.LocalName.Equals("comment"))
                        {
                            commentExtra = el.Value;
                            break;
                        }
                        contentSb.Append(StringifyElement(el));
                        break;
                    case XText t:
                        contentSb.Append(t.Value);
                        break;
                }
            }
            Debug.Assert(typeName != null);
            Debug.Assert(memberName != null);
            var paramDesc = contentSb.ToString();
            var typeData = paramDesc.IndexOf(typeName.Value, StringComparison.Ordinal);
            Debug.Assert(typeData != -1);
            var isConstant = paramDesc.LastIndexOf("const", typeData, StringComparison.OrdinalIgnoreCase) != -1;
            var isOptional = (m.Attribute("optional")?.Value?.IndexOf("true", StringComparison.OrdinalIgnoreCase) ?? -1) != -1;
            var ptrInfo = PointerLevel(paramDesc, 0, typeData) +
                          PointerLevel(paramDesc, typeData + typeName.Value.Length);
            var bufferSize = FixedBufferSize(paramDesc, typeData + typeName.Value.Length);

            var comment = new StringBuilder();
            var commentOnName = memberName.Attribute("comment")?.Value;
            if (commentOnName != null)
                comment.AppendLine(commentOnName);
            var commentOnDeclare = m.Attribute("comment")?.Value;
            if (commentOnDeclare != null)
                comment.AppendLine(commentOnDeclare);
            if (commentExtra != null)
                comment.AppendLine(commentExtra);
            var optional = m.Attribute("optional")?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ??
                           false;
            var annotatedLengths = m.Attribute("len")?.Value?.Split(',');
            var valueExpressions = m.Attribute("values")?.Value?.Split(',');
            return new VkMember(memberName.Value, comment.Length > 0 ? comment.ToString().Trim() : null,
                typeName.Value,
                ptrInfo, valueExpressions, annotatedLengths,
                bufferSize, isConstant, isOptional);
        }

        #endregion

        #region Commands

        private void ReadCommands(XElement e)
        {
            foreach (var child in e.Elements("command"))
                ReadCommand(child);
        }

        private void ReadCommand(XElement cmd)
        {
            var proto = cmd.Element("proto");
            Debug.Assert(proto != null);

            var returnType = ReadMember(proto);

            var members = new List<VkMember>();

            #region Parameters

            foreach (var m in cmd.Elements("param"))
                members.Add(ReadMember(m));

            #endregion

            var comment = cmd.Attribute("command")?.Value;
            var success = cmd.Attribute("successcodes")?.Value?.Split(',');
            var error = cmd.Attribute("errorcodes")?.Value?.Split(',');
            _spec.Add(new VkCommand(returnType.Name, returnType, comment, error, success, members.ToArray()));
        }

        #endregion
    }
}