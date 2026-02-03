using Microsoft.Extensions.Logging;
using Mono.Cecil;

namespace CallGraphBuilder
{
    internal class Workspace
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

        public Config? Config { get; set; }

        public IEnumerable<string> AssemblyFiles { get; set; } = [];

        private readonly IList<ModuleDefinition> moduleDefinitions = [];

        private readonly Queue<MethodDefinition> methodQueue = new Queue<MethodDefinition>();

        public void Initialize(Config config)
        {
            Config = config;

            if (!Directory.Exists(config.BinaryPath))
            {
                logger.LogError($"Directory not found: {config.BinaryPath}.");
                throw new FileNotFoundException($"Directory not found: {config.BinaryPath}.");
            }

            AssemblyFiles = Directory.GetFiles(config.BinaryPath, "*.*", SearchOption.AllDirectories).Where(file => file.EndsWith(".dll"));

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
                ? new ChaAnalyzer(callGraph, methodQueue, moduleDefinitions)
                : new RtaAnalyzer(callGraph, methodQueue, moduleDefinitions);
            
            BuildCallGraph(analyzer, callGraph);

            return callGraph;
        }

        private CallGraph BuildCallGraph(Analyzer analyzer, CallGraph callGraph)
        {
            while (methodQueue.Count > 0) { 
                var methodDefinition = methodQueue.Dequeue();
                analyzer.InspectMethod(methodDefinition, callGraph);
            }
            return analyzer.CallGraph;
        }

        private void DetermineEntryPoints()
        {
            foreach (var md in moduleDefinitions)
            {
                if (Config?.EntrypointStrategy == EntrypointStrategy.DOTNET_MAIN)
                {
                    MethodDefinition? entryPoint = md.EntryPoint;
                    if (entryPoint is null) continue;
                    methodQueue.Enqueue(entryPoint);
                }
                else
                {
                    // TODO implement all other EntrypointStrategy values
                    // we have to inspect all types
                }
            }

            foreach (MethodDefinition item in methodQueue)
            {
                logger.LogInformation($"in queue: {item.FullName}");
            }
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
