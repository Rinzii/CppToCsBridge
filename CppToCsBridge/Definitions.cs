using System.Diagnostics;
using CppAst;
using Microsoft.Extensions.Logging;

namespace BridgeGeneratorTree
{
    public static class DefHelpers
    {
        public static List<MethodDefinition> ExtractMethodsFromCppType<T>(T cppType, ILogger logger)
            where T : CppType
        {
            Debug.Assert(typeof(T) != typeof(CppFunction), nameof(cppType) + " != CppFunction");

            var results = new List<MethodDefinition>();
            if (typeof(T) == typeof(CppClass))
            {
                
                var cppInput  = cppType as CppClass;
                
                Debug.Assert(cppInput != null, nameof(cppInput) + " != null");
                results = ExtractMethodsFromClass(cppInput, logger);
            }
            else if (typeof(T) == typeof(CppNamespace))
            {
                
            }
            return results;
        }
        
        private static List<MethodDefinition> ExtractMethodsFromClass(CppClass astClass, ILogger logger)
        {
            var results = new List<MethodDefinition>();
            foreach (var method in astClass.Functions)
            {
                foreach (var attr in method.Attributes)
                {
                    if (Helpers.Annotation.IsAnnotatedWithMethodAttribute(attr))
                    {
                        results.Add(new MethodDefinition(method, logger));
                    }
                }
            }

            return results;
        }
    }
    
    public interface IDefinitions; // Opaque interface for easy type checking
    
    // ================================
    // HeaderDefinition Class
    // ================================
    public class HeaderDefinition : IDefinitions
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileExt { get; set; }
        public string FileNameNoExt { get; set; }
        public string FileOriginPath { get; set; }
        public CppCompilation Compilation { get; set; }
        public List<ClassDefinition> Classes { get; set; }

        public bool HasValidClass => Classes.Any();

