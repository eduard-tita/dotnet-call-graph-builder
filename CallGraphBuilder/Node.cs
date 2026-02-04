namespace CallGraphBuilder
{
    internal class Node : IEquatable<Node>
    {
        public required string Module { get; set; }
        
        public required string Namespace { get; set; }
        
        public required string Signature { get; set; }

        //public required string MethodAttributes { get; set; } // removing for now, but it could be useful later


        bool IEquatable<Node>.Equals(Node? other)
        {
            return other is null ? false : Signature == other.Signature;
        }

        public override string? ToString()
        {
            return $"{Signature} (in {Module})";
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Node);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Signature);
        }
    }
}
