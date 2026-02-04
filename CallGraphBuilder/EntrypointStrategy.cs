
namespace CallGraphBuilder
{
    internal class EntrypointStrategy
    {
        public static string DOTNET_MAIN = "DOTNET_MAIN";

        // Selects public non-abstract/synthetic methods from non-interface/annotation classes
        public static string PUBLIC_CONCRETE = "PUBLIC_CONCRETE";

        // Selects public/protected non-abstract/synthetic methods from non-interface/annotation classes
        public static string ACCESSIBLE_CONCRETE = "ACCESSIBLE_CONCRETE";

        // Selects all non-abstract/synthetic methods from non-interface/annotation classes
        public static string CONCRETE = "CONCRETE";

        // Selects all methods from all non-interface/annotation classes
        public static string ALL = "ALL";        
    }
}
