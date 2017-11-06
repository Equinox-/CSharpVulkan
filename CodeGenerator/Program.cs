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
        private const string InputDirectory = "layers/";

        public static void Main(string[] args)
        {
            var spec = new Specification.Specification();
            var specParser = new SpecificationReader(spec);
            foreach (var xml in Directory.GetFiles(InputDirectory))
                if (xml.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    specParser.Read(XDocument.Load(xml));

            using (var gen = new OutputGenerator(spec, OutputDirectory, "VulkanLibrary.Generated"))
                gen.Write();
        }
    }
}