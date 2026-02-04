using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CallGraphBuilder
{
    class Program
    {
        private static readonly ILogger<Program> logger;

        static Program()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            logger = loggerFactory.CreateLogger<Program>();
        }

        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                logger.LogInformation("Usage: CallGraphBuilder <config.json>\n  <config.json>  Path to the configuration file (can be a relative path)");
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            string configFilePath = args[0];
            var config = await Config.LoadFromFileAsync(configFilePath);
            
            Workspace workspace = new(config);
            workspace.Initialize();

            CallGraph callGraph = workspace.BuildCallGraph(); 
            
            WriteCallGraphToJson(callGraph, config.JsonOutputPath);

            // Useful for visualization, but you have to limit the number of edges exported; comment it out if not needed
            WriteCallGraphToDot(callGraph, config.JsonOutputPath, 50);

            var stats = new Statistics(config, callGraph, stopwatch.Elapsed);
            stats.PrintOut(logger);
        }

        private static void WriteCallGraphToDot(CallGraph callGraph, string filePath, int edgeCount)
        {
            string dotFilePath = Path.GetFullPath(filePath);
            if (dotFilePath.LastIndexOf('\\') > 0)
            {
                dotFilePath = dotFilePath.Substring(0, dotFilePath.LastIndexOf('\\')) + "\\graph.dot";
            }
            else
            {
                dotFilePath = dotFilePath.Substring(0, dotFilePath.LastIndexOf('/')) + "/graph.dot";
            }
            logger.LogInformation($"Writing results to {dotFilePath}...");

            StringBuilder dot = new("digraph G {\n");
            int index = 0;            
            var en = callGraph.Edges.GetEnumerator();
            bool hasNext = en.MoveNext();
            while (hasNext && index < edgeCount)
            {
                var edge = en.Current;
                var n1 = edge.Caller.Substring(edge.Caller.IndexOf(' ') + 1);
                n1 = n1.Substring(0, n1.LastIndexOf('(')) + "()";
                var n2 = edge.Callee.Substring(edge.Callee.IndexOf(' ') + 1);
                n2 = n2.Substring(0, n2.LastIndexOf('(')) + "()";
                
                dot.AppendLine($"\"{n1}\" -> \"{n2}\"");

                hasNext = en.MoveNext();
                index++;
            }
            dot.AppendLine("}");
            File.WriteAllText(dotFilePath, dot.ToString());
        }

        private static void WriteCallGraphToJson(CallGraph callGraph, string filePath)
        {
            logger.LogInformation($"Writing results to {filePath}...");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(callGraph, options);
            File.WriteAllText(filePath, json);
        }
    }
}
