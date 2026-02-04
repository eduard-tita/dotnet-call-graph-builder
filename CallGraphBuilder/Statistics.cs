using Microsoft.Extensions.Logging;
using System.Text;

namespace CallGraphBuilder
{
    internal class Statistics(Config config, CallGraph callGraph, TimeSpan elapsedTime)
    {
        private static readonly ILogger<Statistics> logger;

        static Statistics()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            logger = loggerFactory.CreateLogger<Statistics>();
        }

        public TimeSpan ElapsedTime { get; set; } = elapsedTime;

        public CallGraph CallGraph { get; set; } = callGraph;

        public Config Config { get; set; } = config;

        public void PrintOut(ILogger logger)
        {
            StringBuilder sb = new();

            sb.AppendLine(" ");
            sb.AppendLine("==================== Parameters ==========================");
            sb.AppendLine($"Binary Path:         {Config.BinaryPath}");
            sb.AppendLine($"Namespaces:          {string.Join(", ", Config.Namespaces ?? [])}");
            sb.AppendLine($"Algorithm:           {Config.Algorithm}");
            sb.AppendLine($"Entrypoint Strategy: {Config.EntrypointStrategy}");
            sb.AppendLine($"Json Output Path:    {Config.JsonOutputPath}");
            sb.AppendLine("==========================================================");

            sb.AppendLine(" ");
            sb.AppendLine("==================== Results =============================");
            sb.AppendLine($"Graph Nodes:         {CallGraph.Nodes.Count}");
            sb.AppendLine($"Graph Edges:         {CallGraph.Edges.Count}");
            sb.AppendLine($"Elapsed Time:        {ElapsedTime.TotalSeconds:F2} seconds");
            sb.AppendLine("==========================================================");

            logger.LogInformation(sb.ToString());
        }
    }
}
