# Redux.Library

A .NET 10 class library that lets other C# applications discover and use Redux problems, solvers, verifiers, reductions, and visualizations **without starting the Redux HTTP API**.

The package wraps the existing Redux implementations through a public `Redux.Library.ReduxClient` class. Implementations are discovered from the compiled library assembly, so newly added classes can become available after the package is rebuilt and the consuming application updates to that version.

## Requirements

- .NET 10 SDK for building the library and the example application
- A built `Redux.Library` NuGet package (`.nupkg`)

The package is currently distributed as a local package or a GitHub Actions artifact; the instructions below do **not** assume that it has been published to NuGet.org.

## Build and package

Run these commands from the **Redux repository root**:

```bash
dotnet build Redux.Library/Redux.Library.csproj -c Release
dotnet test Redux.Library.Tests/Redux.Library.Tests.csproj -c Release
dotnet pack Redux.Library/Redux.Library.csproj -c Release -p:PackageVersion=1.0.100 -o ./nupkgs
```

`1.0.100` is an example local package version. Use a new version when distributing changed code, so NuGet does not reuse an older cached package.

The repository also has a GitHub Actions workflow at `.github/workflows/build-nuget.yml` that builds/tests and packages the library on the `CSharpAPI` branch. Its package version is based on the GitHub Actions run number (`1.0.<run number>`). Download the `.nupkg` from the successful workflow's artifact when using that distribution route.

## Install in another .NET application

For example, create a console application **outside** the Redux repository:

```bash
dotnet new console -n MyReduxApp --framework net10.0
cd MyReduxApp
mkdir packages
```

Copy your newly built `Redux.Library.<version>.nupkg` into `MyReduxApp/packages/`, then run (substitute the version you copied):

```bash
dotnet add package Redux.Library --version 1.0.100 --source ./packages --no-restore
dotnet restore --source ./packages --source https://api.nuget.org/v3/index.json
dotnet run --no-restore
```

The local source supplies `Redux.Library`; the NuGet.org source can supply any other required dependencies. A consumer uses the compiled package and does not need to start the Redux web server.

## Quick start

Replace the consuming application's `Program.cs` with:

```csharp
using Redux.Library;

var redux = new ReduxClient();

Console.WriteLine($"Problems: {redux.GetProblems().Length}");
Console.WriteLine($"Solvers: {redux.GetSolvers().Length}");
Console.WriteLine($"Verifiers: {redux.GetVerifiers().Length}");
Console.WriteLine($"Reductions: {redux.GetReductions().Length}");
Console.WriteLine($"Visualizations: {redux.GetVisualizations().Length}");

string satSolution = redux.Solve("SATBruteForceSolver", "(x1)");
Console.WriteLine($"SAT result: {satSolution}");
```

The number of discovered implementations depends on the package version. In one local test of version `1.0.100`, discovery returned 52 problems, 83 solvers, 52 verifiers, 20 reductions, and 52 visualizations.

## Public client API

| Method | Purpose |
| --- | --- |
| `GetProblems()` | List discovered problem class names |
| `GetSolvers()` | List discovered solver class names |
| `GetVerifiers()` | List discovered verifier class names |
| `GetReductions()` | List discovered reduction class names |
| `GetVisualizations()` | List discovered visualization class names |
| `Solve(solverName, instance)` | Run a solver and return its solution string |
| `Verify(verifierName, instance, certificate)` | Check a certificate and return `bool` |
| `Reduce(reductionName, instance)` | Run a reduction and return the reduced problem's `instance` string |
| `Visualize(visualizationName, instance)` | Return visualization payload as JSON text |
| `VisualizeSolved(visualizationName, instance, solution)` | Return a solved visualization as JSON text |
| `VisualizeSteps(visualizationName, instance, steps)` | Return an array of JSON strings for intermediate visualization steps |

Names are the **implementation class names** returned by discovery, not necessarily the displayed problem names. The `Reduce` method with a custom instance requires the selected reduction class to have a public constructor accepting `string`. Visualization methods currently require the selected visualization class to be constructible with no arguments. Some implementations may not support these requirements.

## Examples

### Solve problems

```csharp
using Redux.Library;

var redux = new ReduxClient();

string cliqueInstance =
    "(({1,2,3},{{1,2},{2,3},{1,3}}),3)";

string cliqueSolution = redux.Solve("CliqueBruteForce", cliqueInstance);
Console.WriteLine(cliqueSolution); // {1,2,3}

string knapsackSolution = redux.Solve(
    "KnapsackBruteForce",
    "({(10,60),(20,100),(30,120)},50,220)"
);
Console.WriteLine(knapsackSolution);
```

