using Mono.Cecil;
using System.Linq;

namespace CallGraphBuilder
{
    internal class ChaAnalyzer : Analyzer
    {
        public ChaAnalyzer(CallGraph callGraph, Queue<MethodDefinition> methodQueue, IList<ModuleDefinition> allModules)
            : base(callGraph, methodQueue, allModules) { }

        protected override void ApplyAlgorithm(MethodDefinition targetMethod, Node callerMethodNode)
        {
            // Use dictionary keyed by FullName to prevent duplicate methods (e.g., from diamond inheritance)
            var possibleMethods = new Dictionary<string, MethodDefinition>();

            // Add the target method if it's concrete:
            // - Non-abstract class methods
            // - Default interface methods (C# 8+) - non-abstract interface methods with a body
            if (!targetMethod.IsAbstract && (!targetMethod.DeclaringType.IsInterface || targetMethod.HasBody))
            {
                possibleMethods[targetMethod.FullName] = targetMethod;
            }

            if (targetMethod.DeclaringType.IsInterface)
            {
                // Find all implementations in classes that implement the interface
                var implementingTypes = GetAllImplementingTypes(targetMethod.DeclaringType);
                foreach (var implementingType in implementingTypes)
                {
                    // TODO !m.IsAbstract may need revisiting
                    // Check for both implicit implementations (name match) and explicit implementations (Overrides)
                    var interfaceMethod = implementingType.Methods.FirstOrDefault(m =>
                        !m.IsAbstract && (MatchesSignature(m, targetMethod) || IsExplicitImplementation(m, targetMethod)));
                    if (interfaceMethod != null)
                    {
                        possibleMethods[interfaceMethod.FullName] = interfaceMethod;
                    }
                }
            }
            else if (targetMethod.IsVirtual)
            {
                // Find all overrides in derived classes
                var derivedTypes = GetAllDerivedTypes(targetMethod.DeclaringType);
                foreach (var derivedType in derivedTypes)
                {
                    // TODO !m.IsAbstract may need revisiting
                    // Check !m.IsNewSlot to exclude methods using 'new' keyword (method hiding) vs 'override'
                    var overrideMethod = derivedType.Methods.FirstOrDefault(m => m.IsVirtual && !m.IsAbstract && !m.IsNewSlot && MatchesSignature(m, targetMethod));
                    if (overrideMethod != null)
                    {
                        possibleMethods[overrideMethod.FullName] = overrideMethod;
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
    }
}
