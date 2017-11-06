using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeGenerator.Specification;

namespace CodeGenerator
{
    public class SpecificationReaderTxt
    {
        private readonly Specification.Specification _spec;

        public SpecificationReaderTxt(Specification.Specification spec)
        {
            _spec = spec;
        }

        public void Read(TextReader reader)
        {
            VkType activeType = null;
            string prevBlockData = null;
            StringBuilder prevBlockBuilder = new StringBuilder();
            VkMember activeMember = null;
            while (true)
            {
                var line = reader.ReadLine()?.Trim();
                if (line == null)
                    break;
                var includeIndex = line.IndexOf("include:", StringComparison.OrdinalIgnoreCase);
                if (includeIndex >= 0)
                {
                    var endKey = line.LastIndexOf(".txt", StringComparison.OrdinalIgnoreCase);
                    if (endKey < 0) continue;
                    var startKey = line.LastIndexOf('/', endKey);
                    if (startKey < 0) continue;
                    var key = line.Substring(startKey + 1, endKey - startKey - 1);
                    activeType = _spec.TypeDefs.GetValueOrDefault(key);
                    if (!string.IsNullOrWhiteSpace(prevBlockData) && activeType != null && string.IsNullOrWhiteSpace(activeType.Comment))
                        activeType.Comment = prevBlockData;
                    continue;
                }
                if (line.StartsWith("*") || line.Length == 0)
                {
                    prevBlockData = prevBlockBuilder.ToString();
                    if (activeMember != null && activeMember.Comment == null)
                        activeMember.Comment = prevBlockData;
                    prevBlockBuilder.Clear();
                    activeMember = null;
                }
                var pnameIndex = line.IndexOf("pname:", StringComparison.OrdinalIgnoreCase);
                if (pnameIndex >= 0 && line.Substring(0, pnameIndex).Trim() == "*")
                {
                    var memberNameIndex = line.IndexOfAny(new[] {' ', '\t'}, pnameIndex);
                    if (memberNameIndex == -1)
                        continue;
                    var memberName = line.Substring(pnameIndex + 6, memberNameIndex - pnameIndex - 6);
                    prevBlockBuilder.Clear();
                    if (activeType is VkStructOrUnion ss)
                        activeMember = ss.Members.FirstOrDefault(x => x.Name.Equals(memberName));
                    else if (activeType is VkTypeFunction fn)
                        activeMember = fn.Arguments.FirstOrDefault(x => x.Name.Equals(memberName));

                    prevBlockBuilder.Append(line.Substring(memberNameIndex + 1).Trim()).Append(' ');
                }
                else
                {
                    prevBlockBuilder.Append(line).Append(' ');
                }
            }
        }
    }
}