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
    }
}
