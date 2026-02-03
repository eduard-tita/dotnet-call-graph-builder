namespace CallGraphBuilder
{
    internal class Edge : IEquatable<Edge>
    {
        public required string Caller { get; set; }

        public required string Callee { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Edge);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Caller, Callee);
        }

        bool IEquatable<Edge>.Equals(Edge? other)
        {
            return other is null ? false : Caller == other.Caller && Callee == other.Callee;
        }

        public override string? ToString()
        {
            return $"{Caller} -> {Callee}";
        }
    }
}
