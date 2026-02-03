# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build
dotnet build

# Build for release
dotnet build -c Release CallGraphBuilder.sln

# Run
dotnet run --project CallGraphBuilder.csproj
```

No test suite or linting configuration exists in this project.

## Project Overview

CallGraphBuilder is a .NET 8.0 console application that performs static analysis on .NET assemblies using Mono.Cecil to generate call graphs. It supports two algorithms for resolving virtual method call targets:

- **CHA (Class Hierarchy Analysis)**: Uses inheritance tree to resolve virtual calls. Fast but less precise (includes all subtypes in hierarchy).
- **RTA (Rapid Type Analysis)**: Tracks actually instantiated types. More precise than CHA as it only considers types with `newobj` calls.

## Architecture

```
Program.cs           Entry point - loads config, initializes workspace, triggers analysis
    │
    ▼
Workspace.cs         Orchestrator - loads assemblies, determines entrypoints, creates analyzer
    │
    ├─► CollectModuleDefinitions()  - Reads .dll files using Mono.Cecil
    ├─► DetermineEntryPoints()      - Seeds methodQueue based on EntrypointStrategy
    └─► BuildCallGraph()            - Creates analyzer, processes method queue
            │
            ▼
Analyzer.cs          Abstract base - inspects methods, handles IL opcodes (Call, Callvirt, Newobj, etc.)
    │
    ├─► ChaAnalyzer.cs    Implements ApplyAlgorithm() for virtual call resolution via type hierarchy
    └─► RtaAnalyzer.cs    Implements ApplyAlgorithm() filtering by instantiated types only
            │
            ▼
CallGraph.cs         Graph data structure containing Node and Edge collections
```

### Key Classes

- **Workspace**: Loads assemblies from `BinaryPath`, resolves modules, manages the method processing queue
- **Analyzer**: Base class that traverses IL instructions. Handles `Call`/`Callvirt`/`Newobj`/`Ldftn`/`Ldvirtftn` opcodes. Delegates virtual call resolution to subclasses via `ApplyAlgorithm()`
- **ChaAnalyzer**: For virtual calls, finds all derived types (classes) or implementing types (interfaces) and adds edges for each possible target
- **CallGraph/Node/Edge**: Simple graph model where nodes are methods (identified by `FullMethodName`) and edges are caller→callee relationships

### Configuration (config.json)

```json
{
  "BinaryPath": "path/to/assemblies",
  "Namespaces": ["MyApp"],
  "Algorithm": "CHA",
  "EntrypointStrategy": "DOTNET_MAIN",
  "JsonOutputPath": "path/to/output/call-graph.json"
}
```

**EntrypointStrategy options** (defined in `EntrypointStrategy.cs`):
- `DOTNET_MAIN` - Uses assembly entry points
- `PUBLIC_CONCRETE` - Public non-abstract methods from concrete classes
- `ACCESSIBLE_CONCRETE` - Public/protected/protected-internal non-abstract methods
- `CONCRETE` - All non-abstract methods
- `ALL` - All methods (including abstract, getters/setters)

## Dependencies

- **Mono.Cecil** - Assembly reading and IL analysis
- **Microsoft.Extensions.Logging** - Logging infrastructure
- **Microsoft.Extensions.Logging.Console** - Console logging provider

## Development Notes

- The config path in `Program.cs:26` and DOT output path in `Program.cs:38` are hardcoded
- Async and iterator methods are handled specially - the analyzer looks for `AsyncStateMachineAttribute` and `IteratorStateMachineAttribute` and analyzes the `MoveNext` method of the state machine type
- Output formats: JSON (full call graph) and DOT (limited edges for visualization)
- Namespace filtering excludes interfaces and attribute classes (including derived attributes)
