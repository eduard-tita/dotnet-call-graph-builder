using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CallGraphBuilder
{
    internal abstract class Analyzer
    {
        private static readonly ILogger<Analyzer> logger;

        static Analyzer()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            logger = loggerFactory.CreateLogger<Analyzer>();
        }        

        public CallGraph CallGraph { get; set; }

        public Queue<MethodDefinition> MethodQueue { get; set; }

        protected readonly ISet<string> signatures = new HashSet<string>();

        protected readonly IList<ModuleDefinition> AllModules;

        protected Analyzer(CallGraph callGraph, Queue<MethodDefinition> methodQueue, IList<ModuleDefinition> allModules)
        {
            CallGraph = callGraph;
            MethodQueue = methodQueue;
            AllModules = allModules;
        }

        internal void InspectMethod(MethodDefinition method, CallGraph callGraph)
        {
            logger.LogInformation($"Analyzing method: {method.FullName}");
            if (method.Body == null)
            {
                return; // Skipping method without body
            }

            string methodIdentifier = method.FullName;
            if (signatures.Contains(methodIdentifier))
            {
                return; // Method already inspected
            }
            signatures.Add(methodIdentifier); // add to inspected signatures

            var methodNode = CreateMethodNode(method);
            callGraph.Nodes.Add(methodNode);

            // Handle async and iterator methods: create edge to MoveNext and queue it for analysis
            // Both async (async/await) and iterator (yield return) methods generate state machines
            if (TryGetStateMachineType(method, out var stateMachineType))
            {
                var moveNextMethod = stateMachineType?.Methods.FirstOrDefault(m => m.Name == "MoveNext");
                if (moveNextMethod != null && moveNextMethod.HasBody)
                {
                    // Create explicit edge from async/iterator method to its state machine's MoveNext
                    var edge = CreateMethodEdge(methodIdentifier, moveNextMethod.FullName);
                    CallGraph.Edges.Add(edge);

                    // Queue MoveNext for separate analysis
                    if (!signatures.Contains(moveNextMethod.FullName))
                    {
                        MethodQueue.Enqueue(moveNextMethod);
                    }
                }
            }

            var instructions = GetMethodInstructions(method);

            foreach (var instruction in instructions)
            {
                if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Ldftn)
                {
                    // Handle regular method calls and loading method pointer (typically for delegate creation)
                    var methodReference = (MethodReference)instruction.Operand;
                    logger.LogInformation($"  {instruction.OpCode}: {methodReference.FullName}");

                    var targetMethodDefinition = methodReference.Resolve();
                    if (targetMethodDefinition == null)
                    {
                        logger.LogError($"Failed to resolve method: {methodReference.FullName}");
                        continue;
                    }

                    var edge = CreateMethodEdge(methodIdentifier, targetMethodDefinition.FullName);
                    CallGraph.Edges.Add(edge);

                    MethodQueue.Enqueue(targetMethodDefinition);
                }
                else if (instruction.OpCode == OpCodes.Callvirt || instruction.OpCode == OpCodes.Ldvirtftn)
                {
                    // Handle virtual method calls and loading virtual method pointer (typically for delegate creation)
                    var methodReference = (MethodReference)instruction.Operand;
                    logger.LogInformation($"  {instruction.OpCode}: {methodReference.FullName}");

                    var targetMethodDefinition = methodReference.Resolve();
                    if (targetMethodDefinition == null)
                    {
                        logger.LogError($"Failed to resolve method: {methodReference.FullName}");
                        continue;
                    }

                    ApplyAlgorithm(targetMethodDefinition, methodNode);                    
                }
                else if (instruction.OpCode == OpCodes.Newobj)
                {
                    // Handle constructor calls (object instantiation)
                    var constructorReference = (MethodReference)instruction.Operand;
                    logger.LogInformation($"  {instruction.OpCode}: {constructorReference.FullName}");

                    var constructorDefinition = constructorReference.Resolve();
                    if (constructorDefinition == null)
                    {
                        logger.LogError($"Failed to resolve constructor: {constructorReference.FullName}");
                        continue;
                    }

                    // TODO fix this
                    // RTA: Track instantiated type
                    //var instantiatedType = constructorDefinition.DeclaringType;
                    //_instantiatedTypes.Add(instantiatedType); // internally guarded by a Contains() call                    

                    // Constructors are not virtual - add directly
                    var edge = CreateMethodEdge(methodIdentifier, constructorDefinition.FullName);
                    CallGraph.Edges.Add(edge);

                    MethodQueue.Enqueue(constructorDefinition);
                }
            }
        }

        protected abstract void ApplyAlgorithm(MethodDefinition targetMethod, Node callerMethodNode);

        private static IEnumerable<Instruction> GetMethodInstructions(MethodDefinition method)
        {
            // Return the method's own instructions
            // For async methods, MoveNext is queued separately in InspectMethod
            if (method.HasBody)
            {
                return method.Body.Instructions;
            }

            return [];
        }

        /// <summary>
        /// Checks if a method uses a state machine (async or iterator) and returns the state machine type.
        /// Async methods have AsyncStateMachineAttribute, iterator methods have IteratorStateMachineAttribute.
        /// </summary>
        private static bool TryGetStateMachineType(MethodDefinition method, out TypeDefinition? stateMachineType)
        {
            stateMachineType = null;
            foreach (var customAttribute in method.CustomAttributes)
            {
                var attrName = customAttribute.AttributeType.FullName;
                if (attrName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute" ||
                    attrName == "System.Runtime.CompilerServices.IteratorStateMachineAttribute")
                {
                    if (customAttribute.ConstructorArguments.Count > 0)
                    {
                        stateMachineType = customAttribute.ConstructorArguments[0].Value as TypeDefinition;
                        return stateMachineType != null;
                    }
                }
            }
            return false;
        }

        protected IEnumerable<TypeDefinition> GetAllImplementingTypes(TypeDefinition interfaceType)
        {
            var implementingTypes = new List<TypeDefinition>();

            // Iterate through all types in all loaded modules (including nested types)
            foreach (var module in AllModules)
            {
                foreach (var type in GetAllTypesRecursive(module.Types))
                {
                    if (ImplementsInterface(type, interfaceType))
                    {
                        implementingTypes.Add(type);
                    }
                }
            }

            return implementingTypes;
        }

        private static bool ImplementsInterface(TypeDefinition type, TypeDefinition interfaceType)
        {
            // Check direct interfaces
            foreach (var iFace in type.Interfaces)
            {
                var resolvedInterface = iFace.InterfaceType.Resolve();
                // Compare by FullName to handle types resolved from different modules
                if (resolvedInterface?.FullName == interfaceType.FullName)
                {
                    return true;
                }
                // Check if this interface inherits from the target interface
                if (resolvedInterface != null && ImplementsInterface(resolvedInterface, interfaceType))
                {
                    return true;
                }
            }
            // Check base type's interfaces
            var baseType = type.BaseType?.Resolve();
            if (baseType != null && ImplementsInterface(baseType, interfaceType))
            {
                return true;
            }
            return false;
        }

        protected static bool MatchesSignature(MethodDefinition method, MethodDefinition targetMethod)
        {
            // Check method name
            if (method.Name != targetMethod.Name)
                return false;

            // Check return type (allowing covariant returns - C# 9+)
            if (!IsReturnTypeCompatible(method.ReturnType, targetMethod.ReturnType))
                return false;

            // Check parameter count
            if (method.Parameters.Count != targetMethod.Parameters.Count)
                return false;

            // Check each parameter type
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                if (method.Parameters[i].ParameterType.FullName != targetMethod.Parameters[i].ParameterType.FullName)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the return type is compatible, supporting covariant returns (C# 9+).
        /// A method's return type is compatible if it's the same as or derives from the target's return type.
        /// </summary>
        private static bool IsReturnTypeCompatible(TypeReference methodReturnType, TypeReference targetReturnType)
        {
            // Exact match
            if (methodReturnType.FullName == targetReturnType.FullName)
                return true;

            // Check for covariant return (method returns a more derived type)
            var methodReturnDef = methodReturnType.Resolve();
            var targetReturnDef = targetReturnType.Resolve();

            if (methodReturnDef == null || targetReturnDef == null)
                return false;

            // Check if method's return type derives from target's return type
            return IsDerivedFromByName(methodReturnDef, targetReturnDef.FullName);
        }

        /// <summary>
        /// Checks if a type derives from a base type identified by FullName.
        /// </summary>
        private static bool IsDerivedFromByName(TypeDefinition type, string baseTypeFullName)
        {
            var currentType = type.BaseType?.Resolve();
            while (currentType != null)
            {
                if (currentType.FullName == baseTypeFullName)
                    return true;
                currentType = currentType.BaseType?.Resolve();
            }
            return false;
        }

        /// <summary>
        /// Checks if a method is an explicit implementation of an interface method.
        /// Explicit implementations have names like "IFoo.Bar" and use the Overrides collection.
        /// </summary>
        protected static bool IsExplicitImplementation(MethodDefinition method, MethodDefinition interfaceMethod)
        {
            if (!method.HasOverrides)
                return false;

            foreach (var overrideRef in method.Overrides)
            {
                var resolved = overrideRef.Resolve();
                if (resolved != null && resolved.FullName == interfaceMethod.FullName)
                    return true;

                // Fallback: compare by full name if resolution fails
                if (overrideRef.FullName == interfaceMethod.FullName)
                    return true;
            }

            return false;
        }

        protected IEnumerable<TypeDefinition> GetAllDerivedTypes(TypeDefinition baseType)
        {
            var derivedTypes = new List<TypeDefinition>();

            // Iterate through all types in all loaded modules (including nested types)
            foreach (var module in AllModules)
            {
                foreach (var type in GetAllTypesRecursive(module.Types))
                {
                    if (IsDerivedFrom(type, baseType))
                    {
                        derivedTypes.Add(type);
                    }
                }
            }

            return derivedTypes;
        }

        private static bool IsDerivedFrom(TypeDefinition type, TypeDefinition baseType)
        {
            var currentType = type.BaseType?.Resolve();
            while (currentType != null)
            {
                // Compare by FullName to handle types resolved from different modules
                if (currentType.FullName == baseType.FullName)
                {
                    return true;
                }
                currentType = currentType.BaseType?.Resolve();
            }
            return false;
        }

        private static IEnumerable<TypeDefinition> GetAllTypesRecursive(IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types)
            {
                yield return type;
                if (type.HasNestedTypes)
                {
                    foreach (var nestedType in GetAllTypesRecursive(type.NestedTypes))
                    {
                        yield return nestedType;
                    }
                }
            }
        }

        protected Node CreateMethodNode(MethodDefinition md)
        {
            return new Node
            {
                Module = md.Module.Name,
                Namespace = md.DeclaringType.Namespace,
                Signature = md.FullName,
                //MethodAttributes = md.Attributes.ToString() // removing for now, but it could be useful later
            };
        }

        protected Edge CreateMethodEdge(string caller, string callee)
        {
            return new Edge
            {
                Caller = caller,
                Callee = callee
            };
        }
    }
}
