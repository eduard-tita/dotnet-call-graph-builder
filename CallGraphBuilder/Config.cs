using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CallGraphBuilder
{
    internal class Config
    {
        private static readonly ILogger<Config> logger;

        static Config()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            logger = loggerFactory.CreateLogger<Config>();
        }

        public required string BinaryPath { get; set; }
        public List<string> Namespaces { get; set; } = [];
        public required string Algorithm { get; set; }
        public required string EntrypointStrategy { get; set; }
        public required string JsonOutputPath { get; set; }

        public static async Task<Config> LoadFromFileAsync(string configFilePath)
        {
            configFilePath = Path.GetFullPath(configFilePath);

            if (!File.Exists(configFilePath))
            {
                var msg = $"Configuration file not found: {configFilePath}";
                logger.LogError(msg);
                throw new FileNotFoundException(msg);
            }
            string jsonString = await File.ReadAllTextAsync(configFilePath);
            var config = JsonSerializer.Deserialize<Config>(jsonString) ?? throw new InvalidOperationException("Failed to deserialize configuration.");

            if (config == null || string.IsNullOrEmpty(config.BinaryPath))
            {
                var msg = "Invalid configuration file.";
                logger.LogError(msg);
                throw new IOException(msg);                
            }
            return config;
        }        
    }
}
