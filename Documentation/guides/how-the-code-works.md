# How the code works

A plain-language map of the Redux API repo: what each folder holds, how the pieces fit together, and the few places where a careless change breaks the website. Read it once before your first task, and come back when you are lost.

Some words you will see:

- **Interface**: a list of members (names, and sometimes methods) that a class promises to provide. A class that says `: IProblem` must supply everything `IProblem` lists. Think of it as a job description.
- **Instance string**: a problem written as text, for example `({3,5,7},8)` for "numbers 3, 5, 7 and a target of 8". The API passes problems around as strings.
- **Certificate**: a proposed answer written as text. A verifier checks whether a certificate really solves a problem.
- **Reflection**: C# code that looks at the program's own classes while it runs ("find me every class that implements `IProblem`"). Redux uses it so you never have to register anything by hand.
- **Controller**: a C# class whose methods answer web requests (API calls).

New to running the code? Do [setup.md](setup.md) first.

---

## 1. The big picture

Redux stores computer-science problems (like Subset Sum, Clique, 3-SAT), ways to **solve** them, ways to **verify** answers, ways to **visualize** them, and **reductions** that turn one problem into another. The website (Redux_GUI) and other tools call the API to list these things and run them.

Each of those five things is one C# class that implements one interface. You add a feature by adding a class in the right folder with the right name. The API finds it on its own.

## 2. The folders

| Folder | What is in it |
| --- | --- |
| [Problems/](../../Problems/) | Every problem, one folder per problem. Split into [NPComplete/](../../Problems/NPComplete/) (folders named `NPC_<NAME>`, the main set), [NPHard/](../../Problems/NPHard/) (`NPH_<NAME>`), and [P/](../../Problems/P/) (`P_<NAME>`). The folder is a filing convention only. What actually decides a problem's class is the `complexityClass` it declares in its own code. |
| [Interfaces/](../../Interfaces/) | The five interfaces, the small "enum" lists used for metadata (such as `ComplexityClass`, `ReductionCost`, `SolverType`), the parse exceptions, graph helpers, and the JSON shapes the API returns. |
| [AdditionalControllers/](../../AdditionalControllers/) | The API itself: [ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs) (solve, verify, reduce, visualize) and [Navigation/](../../AdditionalControllers/Navigation/) (listing what exists). |
| [ProblemTemplate/](../../ProblemTemplate/) | The code that generates starter files, plus the template text files in [Templates/](../../ProblemTemplate/Templates/). |
| [redux-tests/](../../redux-tests/) | All the tests. |
| [Tools/](../../Tools/) | Helpers that are not about one problem: [Boolean_Parser.cs](../../Tools/Boolean_Parser.cs), the quantum server client ([QUANTUM_API_README.md](../../Tools/QUANTUM_API_README.md) explains it), and `ContributorStatsSync`, a separate small program that GitHub runs to refresh contributor info. |
| [wwwroot/](../../wwwroot/) | Static files the API serves as-is: the Swagger page's stylesheet and `contributorInfo.json`. |
| [Documentation/](../../Documentation/) | These guides, [production.md](../production.md) (server operations), and `visualization-types.json` (a list that code and CI read, so do not move or rename it). |

At the repo root, [API.csproj](../../API.csproj) is the project, [Redux.slnx](../../Redux.slnx) lists the API and the test project together, and [Program.cs](../../Program.cs) starts the web server.

### Inside one problem folder

Take [Problems/NPComplete/NPC_SUBSETSUM/](../../Problems/NPComplete/NPC_SUBSETSUM/) as the model:

```text
NPC_SUBSETSUM/
  SUBSETSUM_Class.cs          the problem itself (IProblem)
  Solvers/                    one file per solver (ISolver)
  Verifiers/                  one file per verifier (IVerifier)
  Visualizations/             one file per visualization (IVisualization), when the problem has one
  ReduceTo/NPC_<TO>/          one file per reduction away from this problem (IReduction)
```

Notes: the class file is `<NAME>_Class.cs`, and reductions live under the problem they reduce **from**. A few older problems differ slightly (for example `P_SSSP.cs`). When in doubt, copy the newest problem that is similar to yours.

## 3. The five interfaces

All in [Interfaces/](../../Interfaces/). Each has a plain version and a generic version (for example `IProblem` and `IProblem<TSolver, TVerifier, TVisualization>`). You implement the generic one, which saves you from writing the string-parsing boilerplate.

| Interface | File | What one class does |
| --- | --- | --- |
| `IProblem` | [ProblemInterface.cs](../../Interfaces/ProblemInterface.cs) | Describes a problem (name, definition, source, a default example, the instance and certificate formats, declared complexity class) and parses an instance string. Names its default solver, verifier, and visualization. |
| `ISolver` | [SolverInterface.cs](../../Interfaces/SolverInterface.cs) | Takes a problem and returns a solution as a string. Can optionally return step-by-step progress. |
| `IVerifier` | [VerifierInterface.cs](../../Interfaces/VerifierInterface.cs) | Takes a problem and a certificate and answers true or false. |
| `IVisualization` | [VisualizationInterface.cs](../../Interfaces/VisualizationInterface.cs) | Turns a problem (and optionally a solution or steps) into a JSON drawing the website can render. |
| `IReduction` | [ReductionInterface.cs](../../Interfaces/ReductionInterface.cs) | Turns an instance of problem A into an instance of problem B, maps solutions back, and can describe "gadgets" (which parts of A became which parts of B). |

