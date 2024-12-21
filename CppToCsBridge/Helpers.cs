using CppAst;
using Microsoft.Extensions.Logging;

namespace BridgeGeneratorTree
{
    namespace Helpers
    {
        static class Annotation
        {
            static class AnnotationId
            {
                public const string Class = "bridge_class";
                public const string Method = "bridge_func";
            }

            public static bool IsAnnotatedWithMethodAttribute(CppAttribute attr)
            {
                return attr.Kind == AttributeKind.AnnotateAttribute && attr.Arguments.Contains(AnnotationId.Method);
            }

            public static bool IsAnnotatedWithClassAttribute(CppAttribute atr)
            {
                return atr.Kind == AttributeKind.AnnotateAttribute && atr.Arguments.Contains(AnnotationId.Class);
            }
        }

        public static class TypeInfo
        {
            public static List<T> GetNodesOfType<T>(ICppContainer container) where T : CppElement
            {
                var results = new List<T>();

                foreach (var child in container.Children())
                {
                    if (child is T nodeOfType) results.Add(nodeOfType);
                    if (child is ICppContainer childContainer) results.AddRange(GetNodesOfType<T>(childContainer));
                }

                return results;
            }

            /// <summary>
            /// Recursively generates the fully qualified name of a CppType, including its namespace.
            /// </summary>
            /// <param name="type">The CppType to generate the name for.</param>
            /// <param name="container">The root container to use to find namespaces.</param>
            /// <returns>The fully qualified name of the type (e.g., std::vector<int>).</returns>
            public static string GetFullQualifiedTypeName(CppType type, ICppContainer container)
            {
                if (type is CppPointerType pointerType)
                {
                    // Handle pointers (e.g., "int*")
                    return GetFullQualifiedTypeName(pointerType.ElementType, container) + "*";
                }
                else if (type is CppReferenceType referenceType)
                {
                    // Handle references (e.g., "int&")
                    return GetFullQualifiedTypeName(referenceType.ElementType, container) + "&";
                }
                else if (type is CppQualifiedType qualifiedType)
                {
                    // Handle const/volatile types (e.g., "const std::string&")
                    string qualifiers = string.Empty;
                    if (qualifiedType.Qualifier == CppTypeQualifier.Const) qualifiers += "const ";
                    if (qualifiedType.Qualifier == CppTypeQualifier.Volatile) qualifiers += "volatile ";

                    string baseTypeName = GetFullQualifiedTypeName(qualifiedType.ElementType, container);
                    return qualifiers + baseTypeName;
                }
                else if (type is CppClass cppClass || type is CppTypedef cppTypedef)
                {
                    // Handle classes and typedefs with namespaces
                    var namespaces = FindNamespacesForType(container, GetTypeName(type));
                    if (namespaces.Count > 0)
                    {
                        var fullNamespace = string.Join("::", namespaces);
                        return $"{fullNamespace}::{GetTypeName(type)}";
                    }

                    return GetTypeName(type);
                }
                else if (type is CppPrimitiveType primitiveType)
                {
                    // Handle primitive types like "int", "float"
                    return primitiveType.Kind.ToString().ToLower();
                }

                // Fallback for unknown types
                return type.ToString();
            }


            /// <summary>
            /// Extracts the base name of a CppType (removes pointers, references, etc.).
            /// </summary>
            private static string GetTypeName(CppType type)
            {
                if (type is CppPointerType pointerType)
                    return GetTypeName(pointerType.ElementType);
                if (type is CppReferenceType referenceType)
                    return GetTypeName(referenceType.ElementType);
                if (type is CppClass cppClass)
                    return cppClass.Name;
                if (type is CppTypedef cppTypedef)
                    return cppTypedef.Name;
                if (type is CppQualifiedType qualifiedType)
                    return qualifiedType.ElementType.ToString();
                return type.ToString();
            }

            /// <summary>
            /// Recursively finds the namespaces for a given type name by walking down the container tree.
            /// </summary>
            private static List<string> FindNamespacesForType(ICppContainer container, string typeName)
            {
                var namespaceStack = new Stack<string>();
                var namespaces = new List<string>();

                bool found = WalkNamespacesForType(container, typeName, namespaceStack, namespaces);
                return namespaces;
            }