        public HeaderDefinition(string filePath, CppParserOptions parserOptions, ILogger logger)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            FileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            FileExt = Path.GetExtension(filePath);
            Compilation = CppParser.ParseFile(filePath, parserOptions);
            FileOriginPath = ResolveFileOriginPath(filePath);
            Classes = ParseClasses(logger, Compilation);
        }

        private string ResolveFileOriginPath(string filePath)
        {
            return Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException();
        }

        private static List<ClassDefinition> ParseClasses(ILogger logger, CppCompilation tree)
        {
            var classes = new List<ClassDefinition>();

            if (tree.HasErrors)
            {
                Helpers.Logging.LogParseErrors(logger, tree);
                return classes;
            }

            var allClassesInAst = Helpers.TypeInfo.GetNodesOfType<CppClass>(tree);

            foreach (var cls in allClassesInAst)
            {
                foreach (var atr in cls.Attributes)
                {
                    if (Helpers.Annotation.IsAnnotatedWithClassAttribute(atr))
                    {
                        classes.Add(new ClassDefinition(cls, logger));
                    }
                }
            }


            return classes;
        }
    }

    // ================================
    // NamespaceDefinition Class
    // ================================
    public class NamespaceDefinition(CppNamespace type, string name) : IDefinitions
    {
        public CppNamespace Type { get; set; } = type;
        public string Name { get; set; } = name;
    }

    // ================================
    // NamespaceDefinition Class
    // ================================
    public class ClassDefinition : IDefinitions
    {
        public CppClass Type { get; set; }
        public string Name { get; set; }
        public List<MethodDefinition> Methods { get; set; }

        public List<NamespaceDefinition> Namespaces { get; set; }


        public ClassDefinition(CppClass astClass, ILogger logger)
        {
            Type = astClass;
            Name = astClass.Name;
            Methods = DefHelpers.ExtractMethodsFromCppType(astClass, logger);
        }

        public List<NamespaceDefinition> ExtractNamespaces(ICppContainer container, ILogger logger)
        {
            var namespaceStack = new Stack<NamespaceDefinition>();
            var namespaces = new List<NamespaceDefinition>();

            bool found = WalkAndExtractNamespaces(container, Type, namespaceStack, namespaces);

            if (!found)
            {
                // TODO: This kinda outputs a lot and may not be needed. Decide if to remove it later.
                logger.LogWarning($"Class {Type.Name} not found in the provided container.");
            }

            // Correct the ordering of the namespaces.
            namespaces.Reverse();

            return namespaces;
        }

        private bool WalkAndExtractNamespaces(ICppContainer container,
            CppClass targetClass,
            Stack<NamespaceDefinition> namespaceStack,
            List<NamespaceDefinition> namespaces
        )
        {
            foreach (var child in container.Children())
            {
                // If the child is a namespace, enter it
                if (child is CppNamespace cppNamespace)
                {
                    namespaceStack.Push(new NamespaceDefinition(cppNamespace, cppNamespace.Name));

                    // Continue searching inside this namespace
                    bool found = WalkAndExtractNamespaces(cppNamespace, targetClass, namespaceStack, namespaces);

                    // Pop the namespace when we leave
                    namespaceStack.Pop();

                    // If we found the class, we don't need to keep searching
                    if (found) return true;
                }
                // If we find the target class, copy the current namespace path
                else if (child.Equals(targetClass))
                {
                    namespaces.AddRange(namespaceStack);
                    return true;
                }
                // Continue searching if this child is a container (e.g., a namespace or a class with nested members)
                else if (child is ICppContainer childContainer)
                {
                    bool found = WalkAndExtractNamespaces(childContainer, targetClass, namespaceStack, namespaces);
                    if (found) return true;
                }
            }

            return false;
        }
    }

    // ================================
    // NamespaceDefinition Class
    // ================================
    public class MethodDefinition : IDefinitions
    {
        public CppFunction Type { get; set; }
        public CppType ReturnType { get; set; }
        public string Name { get; set; }
        public List<ParameterDefinition> Parameters { get; set; }

        public MethodDefinition(CppFunction astMethod, ILogger logger)
        {
            Type = astMethod;
            ReturnType = astMethod.ReturnType;
            Name = astMethod.Name;
            Parameters = ExtractParametersFromMethod(astMethod, logger);
        }

        private List<ParameterDefinition> ExtractParametersFromMethod(CppFunction astFunc, ILogger logger)
        {
            var results = new List<ParameterDefinition>();
            foreach (var param in astFunc.Parameters)
            {
                results.Add(new ParameterDefinition(param, logger));
            }

            return results;
        }

        public List<NamespaceDefinition> ExtractNamespacesFromParam(ICppContainer container, ILogger logger)
        {
            var namespaceStack = new Stack<NamespaceDefinition>();
            var namespaces = new List<NamespaceDefinition>();

            bool found = WalkAndExtractNamespaces(container, Type, namespaceStack, namespaces);

            if (!found)
            {
                // TODO: This kinda outputs a lot and may not be needed. Decide if to remove it later.
                logger.LogWarning($"Class {Type.Name} not found in the provided container.");
            }

            return namespaces;
        }

        private bool WalkAndExtractNamespaces(ICppContainer container,
            CppFunction targetClass,
            Stack<NamespaceDefinition> namespaceStack,
            List<NamespaceDefinition> namespaces
        )
        {
            foreach (var child in container.Children())
            {
                // If the child is a namespace, enter it
                if (child is CppNamespace cppNamespace)
                {
                    namespaceStack.Push(new NamespaceDefinition(cppNamespace, cppNamespace.Name));

                    // Continue searching inside this namespace
                    bool found = WalkAndExtractNamespaces(cppNamespace, targetClass, namespaceStack, namespaces);

                    // Pop the namespace when we leave
                    namespaceStack.Pop();

                    // If we found the class, we don't need to keep searching
                    if (found) return true;
                }
                // If we find the target class, copy the current namespace path
                else if (child.Equals(targetClass))
                {
                    namespaces.AddRange(namespaceStack);
                    return true;
                }
                // Continue searching if this child is a container (e.g., a namespace or a class with nested members)
                else if (child is ICppContainer childContainer)
                {
                    bool found = WalkAndExtractNamespaces(childContainer, targetClass, namespaceStack, namespaces);
                    if (found) return true;
                }
            }

            return false;
        }
    }

    // ================================
    // NamespaceDefinition Class
    // ================================
    public class ParameterDefinition : IDefinitions
    {
        public CppType Type { get; set; }
        public CppParameter PType { get; set; }

        public string Name { get; set; }
        public List<NamespaceDefinition> Namespaces { get; set; }

        public ParameterDefinition(CppParameter astParameter, ILogger logger)
        {
            PType = astParameter;
            Type = astParameter.Type;
            Name = astParameter.Name;
            //Namespaces = ExtractNamespacesFromParam(astParameter);
        }

        public List<NamespaceDefinition> ExtractNamespacesFromParam(ICppContainer container, ILogger logger)
        {
            var namespaceStack = new Stack<NamespaceDefinition>();
            var namespaces = new List<NamespaceDefinition>();

            // Get the actual type of the parameter (like std::vector<int>)
            var targetTypeName = GetTypeName(PType.Type, logger);
            logger.LogDebug($"Extracting namespaces for parameter type: {targetTypeName}"); // Debug

            // Walk and extract the namespaces for the parameter's actual type
            bool found = WalkAndExtractNamespaces(container, targetTypeName, namespaceStack, namespaces, logger);

            if (!found)
            {
                // TODO: This kinda outputs a lot and may not be needed. Decide if to remove it later.
                logger.LogWarning($"Type {targetTypeName} not found in the provided container.");
            }

            return namespaces;
        }

        private bool WalkAndExtractNamespaces(
            ICppContainer container,
            string targetTypeName,
            Stack<NamespaceDefinition> namespaceStack,
            List<NamespaceDefinition> namespaces,
            ILogger logger
        )
        {
            foreach (var child in container.Children())
            {
                // If the child is a namespace, enter it
                if (child is CppNamespace cppNamespace)
                {
                    namespaceStack.Push(new NamespaceDefinition(cppNamespace, cppNamespace.Name));
                    bool found = WalkAndExtractNamespaces(cppNamespace, targetTypeName, namespaceStack, namespaces, logger);
                    namespaceStack.Pop();
                    if (found) return true;
                }
                // If the child is a CppClass, CppTypedef, or CppContainer, check if its name matches the type name
                else if ((child is CppClass cppClass && cppClass.Name == targetTypeName) ||
                         (child is CppTypedef cppTypedef && cppTypedef.Name == targetTypeName))
                {
                    logger.LogDebug(
                        $"Found type {targetTypeName} in namespace path: {string.Join("::", namespaceStack)}");
                    namespaces.AddRange(namespaceStack);
                    return true;
                }
                // Continue searching if this child is a container (e.g., a namespace or a class with nested members)
                else if (child is ICppContainer childContainer)
                {
                    bool found = WalkAndExtractNamespaces(childContainer, targetTypeName, namespaceStack, namespaces, logger);
                    if (found) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts the base name of a CppType (removes pointers, references, templates, etc.).
        /// </summary>
        /// <param name="type">The CppType to extract the name from.</param>
        /// <returns>The name of the type (without modifiers like *, &, etc.).</returns>
        private string GetTypeName(CppType type, ILogger logger)
        {
            if (type is CppPointerType pointerType)
                return GetTypeName(pointerType.ElementType, logger);
            if (type is CppReferenceType referenceType)
                return GetTypeName(referenceType.ElementType, logger);
            if (type is CppQualifiedType qualifiedType)
            {
                var baseTypeName = GetTypeName(qualifiedType.ElementType, logger);
                logger.LogDebug(
                    $"CppQualifiedType: Qualifier = {qualifiedType.Qualifier}, BaseType = {baseTypeName}");
                return baseTypeName;
            }

            if (type is CppClass cppClass)
                return cppClass.Name;
            if (type is CppTypedef cppTypedef)
                return cppTypedef.Name;

            return type.ToString() ?? string.Empty;
        }
    }

    public static class TypeNameHelper
    {
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

                /*
                // Handle qualified types like std::vector<int>
                string qualifierName = qualifiedType.Qualifier.ToString();
                string baseTypeName = GetFullQualifiedTypeName(qualifiedType.ElementType, container);
                return $"{qualifierName}::{baseTypeName}";
                */
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
                return qualifiedType.ElementType.ToString() ?? string.Empty;
            return type.ToString() ?? string.Empty;
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
            Stack<string> namespaceStack,
            List<string> namespaces)
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
}