How they relate:

```text
Reduction  --reductionFrom-->  Problem A
           --reductionTo---->  Problem B
Problem  --defaultSolver-->  Solver
         --defaultVerifier-> Verifier
         --defaultVisualization--> Visualization
Solver / Verifier / Visualization are generic over the problem they work on.
```

A problem can have many solvers, verifiers, and visualizations, but it must name one of each as its default. If a problem has no visualization, use `DummyVisualization` from [Interfaces/DummyClasses/](../../Interfaces/DummyClasses/). Every interface has "declared metadata" members (such as `complexityClass`, `solverType`, `cost`) that a person fills in. They default to `Unclassified`, and tests fail while they stay that way. The task guides explain each one.

To add one of these, follow its guide: [adding-a-problem.md](adding-a-problem.md), [adding-a-solver.md](adding-a-solver.md), [adding-a-verifier.md](adding-a-verifier.md), [adding-a-visualization.md](adding-a-visualization.md), [adding-a-reduction.md](adding-a-reduction.md).

## 4. How classes are found: ProblemProvider and reflection

Older docs say each problem or reduction needs its own controller file. That is no longer true. One controller, [ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs), serves all of them.

When the API starts, `ProblemProvider` scans every class in the program once (`ScanTypes`). It files each class into one of five dictionaries (`Problems`, `Verifiers`, `Solvers`, `Visualizers`, `Reductions`) based on which interface it implements. The dictionary key is the class name in lowercase. Then calls such as `POST /ProblemProvider/solve?solver=<ClassName>` look the class up in the dictionary and create it.

What this means for you:

- **No registration.** Creating the class in the right folder is enough.
- **Class names must be unique across the whole project, ignoring upper and lower case, within each kind of class.** The scan uses `.Add`, so two classes with the same lowercase name throw an error at startup and the whole API fails to start. See [troubleshooting.md](troubleshooting.md).
- **The class name is the ID** that callers (including the website) use, so renaming a class changes the API.
- Anything that looks like a problem becomes public. Do not put half-finished classes on a branch you intend to merge.

The calls you get for free, all in `ProblemProvider.cs`: `verify`, `solve`, `info`, `problemInstance`, `visualize`, `visualizeReduction`, `reduce`, `mapSolution`, and `gadgets`. Try them in Swagger.

## 5. The Navigation controllers (careful)

The files in [AdditionalControllers/Navigation/](../../AdditionalControllers/Navigation/) answer "what exists?": which problems, reductions, solvers, verifiers, and visualizations there are, the reduction graph (`/Navigation/Reductions`), shortest reduction paths, the metadata enums, batch endpoints, and contributor profiles. They read the same reflection dictionaries, so they pick up your new class automatically.

**The website depends heavily on these exact URLs and the exact shape of their JSON.** You normally never need to edit these files. If you do:

- Do not rename a route, a controller, or a JSON field.
- Adding a field is usually safe. Removing or renaming one breaks the website.
- Tests in [redux-tests/Endpoints/Navigation_Endpoint_Tests.cs](../../redux-tests/Endpoints/Navigation_Endpoint_Tests.cs) guard some of this, but not all of it. Mention the change in your PR and ask a maintainer to check it against Redux_GUI.

## 6. Graph helpers

Many problems are about graphs. Use the helpers in [Interfaces/graphs/](../../Interfaces/graphs/) instead of writing your own:

- [UtilCollectionGraph.cs](../../Interfaces/graphs/UtilCollectionGraph.cs): builds a graph from the parsed nodes and edges, works out for itself whether the graph is directed and/or weighted, and has `ToAPIGraph()`, which converts it to the JSON drawing the website expects. A real user: [ARCSET_Class.cs](../../Problems/NPComplete/NPC_ARCSET/ARCSET_Class.cs).
- [Graph.cs](../../Interfaces/graphs/Graph.cs), [Node.cs](../../Interfaces/graphs/Node.cs), [Edge.cs](../../Interfaces/graphs/Edge.cs), [LabeledEdge.cs](../../Interfaces/graphs/LabeledEdge.cs), [GraphParser.cs](../../Interfaces/graphs/GraphParser.cs), and [WeightedDirectedGraph.cs](../../Interfaces/graphs/WeightedDirectedGraph.cs): older graph types that some problems still use.

The classes that make the actual drawing JSON are in [Interfaces/JSON_Objects/](../../Interfaces/JSON_Objects/) (`API_GraphJSON`, `API_SAT`, and so on). The code comment on `UtilCollectionGraph` says it is a transitional class that is expected to be replaced eventually, but it is what most problems use today.

