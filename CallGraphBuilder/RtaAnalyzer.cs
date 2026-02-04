using Mono.Cecil;

namespace CallGraphBuilder
{
    /// <summary>
    /// Rapid Type Analysis (RTA) analyzer.
    /// Unlike CHA which considers all subtypes in the hierarchy, RTA only considers
    /// types that are actually instantiated in the program (via newobj instructions).
    /// This produces a more precise call graph with fewer false positive edges.
    /// </summary>
    internal class RtaAnalyzer : Analyzer
    {
        public RtaAnalyzer(CallGraph callGraph, Queue<MethodDefinition> methodQueue, IList<ModuleDefinition> allModules)
            : base(callGraph, methodQueue, allModules) { }

        protected override void ApplyAlgorithm(MethodDefinition targetMethod, Node callerMethodNode)
        {
            // Use dictionary keyed by FullName to prevent duplicate methods (e.g., from diamond inheritance)
            var possibleMethods = new Dictionary<string, MethodDefinition>();

            // Add the target method if it's concrete AND its declaring type is instantiated:
            // - Non-abstract class methods where the class is instantiated
            // - Default interface methods (C# 8+) - included if any implementing type is instantiated
            if (!targetMethod.IsAbstract && !targetMethod.DeclaringType.IsInterface)
            {
                if (IsTypeOrSubtypeInstantiated(targetMethod.DeclaringType))
                {
                    possibleMethods[targetMethod.FullName] = targetMethod;
                }
            }

            if (targetMethod.DeclaringType.IsInterface)
            {
                // Find all implementations in classes that implement the interface AND are instantiated
                var implementingTypes = GetAllImplementingTypes(targetMethod.DeclaringType);
                foreach (var implementingType in implementingTypes)
                {
                    // Only consider types that are instantiated (or have instantiated subtypes)
                    if (!IsTypeOrSubtypeInstantiated(implementingType))
                        continue;

                    // Check for both implicit implementations (name match) and explicit implementations (Overrides)
                    var interfaceMethod = implementingType.Methods.FirstOrDefault(m =>
                        !m.IsAbstract && (MatchesSignature(m, targetMethod) || IsExplicitImplementation(m, targetMethod)));

                    if (interfaceMethod != null)
                    {
                        possibleMethods[interfaceMethod.FullName] = interfaceMethod;
                    }
                    else if (!targetMethod.IsAbstract && targetMethod.HasBody)
                    {
                        // Default interface method - add it if no implementation found
                        possibleMethods[targetMethod.FullName] = targetMethod;
                    }
                }
            }
            else if (targetMethod.IsVirtual)
            {
                // Find all overrides in derived classes that are instantiated
                var derivedTypes = GetAllDerivedTypes(targetMethod.DeclaringType);
                foreach (var derivedType in derivedTypes)
                {
                    // Only consider types that are instantiated (or have instantiated subtypes)
                    if (!IsTypeOrSubtypeInstantiated(derivedType))
                        continue;

                    // Check !m.IsNewSlot to exclude methods using 'new' keyword (method hiding) vs 'override'
                    var overrideMethod = derivedType.Methods.FirstOrDefault(m =>
                        m.IsVirtual && !m.IsAbstract && !m.IsNewSlot && MatchesSignature(m, targetMethod));

                    if (overrideMethod != null)
                    {
                        possibleMethods[overrideMethod.FullName] = overrideMethod;
                    }
                    else
                    {
                        // No override in this type - the base method will be called
                        // Find the actual method that will be invoked by walking up the hierarchy
                        var actualMethod = FindInheritedMethod(derivedType, targetMethod);
                        if (actualMethod != null)
                        {
                            possibleMethods[actualMethod.FullName] = actualMethod;
                        }
                    }
                }
            }

            foreach (var possibleMethod in possibleMethods.Values)
            {
                var edge = CreateMethodEdge(callerMethodNode.Signature, possibleMethod.FullName);
                CallGraph.Edges.Add(edge);

                if (!signatures.Contains(possibleMethod.FullName))
                {
                    MethodQueue.Enqueue(possibleMethod);
                }
            }
        }

        /// <summary>
        /// Checks if a type or any of its subtypes is instantiated.
        /// This is needed because if SubClass is instantiated and inherits from BaseClass,
        /// a call to BaseClass.Method() could resolve to SubClass at runtime.
        /// </summary>
        private bool IsTypeOrSubtypeInstantiated(TypeDefinition type)
        {
            // Check if this type itself is instantiated
            if (InstantiatedTypes.Contains(type.FullName))
                return true;

            // Check if any derived type is instantiated
            var derivedTypes = GetAllDerivedTypes(type);
            foreach (var derivedType in derivedTypes)
            {
                if (InstantiatedTypes.Contains(derivedType.FullName))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the actual method that will be invoked for a type that doesn't override the target method.
        /// Walks up the inheritance hierarchy to find the nearest implementation.
        /// </summary>
        private static MethodDefinition? FindInheritedMethod(TypeDefinition type, MethodDefinition targetMethod)
        {
            var currentType = type.BaseType?.Resolve();
            while (currentType != null)
            {
                var method = currentType.Methods.FirstOrDefault(m =>
                    m.IsVirtual && !m.IsAbstract && MatchesSignature(m, targetMethod));

                if (method != null)
                    return method;

                currentType = currentType.BaseType?.Resolve();
            }
            return null;
        }
    }
}
