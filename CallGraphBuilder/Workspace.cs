using Microsoft.Extensions.Logging;
using Mono.Cecil;

namespace CallGraphBuilder
{
    internal class Workspace(Config config)
    {
        private static readonly ILogger<Workspace> logger;

        static Workspace()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            logger = loggerFactory.CreateLogger<Workspace>();
        }

        public Config Config { get; set; } = config;

        public IEnumerable<string> AssemblyFiles { get; set; } = [];

        private readonly IList<ModuleDefinition> moduleDefinitions = [];

        private readonly Queue<MethodDefinition> methodQueue = new Queue<MethodDefinition>();

        public void Initialize()
        {
            if (!Directory.Exists(Config.BinaryPath))
            {
                logger.LogError($"Directory not found: {config.BinaryPath}.");
                throw new FileNotFoundException($"Directory not found: {Config.BinaryPath}.");
            }

            AssemblyFiles = Directory.GetFiles(Config.BinaryPath, "*.*", SearchOption.AllDirectories).Where(file => file.EndsWith(".dll"));

            var count = AssemblyFiles.Count();
            if (count == 0)
            {
                logger.LogWarning("Empty assembly list!");
            }
            else
            {
                logger.LogInformation($"Found {count} assemblies.");
            }
        }

        internal CallGraph BuildCallGraph()
        {
            CollectModuleDefinitions();

            DetermineEntryPoints();

            CallGraph callGraph = new();

            Analyzer analyzer = Config?.Algorithm == "CHA"
                ? new ChaAnalyzer(callGraph, methodQueue, moduleDefinitions, Config.Namespaces)
                : new RtaAnalyzer(callGraph, methodQueue, moduleDefinitions, Config.Namespaces);

            BuildCallGraph(analyzer, callGraph);

            return callGraph;
        }

        private void BuildCallGraph(Analyzer analyzer, CallGraph callGraph)
        {
            while (methodQueue.Count > 0) { 
                var methodDefinition = methodQueue.Dequeue();
                analyzer.InspectMethod(methodDefinition, callGraph);
            }
        }

        private void DetermineEntryPoints()
        {
            // Iterate through all loaded modules
            foreach (var md in moduleDefinitions)
            {
                if (Config?.EntrypointStrategy == EntrypointStrategy.DOTNET_MAIN)
                {
                    MethodDefinition? entryPoint = md.EntryPoint;
                    if (entryPoint is null) continue;

                    if (!IsNamespaceMatch(entryPoint.DeclaringType.Namespace)) continue;

                    methodQueue.Enqueue(entryPoint);
                }
                else
                {
                    // Iterate through all types (including nested types)
                    foreach (var type in Analyzer.GetAllTypesRecursive(md.Types))
                    {
                        if (!IsNamespaceMatch(type.Namespace)) continue;

                        // filter out interfaces and attribute classes
                        if (type.IsInterface || IsAttributeType(type)) continue;

                        logger.LogInformation($" >>> Analyzing type: {type.FullName}");
                        foreach (var method in type.Methods)
                        {
                            // filter out property accessor methods(equivalent to Java synthetic methods), if Entrypoint Strategy != ALL
                            if (Config?.EntrypointStrategy != EntrypointStrategy.ALL && (method.IsGetter || method.IsSetter)) continue;

                            if (Config?.EntrypointStrategy == EntrypointStrategy.PUBLIC_CONCRETE)
                            {
                                // Selects public non-abstract/synthetic methods from non-interface/annotation classes
                                if (method.IsPublic && !method.IsAbstract)
                                {
                                    methodQueue.Enqueue(method);
                                }
                            }
                            else if (Config?.EntrypointStrategy == EntrypointStrategy.ACCESSIBLE_CONCRETE)
                            {
                                // Selects public/protected non-abstract/synthetic methods from non-interface/annotation classes
                                if ((method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly) && !method.IsAbstract)
                                {
                                    methodQueue.Enqueue(method);
                                }
                            }
                            else if (Config?.EntrypointStrategy == EntrypointStrategy.CONCRETE)
                            {
                                // Selects all non-abstract/synthetic methods from non-interface/annotation classes
                                if (!method.IsAbstract)
                                {
                                    methodQueue.Enqueue(method);
                                }
                            }
                            else // EntrypointStrategy.ALL
                            {
                                // Selects all methods from all non-interface/annotation classes
                                methodQueue.Enqueue(method);
                            }
                        }
                    }
                }
            }

            foreach (MethodDefinition item in methodQueue)
            {
                logger.LogInformation($"in queue: {item.FullName}");
            }
        }

        private bool IsNamespaceMatch(string typeNamespace)
        {
            if (Config?.Namespaces is null || Config?.Namespaces.Count == 0) return true;

            foreach (var item in Config?.Namespaces)
            {
                if (typeNamespace == item || typeNamespace.StartsWith(item + '.'))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAttributeType(TypeDefinition type)
        {
            var currentType = type.BaseType?.Resolve();
            while (currentType != null)
            {
                if (currentType.FullName == "System.Attribute")
                {
                    return true;
                }
                currentType = currentType.BaseType?.Resolve();
            }
            return false;
        }

        private void CollectModuleDefinitions()
        {
            var assemblyResolver = new DefaultAssemblyResolver();
            assemblyResolver.AddSearchDirectory(Config?.BinaryPath);            

            foreach (var assemblyFile in AssemblyFiles)
            {

                logger.LogInformation($" ::: Analyzing assembly: {assemblyFile}");

                AssemblyDefinition? assembly = null;
                try
                {
                    assembly = AssemblyDefinition.ReadAssembly(assemblyFile, new ReaderParameters { AssemblyResolver = assemblyResolver });
                }
                catch (BadImageFormatException ex)
                {
                    logger.LogError($"Failed to read assembly: {assemblyFile}. Exception: {ex.Message}");
                    return;
                }

                if (assembly.Modules.Count != 1)
                {
                    logger.LogError($"Unexpected number of modules in assembly: {assembly.Modules.Count}");
                    logger.LogError($"Modules in assembly: {string.Join(", ", assembly.Modules.Select(m => m.Name))}");
                }
                moduleDefinitions.Add(assembly.MainModule);
            }
        }
    }
}