## 7. SPADE: reading instance strings

**SPADE** is a library (a NuGet package, [github.com/Jetison333/SPADE](https://github.com/Jetison333/SPADE)) that turns an instance string into usable values. You describe the shape of the text with a short "grammar", and SPADE does the parsing. The package is listed in [API.csproj](../../API.csproj), so there is nothing to install.

A real example, from [ARCSET_Class.cs](../../Problems/NPComplete/NPC_ARCSET/ARCSET_Class.cs):

- The grammar is the constant `{((N,E),K) | N is set, E subset N cross N, K is int}`. It says: the text is a pair of (a graph made of a set N of nodes and a set E of edges between those nodes) and a whole number K.
- The default instance that matches it is `(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)`.
- The constructor does the parsing:

  ```csharp
  StringParser arcset = new(InstanceGrammar);
  arcset.parse(arcInput);
  graph = new UtilCollectionGraph(arcset["N"], arcset["E"]);
  K = int.Parse(arcset["K"].ToString());
  ```

For the full grammar words (`set`, `cross`, `subset`, `int`, and more), read the [SPADE documentation](https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md). SPADE does not cover every input shape, so if your problem's text does not fit, write a small hand parser instead (for example [Tools/Boolean_Parser.cs](../../Tools/Boolean_Parser.cs) for boolean formulas).

Two things to know:

- SPADE throws a plain `Exception` (for example "Braces not matched") on bad text. `ParseGuard` (next section) knows this and turns it into a friendly error.
- Put the grammar in a constant and reuse it in `instanceFormat`, as ARCSET does. Then the documentation of the format and the parser cannot drift apart.

## 8. Parse exceptions: bad input should give a 400, not a 500

Callers send text, and text is often wrong. [Interfaces/ParseExceptions.cs](../../Interfaces/ParseExceptions.cs) defines how bad input is reported:

| Exception | Thrown when | What the API sends back |
| --- | --- | --- |
| `ProblemParseException` | A problem's constructor cannot read the instance string. | HTTP 400, quoting the problem's `instanceFormat` |
| `CertificateParseException` | A verifier cannot read the certificate string. | HTTP 400, quoting the problem's `certificateFormat` |
| `ReductionInputException` | A reduction cannot read its input (an instance, or a solution passed to `mapSolutions`). | HTTP 400 with the expected format |

The helper `ParseGuard` in the same file wraps the construction step and the certificate-reading step for you. If your constructor throws any ordinary exception while parsing, it becomes a `ProblemParseException` automatically. Only the parsing step is guarded. A bug in your solving or reducing algorithm still gives a 500, on purpose, so that real bugs are not hidden.

So: throw a parse exception (or let SPADE or `int.Parse` throw) on bad text. Return `false` from a verifier only when the certificate parsed fine but is not a solution. Tests such as `ParseError_Endpoint_Tests.cs` send garbage to every class and fail if they get a 500.

## 9. Templates

The folder [ProblemTemplate/Templates/](../../ProblemTemplate/Templates/) holds starter files (`PROBLEM_Class.txt`, `Solvers/ProblemSolver.txt`, `Verifiers/ProblemVerifier.txt`, `Visualizations/PROBLEMVisualization.txt`, `ReduceTo/NPC_PROBLEM/Reduction.txt`) with placeholders such as `{NAME}`. You can copy them by hand, or have the running API fill them in and zip them. The generator is [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs), and its calls appear in Swagger under "Problem Template" (`GET /ProblemTemplate`, `/ProblemTemplate/reduction`, `/solver`, `/verifier`, `/visualization`). [ProblemTemplate/Templates/README.md](../../ProblemTemplate/Templates/README.md) is a field-by-field reference for what each interface member means.

## 10. The test project

All tests are in [redux-tests/](../../redux-tests/) (an xUnit project, listed in `Redux.slnx`). Its layout:

| Folder | What it tests |
| --- | --- |
| `Problems/NPC_<NAME>/` | Your tests: one folder per problem, with a `<NAME>_Tests.cs` file. This is where new problem, solver, verifier, and reduction tests go. |
| `Metadata/` | Automatic checks that run once for **every** problem, solver, verifier, visualization, and reduction found by reflection: it has a declared class, a well-formed name, it can be built from its default, and so on. You get these for free, and they fail if you leave metadata `Unclassified`. |
| `Endpoints/` | Tests that start the real API in memory and call it: parse-error handling, performance, navigation, the template generator. |
| `Interfaces/` | Tests for the shared helpers (graphs, `ParseGuard`, instance generators). |
| `Navigation/`, `Tools/` | Contributor profiles and the quantum scheduler. |

The commands for running them are in [building-and-testing.md](building-and-testing.md). If a test fails and you do not know why, start with [troubleshooting.md](troubleshooting.md).

## 11. Where to go next

- Run the checks: [building-and-testing.md](building-and-testing.md).
- Pick a task: [guides index](README.md).
