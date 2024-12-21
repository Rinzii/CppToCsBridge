using BridgeGeneratorTree.Helpers;
using CppAst;
using Microsoft.Extensions.Logging;

namespace BridgeGeneratorTree
{
    static class CodeGenerator
    {
        public static void GenerateBridge(string outputPath, string rootIncludeDir, string[] rawHeaders, ILogger logger)
        {
            var parserOpt = CreateParserOptions(rootIncludeDir);

            var parsedHeaders = ParseHeaderFiles(parserOpt, rawHeaders, logger);
            
            FileManager.SetupOutputPath(outputPath, logger);
            GenerateCppBridge(outputPath, parsedHeaders, logger);
            
            // TODO: Implement GenerateCSharpBridge()
        }

        private static void GenerateCppBridge(string outputPath, List<HeaderDefinition> headers, ILogger logger)
        {
            string generationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            foreach (var header in headers)
            {
                string filePath = PathHelpers.ResolveOutputFilePath(outputPath, header, logger);
                string filePathDirName = Path.GetDirectoryName(filePath) ?? outputPath;
                string content = GenerateFileContent(header, generationDate, logger);

                if (!Directory.Exists(filePathDirName))
                {
                    FileManager.SetupOutputPath(filePathDirName, logger);
                }

                FileManager.WriteGeneratedFile(filePath, content);
            }
        }
        

        private static string GenerateFileContent(HeaderDefinition header, string generationDate, ILogger logger)
        {
            string includePath = Helpers.PathHelpers.ResolveIncludePath(header.FilePath);

            var writer = new StringWriter();
            writer.WriteLine("// THIS IS GENERATED CODE DO NOT EDIT DIRECTLY");
            writer.WriteLine($"// FILE USED FOR GENERATION: {PathHelpers.TrimToAfterIncludeBeforeImpact(header.FilePath)}");
            writer.WriteLine($"// GENERATION DATE: {generationDate}");
            writer.WriteLine("// clang-format off");
            writer.WriteLine("// NOLINTBEGIN");
            writer.WriteLine("#pragma once");
            writer.WriteLine();
            writer.WriteLine($"#include \"{includePath}\"");
            // TODO: Remove usage of iostream and instead use our builtin logger.
            writer.WriteLine("#include <utility>");

            writer.WriteLine("extern \"C\" {");

            // TODO: Add namespace info to classes specifically
            // TODO: Instead pull this logic into the population logic and have a ClassDefinition
            // TODO: own a tree or something to indicate the order of namespaces to simplify generation.

            foreach (var cls in header.Classes)
            {
                cls.Namespaces = cls.ExtractNamespaces(header.Compilation, logger);
                foreach (var ns in cls.Namespaces)
                {
                    writer.WriteLine($"namespace {ns.Name} {{");
                }

                writer.WriteLine($"typedef void* {cls.Name}Handle;");
                writer.WriteLine(
                    $"inline void* {cls.Name}_Create() {{ return reinterpret_cast<{cls.Name}Handle>(new {cls.Name}()); }}");
                writer.WriteLine(
                    $"inline void {cls.Name}_Destroy({cls.Name}Handle handle) {{ delete reinterpret_cast<{cls.Name}*>(handle); }}");

                writer.WriteLine(
                    $"inline void {cls.Name}_Call({cls.Name}Handle handle, uint32_t methodID, void* param) {{");
                writer.WriteLine($"    auto* instance = reinterpret_cast<{cls.Name}*>(handle);");
                writer.WriteLine("    switch (methodID) {");

                int methodId = 0;
                foreach (var method in cls.Methods)
                {
                    writer.WriteLine($"        case {methodId}:");

                    if (method.Parameters.Count > 0)
                    {
                        string argsTypeName = $"ArgsType_{method.Name}_{methodId}";
                        string argsVariableName = $"args_{method.Name}_{methodId}";

                        //CppQualifiedType
                        writer.Write($"            using {argsTypeName} = std::tuple<");
                        writer.Write(string.Join(", ", method.Parameters.ConvertAll(p =>
                        {
                            // Extract the full namespace for the parameter's type
                            p.Namespaces = p.ExtractNamespacesFromParam(header.Compilation, logger);

                            // Get the fully qualified type (including namespace, pointer, reference, etc.)
                            var fullyQualifiedType =
                                TypeNameHelper.GetFullQualifiedTypeName(p.Type, header.Compilation);

                            // Combine namespaces into a fully qualified type name
                            return fullyQualifiedType;
                        })));


                        writer.WriteLine(">;");
                        writer.WriteLine(
                            $"            auto* {argsVariableName} = reinterpret_cast<{argsTypeName}*>(param);");

                        writer.Write(
                            $"            std::apply([&](auto&&... args) {{ instance->{method.Name}(std::forward<decltype(args)>(args)...); }}, *{argsVariableName});");
                    }
                    else
                    {
                        writer.WriteLine($"            instance->{method.Name}();");
                    }

                    writer.WriteLine("            break;");
                    methodId++;
                }

                writer.WriteLine("        default:");
                writer.WriteLine("            break;");
                writer.WriteLine("    }");
                writer.WriteLine("}");

                foreach (var ns in cls.Namespaces)
                {
                    writer.WriteLine($"}} // namespace {ns.Name}");
                }
            }

            writer.WriteLine("} // extern c");

            writer.WriteLine("// NOLINTEND");
            writer.WriteLine("// clang-format on");
            return writer.ToString();
        }

        private static CppParserOptions CreateParserOptions(string includePath)
        {
            var options = new CppParserOptions
            {
                ParseTokenAttributes = true,
                ParseSystemIncludes = false,
                ParseCommentAttribute = true,
            };
            options.IncludeFolders.Add(includePath);
            options.AdditionalArguments.Add("-std=c++20");
            return options;
        }

        private static List<HeaderDefinition> ParseHeaderFiles(CppParserOptions options, string[] headerFiles,
            ILogger logger)
        {
            List<HeaderDefinition> headers = [];

            foreach (var headerPath in headerFiles)
            {
                if (!File.Exists(headerPath))
                {
                    logger.LogError("Error: File '{HeaderPath}' does not exist.", headerPath);
                    continue;
                }

                headers.Add(new HeaderDefinition(headerPath, options, logger));
            }

            List<HeaderDefinition> validHeaders = new List<HeaderDefinition>();

            foreach (var head in headers)
            {
                if (!head.HasValidClass) continue;

                validHeaders.Add(head);
            }

            return validHeaders;
        }
    }
}