            private static bool WalkNamespacesForType(ICppContainer container, string typeName,
                Stack<string> namespaceStack, List<string> namespaces)
            {
                foreach (var child in container.Children())
                {
                    if (child is CppNamespace cppNamespace)
                    {
                        namespaceStack.Push(cppNamespace.Name);
                        bool found = WalkNamespacesForType(cppNamespace, typeName, namespaceStack, namespaces);
                        namespaceStack.Pop();
                        if (found) return true;
                    }
                    else if (child is CppClass cppClass && cppClass.Name == typeName)
                    {
                        namespaces.AddRange(namespaceStack.ToArray());
                        namespaces.Reverse();
                        return true;
                    }
                    else if (child is ICppContainer childContainer)
                    {
                        bool found = WalkNamespacesForType(childContainer, typeName, namespaceStack, namespaces);
                        if (found) return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Helper class to generate a generic ILogger for any given type.
        /// </summary>
        public static class Logging
        {
            public static void LogParseErrors(ILogger logger, CppCompilation tree)
            {
                foreach (var msg in tree.Diagnostics.Messages)
                {
                    logger.LogError("{ErrorMessage}", msg.Text);
                }
            }

            /// <summary>
            /// Creates an instance of ILogger for the specified type using a default logger factory.
            /// </summary>
            /// <typeparam name="T">The type for which the logger will be created.</typeparam>
            /// <returns>An instance of ILogger for the specified type.</returns>
            public static ILogger<T> CreateDefaultLogger<T>()
            {
                var loggerFactory = CreateDefaultLoggerFactory();
                return CreateLogger<T>(loggerFactory);
            }

            /// <summary>
            /// Creates an instance of ILogger for the specified type.
            /// </summary>
            /// <typeparam name="T">The type for which the logger will be created.</typeparam>
            /// <param name="loggerFactory">The logger factory used to create the logger.</param>
            /// <returns>An instance of ILogger for the specified type.</returns>
            private static ILogger<T> CreateLogger<T>(ILoggerFactory loggerFactory)
            {
                if (loggerFactory == null)
                    throw new ArgumentNullException(nameof(loggerFactory), "LoggerFactory cannot be null.");

                return loggerFactory.CreateLogger<T>();
            }

            /// <summary>
            /// Creates a logger factory with a simple console logger provider.
            /// </summary>
            /// <returns>A logger factory with console logging enabled.</returns>
            private static ILoggerFactory CreateDefaultLoggerFactory()
            {
                return LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            }
        }

        static class PathHelpers
        {
            public static string ResolveIncludePath(string headerPath)
            {
                var parts = headerPath.Split(new[] { "include/" }, StringSplitOptions.None);
                return parts.Length > 1 ? parts[1] : Path.GetFileName(headerPath);
            }

            public static string NormalizePath(string path)
            {
                return path.Replace('\\', '/');
            }

            public static string ResolveIncludePathWithoutRoot(string headerPath)
            {
                // Ensure the input is valid
                if (string.IsNullOrEmpty(headerPath))
                {
                    throw new ArgumentException("Header path cannot be null or empty.", nameof(headerPath));
                }

                // Find the index of "include/impact"
                string targetSegment = NormalizePath("include/impact");
                string normalizedHeaderPath = NormalizePath(headerPath);
                int startIndex = normalizedHeaderPath.IndexOf(targetSegment, StringComparison.Ordinal);

                if (startIndex == -1)
                {
                    return string.Empty;
                }

                // Calculate the start index for the remaining path
                int remainderStartIndex = startIndex + targetSegment.Length;

                // Return the remainder of the path
                return normalizedHeaderPath.Substring(remainderStartIndex).TrimStart('/');
            }

            public static string TrimToAfterIncludeBeforeImpact(string input)
            {
                string rootSection = "include/impact";
                // Find the index of "include/impact"
                int includeImpactIndex = input.IndexOf(rootSection, StringComparison.OrdinalIgnoreCase);

                // If "include/impact" is found, return the substring after it
                if (includeImpactIndex >= 0)
                {
                    // Advance to the end of "impact/"
                    includeImpactIndex += rootSection.Length + 1;
                    return "impact/" + input.Substring(includeImpactIndex);
                }

                // If "include/impact" is not found, return the original string
                return input;
            }

            public static string TrimToAfterIncludeImpact(string input)
            {
                string rootSection = "include/impact";
                // Find the index of "include/impact"
                int includeImpactIndex = input.IndexOf(rootSection, StringComparison.OrdinalIgnoreCase);

                // If "include/impact" is found, return the substring after it
                if (includeImpactIndex >= 0)
                {
                    // Advance to the end of "impact/"
                    includeImpactIndex += rootSection.Length;
                    return input.Substring(includeImpactIndex);
                }

                // If "include/impact" is not found, return the original string
                return ResolveIncludePathWithoutRoot(input);
            }

            public static string ResolveOutputFilePath(string genPath, HeaderDefinition header, ILogger logger)
            {
                string result = NormalizePath(Path.Combine(genPath,
                    ResolveIncludePathWithoutRoot(header.FileOriginPath),
                    (header.FileNameNoExt + "_bridge" + header.FileExt)));
                logger.LogCritical(result);
                return result;
            }
        }
    }
}