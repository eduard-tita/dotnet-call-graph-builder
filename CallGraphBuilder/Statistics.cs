using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CallGraphBuilder
{
    internal class Statistics(Config config, CallGraph callGraph)
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

        public TimeSpan ElapsedTime { get; set; }

        public CallGraph CallGraph { get; set; } = callGraph;

        public Config Config { get; set; } = config;

        /// <summary>
        /// Peak working set (physical memory) in bytes during the process lifetime.
        /// </summary>
        public long PeakWorkingSetBytes { get; set; }

        /// <summary>
        /// Current working set (physical memory) in bytes at the time of measurement.
        /// </summary>
        public long CurrentWorkingSetBytes { get; set; }

        /// <summary>
        /// Managed heap size in bytes (approximate).
        /// </summary>
        public long ManagedHeapBytes { get; set; }

        /// <summary>
        /// Captures current process metrics (elapsed time, memory usage).
        /// </summary>
        public void CaptureMetrics()
        {
            var process = Process.GetCurrentProcess();
            ElapsedTime = DateTime.Now - process.StartTime;
            PeakWorkingSetBytes = process.PeakWorkingSet64;
            CurrentWorkingSetBytes = process.WorkingSet64;
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
        }

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
            sb.AppendLine($"Peak Memory:         {FormatBytes(PeakWorkingSetBytes)}");
            sb.AppendLine($"Current Memory:      {FormatBytes(CurrentWorkingSetBytes)}");
            sb.AppendLine($"Managed Heap:        {FormatBytes(ManagedHeapBytes)}");
            sb.AppendLine("==========================================================");

            logger.LogInformation(sb.ToString());
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F2} GB",
                >= MB => $"{bytes / (double)MB:F2} MB",
                >= KB => $"{bytes / (double)KB:F2} KB",
                _ => $"{bytes} bytes"
            };
        }
    }
}
