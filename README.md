# .NET Call Graph Builder

## Project Overview

`CallGraphBuilder` is a .NET console application that performs static analysis on .NET assemblies using `Mono.Cecil`. 

It generates call graphs by analyzing method call relationships and supports Class Hierarchy Analysis (CHA) and Rapid Type Analysis (RTA) 
for resolving virtual method call targets.

## Project Structure

```
dotnet-call-graph-builder/
├── CallGraphBuilder/           # Main source code
│   ├── Program.cs              # Application entry point
│   ├── Workspace.cs            # Analysis orchestrator
│   ├── Analyzer.cs             # Abstract base analyzer
│   ├── ChaAnalyzer.cs          # Class Hierarchy Analysis
│   ├── RtaAnalyzer.cs          # Rapid Type Analysis
│   ├── CallGraph.cs            # Graph data structure
│   ├── Node.cs                 # Method node
│   ├── Edge.cs                 # Call edge
│   ├── Config.cs               # Configuration loader
│   ├── EntrypointStrategy.cs   # Entry point constants
│   └── Statistics.cs           # Statistics reporting
├── CallGraphBuilder.csproj     # Project file (.NET 8.0)
├── CallGraphBuilder.sln        # Solution file
├── config.json                 # Runtime configuration
└── out/                        # Output directory
```

## Build and Run Commands

```bash
# Build the solution
dotnet build CallGraphBuilder.sln
# or simply
dotnet build

# Build for release
dotnet build -c Release CallGraphBuilder.sln

# Run the application (config file path is required)
dotnet run --project CallGraphBuilder.csproj -- config.json
# or call the generated DLL directly
dotnet ./bin/Debug/net8.0/CallGraphBuilder.dll config.json
```

## Call Flow Analysis Algorithms

### Class Hierarchy Analysis (CHA)

Class Hierarchy Analysis is a static analysis technique used to resolve virtual method call targets in object-oriented programs. It's particularly important for building accurate call graphs.

**The Problem CHA Solves**

In OOP languages, virtual/polymorphic method calls create ambiguity at compile time:
```c#
  Animal animal = GetAnimal();  // Could return Dog, Cat, Bird...
  animal.Speak();               // Which Speak() gets called?
```

The actual method invoked depends on the runtime type, which static analysis can't know for certain.

**How CHA Works**

CHA uses the class hierarchy (inheritance tree) to conservatively estimate all possible call targets:

1. Build the class hierarchy - Map all classes and their inheritance relationships
2. For each virtual call site - Identify the declared type of the receiver
3. Find all subtypes - Collect all classes that inherit from (or implement) that type
4. Include all overrides - Any class that overrides the method is a potential target

**Example**
```
           Animal (abstract)
             │
      ┌──────┼──────┐
      │      │      │
     Dog    Cat   Bird
```

For `animal.Speak()` where animal is declared as `Animal`, CHA would report possible targets:
 - `Dog.Speak()`
 - `Cat.Speak()`
 - `Bird.Speak()`

**Trade-offs**

|   Aspect    |      CHA Characteristic                                                   |
|:------------|:--------------------------------------------------------------------------|
| Precision   | Low - overapproximates (includes targets that may never actually execute) |
| Soundness   | High - won't miss real call targets                                       |
| Speed       | Fast - only needs type hierarchy, not data flow                           |
| Scalability | Excellent - works on large codebases                                      |


**Comparison with Other Analyses**
  - CHA - Uses class hierarchy only (fast, imprecise)
  - RTA (Rapid Type Analysis) - Only considers instantiated types (more precise)
  - VTA (Variable Type Analysis) - Tracks types flowing to variables (even more precise)
  - Points-to Analysis - Full pointer/reference tracking (most precise, slowest)


### Rapid Type Analysis (RTA)

RTA is a static analysis technique for resolving virtual method call targets, sitting between Class Hierarchy Analysis (CHA) and more precise flow-sensitive analyses.

**Core Idea**

RTA improves on CHA by tracking which types are actually instantiated in the program, not just which types exist in the hierarchy.

**How It Works**

1. Build a set of instantiated types - Scan the program for new expressions (or newobj in IL) and record which concrete types are created
2. Resolve virtual calls - When analyzing a virtual call on type T, only consider implementations from types that:
  - Are subtypes of T, AND
  - Are in the instantiated set (or have a subtype that's instantiated)

**Example**
```c#
  abstract class Animal { public abstract void Speak(); }
  class Dog : Animal { public override void Speak() => Console.WriteLine("Bark"); }
  class Cat : Animal { public override void Speak() => Console.WriteLine("Meow"); }
  class Bird : Animal { public override void Speak() => Console.WriteLine("Chirp"); }

  // Program only instantiates Dog
  Animal a = new Dog();
  a.Speak();  // Virtual call
```

- CHA considers: `Dog.Speak()`, `Cat.Speak()`, `Bird.Speak()` (all subtypes)
- RTA considers: Only `Dog.Speak()` (only Dog is instantiated)

**Iterative Refinement**

RTA typically runs iteratively:
1. Start with entry point methods
2. Find instantiated types and reachable methods
3. Analyze newly reachable methods for more instantiations
4. Repeat until fixed point


## Comparison between CHA and RTA

| Aspect          |            CHA            |            RTA             |
|-----------------|---------------------------|----------------------------|
| Considers       | All subtypes in hierarchy | Only instantiated subtypes |
| Precision       | Lower                     | Higher                     |
| Cost            | Cheaper                   | Slightly more expensive    |
| False positives | More                      | Fewer                      |


## Configuration

The application reads settings from a `config.json` file. The config file path can be absolute or relative (resolved against the current working directory).

```json
{
  "BinaryPath": "path/to/assemblies",
  "Namespaces": ["MyApp"],
  "Algorithm": "CHA",
  "EntrypointStrategy": "DOTNET_MAIN",
  "JsonOutputPath": "path/to/output/call-graph.json"
}
```

| Property             | Description                     |
|----------------------|---------------------------------|
| `BinaryPath`         | Directory containing .NET assemblies (.dll files) to analyze |
| `Namespaces`         | Filter to limit analysis to types in these namespaces (exact match or prefix) |
| `Algorithm`          | Analysis algorithm: `CHA` or `RTA` |
| `EntrypointStrategy` | How to determine analysis starting points: `DOTNET_MAIN`, `PUBLIC_CONCRETE`, `ACCESSIBLE_CONCRETE`, `CONCRETE`, `ALL` |
| `JsonOutputPath`     | File path for JSON output |

## Output

The tool produces two output formats:

1. **JSON** - Complete call graph with nodes (methods) and edges (caller→callee relationships), written to `JsonOutputPath`
2. **DOT** - GraphViz format for visualization (limited to 50 edges), written as `graph.dot` in the same directory as the JSON output
