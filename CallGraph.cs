using Microsoft.Extensions.Logging;
using System.Text;

namespace CallGraphBuilder
{
    internal class CallGraph
    {
        public HashSet<Node> Nodes { get; } = [];

        public HashSet<Edge> Edges { get; } = [];

        public override string? ToString()
        {
            return $"CallGraph [nodes: {Nodes.Count}, edges: {Edges.Count}]";
        }

        public void PrintOut(ILogger logger)
        {
            StringBuilder sb = new("\n", 128);

            sb.Append("=============== Call Graph ==============\n");
            sb.Append($"Nodes: {Nodes.Count}\n");
            sb.Append($"Edges: {Edges.Count}\n");
            sb.Append("=========================================\n");

            logger.LogInformation(sb.ToString());
        }
    }
}
