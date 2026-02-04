# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build
dotnet build

# Build for release
dotnet build -c Release CallGraphBuilder.sln

# Run (config file path is required)
dotnet run --project CallGraphBuilder.csproj -- config.json
```

No test suite or linting configuration exists in this project.

## Project Overview

CallGraphBuilder is a .NET 8.0 console application that performs static analysis on .NET assemblies using Mono.Cecil to generate call graphs. It supports two algorithms for resolving virtual method call targets:

- **CHA (Class Hierarchy Analysis)**: Uses inheritance tree to resolve virtual calls. Fast but less precise (includes all subtypes in hierarchy).
- **RTA (Rapid Type Analysis)**: Tracks actually instantiated types. More precise than CHA as it only considers types that are actually instantiated (`newobj` for classes, `initobj` for default struct initialization, `newarr` for struct arrays).

## Architecture

```
CallGraphBuilder/
├── Program.cs              Entry point - loads config, initializes workspace, triggers analysis
│       │
│       ▼
├── Workspace.cs            Orchestrator - loads assemblies, determines entrypoints, creates analyzer
│       │
│       ├─► CollectModuleDefinitions()  - Reads .dll files using Mono.Cecil
│       ├─► DetermineEntryPoints()      - Seeds methodQueue based on EntrypointStrategy
│       └─► BuildCallGraph()            - Creates analyzer, processes method queue
│               │
│               ▼
├── Analyzer.cs             Abstract base - inspects methods, handles IL opcodes (Call, Callvirt, Newobj, etc.)
│       │
│       ├─► ChaAnalyzer.cs  Implements ApplyAlgorithm() for virtual call resolution via type hierarchy
│       └─► RtaAnalyzer.cs  Implements ApplyAlgorithm() filtering by instantiated types only
│               │
│               ▼
├── CallGraph.cs            Graph data structure containing Node and Edge collections
├── Node.cs                 Method representation (Module, Namespace, Signature)
├── Edge.cs                 Caller→Callee relationship
├── Config.cs               Configuration loader and validation
├── EntrypointStrategy.cs   Entry point strategy constants
└── Statistics.cs           Collects and reports analysis statistics
```

### Key Classes

- **Workspace**: Loads assemblies from `BinaryPath`, resolves modules, manages the method processing queue
- **Analyzer**: Base class that traverses IL instructions. Handles `Call`/`Callvirt`/`Newobj`/`Initobj`/`Newarr`/`Ldftn`/`Ldvirtftn` opcodes. Delegates virtual call resolution to subclasses via `ApplyAlgorithm()`
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

- Config file path accepts both absolute and relative paths (resolved via `Path.GetFullPath()`)
- DOT output is written to the same directory as `JsonOutputPath` with filename `graph.dot`
- Async and iterator methods are handled specially - the analyzer looks for `AsyncStateMachineAttribute` and `IteratorStateMachineAttribute` and analyzes the `MoveNext` method of the state machine type
- Output formats: JSON (full call graph) and DOT (limited to 50 edges for visualization)
- Namespace filtering excludes interfaces and attribute classes (including derived attributes)

### Special IL Handling

- **Async/Await**: Detects `AsyncStateMachineAttribute`, creates edge to state machine's `MoveNext()` method
- **Iterators (yield)**: Detects `IteratorStateMachineAttribute`, creates edge to state machine's `MoveNext()` method
- **Explicit Interface Implementation**: Uses `method.Overrides` collection to match `IFoo.Bar` style methods
- **Method Hiding vs Override**: Checks `IsNewSlot` flag to distinguish `new` from `override`
- **Covariant Return Types**: C# 9+ feature supported via compatible return type checking
- **Default Interface Methods**: C# 8+ feature - includes interface methods with `HasBody`
- **Struct Instantiation (RTA)**: Tracks struct types via `initobj` (default initialization) and `newarr` (struct arrays), not just `newobj`
