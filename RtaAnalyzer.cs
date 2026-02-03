using Mono.Cecil;

namespace CallGraphBuilder
{
    internal class RtaAnalyzer : Analyzer
    {
        public RtaAnalyzer(CallGraph callGraph, Queue<MethodDefinition> methodQueue, IList<ModuleDefinition> allModules)
            : base(callGraph, methodQueue, allModules) { }

        protected override void ApplyAlgorithm(MethodDefinition targetMethod, Node callerMethodNode)
        {
            // TODO implement this
            throw new NotImplementedException();
        }
    }
}