Other solvers successfully exercised in a consuming application include `MaxCutSolver`, `IntegerProgrammingBruteForce`, and `SudokuSolver`. Each algorithm requires its own input grammar; refer to the corresponding Redux problem's `defaultInstance` or `instanceFormat` for examples.

### Verify a certificate

```csharp
// Use a verifier name from redux.GetVerifiers(), with an instance and
// certificate in the format expected by that verifier.
string[] verifierNames = redux.GetVerifiers();
foreach (string name in verifierNames)
    Console.WriteLine(name);

// bool valid = redux.Verify("ActualVerifierClassName", instance, certificate);
```

Verification is exposed by the client, but the examples in this README do not claim a specific verifier/certificate combination was independently tested.

### Reduce CLIQUE to VERTEXCOVER

```csharp
string cliqueInstance =
    "(({1,2,3},{{1,2},{2,3},{1,3}}),3)";

string reduced = redux.Reduce(
    "sipserReductionVertexCover",
    cliqueInstance
);

Console.WriteLine(reduced); // (({1,2,3},{}),0)
```

The input is a complete three-vertex graph. Its complement has no edges, so the corresponding vertex-cover threshold is zero.

### Generate graph JSON

```csharp
string cliqueInstance =
    "(({1,2,3},{{1,2},{2,3},{1,3}}),3)";

string graphJson = redux.Visualize(
    "CliqueDefaultVisualization",
    cliqueInstance
);

Console.WriteLine(graphJson);
```

The JSON includes `kind: "graph"`, a `nodes` array, and a `links` array. This is **structured visualization data**, not a PNG, SVG, or directly rendered graph; the consuming application must render it.

### Generate a solved graph visualization

```csharp
string cliqueInstance =
    "(({1,2,3},{{1,2},{2,3},{1,3}}),3)";

string solution = redux.Solve("CliqueBruteForce", cliqueInstance);
string solvedGraphJson = redux.VisualizeSolved(
    "CliqueDefaultVisualization",
    cliqueInstance,
    solution
);

Console.WriteLine(solvedGraphJson);
```

For this example, the returned graph JSON marks the three solution vertices and their edges with `"color":"Solution"`.

### Handle errors without stopping other tests

```csharp
try
{
    string result = redux.Solve("CliqueBruteForce", "invalid input");
    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.WriteLine($"Redux operation failed: {ex.Message}");
}
```

An unknown class name raises `ArgumentException`; unsupported reduction constructors raise `NotSupportedException`. Algorithm-specific parsing or execution errors can also occur.

## How discovery works

`ReduxClient` scans the compiled `Redux.Library` assembly and finds concrete classes implementing Redux's internal `IProblem`, `ISolver`, `IVerifier`, `IReduction`, and `IVisualization` interfaces. The public wrapper exposes strings, booleans, and serialized JSON instead of requiring consuming applications to reference those internal interfaces.

**Important:** Pulling newer Redux source code does not change an already installed package. Rebuild and install a **new package version** to make new implementations available to the consuming application.

## Automated tests

The `Redux.Library.Tests` xUnit project tests the library without running the HTTP API. The test suite includes solver functionality and tests for reduction discovery, CLIQUE-to-VERTEXCOVER reduction, visualization discovery, graph JSON structure, and solved CLIQUE highlighting.

From the Redux repository root:

```bash
dotnet test Redux.Library.Tests/Redux.Library.Tests.csproj -c Release
```

The CI workflow runs the library tests before packaging. A successful compilation alone does not prove that every discovered solver, reduction, or visualization executes correctly.

## Current limitations

- Reductions with no public `string` constructor cannot be invoked with a custom instance through the current `Reduce` wrapper.
- Visualizations are returned as JSON strings, not rendered UI components or image files.
- Visualization classes may have differing constructor or payload requirements; discovery alone does not guarantee execution support.
- Step visualizations require step objects in the shape expected by the specific implementation.
- The package is currently installed from local `.nupkg` files or downloaded build artifacts, not assumed to be available on NuGet.org.
- Some Redux reduction classes perform work in their constructors; invoking `reduce()` afterward may repeat that work.

## Project layout

```text
Redux/
├── Redux.Library/
│   ├── Redux.Library.csproj
│   └── ReduxClient.cs
├── Redux.Library.Tests/
│   └── Redux.Library.Tests.csproj
├── Interfaces/
├── Problems/
├── .github/workflows/build-nuget.yml
└── nupkgs/                 # generated locally
```

`Redux.Library` compiles the existing Redux problem/interface source into a reusable .NET assembly. This means external .NET applications can use the Redux algorithms without running the original HTTP API.
