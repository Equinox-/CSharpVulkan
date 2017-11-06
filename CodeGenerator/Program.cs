using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using CodeGenerator.Specification;
using CodeGenerator.Utils;

namespace CodeGenerator
{
    internal class Program
    {
        private const string OutputDirectory = "../VulkanLibrary/Generated/";
        private const string InputDirectoryXml = "documentation/src/spec/";
        private const string InputDirectoryText = "documentation/doc/specs/vulkan/";

        public static void Main(string[] args)
        {
            var spec = new Specification.Specification();
            {
                var specParserXml = new SpecificationReaderXml(spec);
                foreach (var xml in Directory.GetFiles(InputDirectoryXml))
                    if (xml.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        specParserXml.Read(XDocument.Load(xml));
            }
            SpecificationOverrides.OverrideSpecification(spec);
            {
                var specParserTxt = new SpecificationReaderTxt(spec);
                foreach (var txt in Directory.EnumerateFiles(InputDirectoryText, "*.txt", SearchOption.AllDirectories))
                    using (var stream = File.OpenText(txt))
                        specParserTxt.Read(stream);
            }
            using (var gen = new OutputGenerator(spec, OutputDirectory, "VulkanLibrary"))
                gen.Write();
        }
    }
}