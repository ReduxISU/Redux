# Adding a problem

This guide walks you through adding a new **problem** to Redux, from an empty folder to a green pull request (PR). It uses a real, small problem already in the repo (Subset Sum) as the worked example.

New to the repo? Read the [guides index](README.md) first. When you reach the "build and test" step, the details live in [building-and-testing.md](building-and-testing.md). For how the pieces fit together, see [how-the-code-works.md](how-the-code-works.md).

Some words you will see:

- **Problem**: a question a computer can try to answer, such as "can these numbers add up to 28?". In Redux, one problem is one C# class.
- **Instance**: one specific version of the question, written as text, for example `({1,7,12,15},28)`.
- **Certificate**: a proposed answer, also written as text, for example `{1,12,15}`. A **verifier** checks whether a certificate really answers an instance.
- **Solver**: code that finds a certificate for an instance.
- **Visualization**: code that turns an instance (and optionally its solution) into drawing data for the GUI.
- **SPADE**: a small library (a NuGet package, already referenced by [API.csproj](../../API.csproj)) that reads text like `({1,7,12,15},28)` according to a pattern you describe, so you do not write the text-cutting code by hand.

---

## 1. What a problem is

A **problem** is the "question" half of everything Redux does. Solvers answer it, verifiers check answers to it, visualizations draw it, and reductions turn it into other problems.

Everyday analogy: a problem is a recipe card in a recipe box. The card says what dish you are making (the name and definition), how ingredients must be written down (the instance format), and how a finished plate is described (the certificate format). The card also points to three helpers: a cook who can make the dish (the default solver), a taster who checks a plate against the card (the default verifier), and a photographer (the default visualization).

In Redux:

- A problem is one C# class, for example `SUBSETSUM`. It implements `IProblem<Solver, Verifier, Visualization>` ([Interfaces/ProblemInterface.cs](../../Interfaces/ProblemInterface.cs)). The three type parameters name its **default solver, default verifier, and default visualization**.
- The class holds the problem's description (name, definitions, citation), its default instance, and the code that **parses** (reads) an instance string into C# fields.
- You do not write a controller or register anything. Redux finds every problem class automatically (see section 3), and the API exposes it.

### The minimum you need so it compiles

Because of those three type parameters, a problem cannot compile alone. You need at least:

| Piece | Minimum | Where to learn how |
| --- | --- | --- |
| Default solver | One class implementing `ISolver<YourProblem>` | [adding-a-solver.md](adding-a-solver.md) |
| Default verifier | One class implementing `IVerifier<YourProblem>` | [adding-a-verifier.md](adding-a-verifier.md) |
| Default visualization | A real one, **or** the built-in `DummyVisualization` placeholder | [adding-a-visualization.md](adding-a-visualization.md) |

`DummyVisualization` ([Interfaces/DummyClasses/DummyVisualization.cs](../../Interfaces/DummyClasses/DummyVisualization.cs), namespace `API.DummyClasses`) is the official "no visualization yet" answer. The worked example below uses it. If you do write a real visualization that introduces a new drawing type, the extra steps (a manifest file and a GUI check) are in [adding-a-visualization.md](adding-a-visualization.md).

## 2. Before you start

1. **Check the problem is not already there.** Look in `Problems/NPComplete/`, `Problems/NPHard/`, and `Problems/P/`.
2. **Pick a good name.** The class name is a short ALL-CAPS name, for example `SUBSETSUM`. It must be unique across the whole project, ignoring upper/lower case, and must **not** start with `NPC_`, `NPH_`, or `P_` (a test enforces that, see section 5).
3. **Decide the complexity class and subject area.** You must declare both (see Step 2). Pick them from the literature (for example Karp's 1972 paper or Garey and Johnson's book), not by guessing. If you are not sure, ask in your PR.
4. Work on a branch based on `CSharpAPI`. Pull requests go back to `CSharpAPI`.

   ```bash
   git fetch origin
   git switch -c add-problem-subsetsum origin/CSharpAPI
   ```

   Pick your own branch name, for example `add-problem-<name>`.
5. Make sure the project builds before you change anything. See [building-and-testing.md](building-and-testing.md).
6. Read the instance format of a similar problem first. If your problem is a graph or a set, **your format should match the existing problems** (see Step 3).

## 3. Where the files go

All the files for one problem live in one folder, named after the problem and its folder prefix:

```text
Problems/<Folder>/<Prefix>_<NAME>/
    <NAME>_Class.cs          the problem class
    Solvers/                 one file per solver
    Verifiers/               one file per verifier
    Visualizations/          one file per visualization (skip it if you use DummyVisualization)
    ReduceTo/                reductions that start from this problem (optional)
```

The folder and prefix depend on the complexity class you believe the problem has:

| Complexity class | `<Folder>` | `<Prefix>` | Namespace of the class |
| --- | --- | --- | --- |
| NP-Complete (most problems) | `NPComplete` | `NPC` | `API.Problems.NPComplete.NPC_<NAME>` |
| NP-Hard | `NPHard` | `NPH` | `API.Problems.NPHard.NPH_<NAME>` |
| In P | `P` | `P` | `API.Problems.P.P_<NAME>` |

Rules that matter:

| Thing | Rule | Worked example |
| --- | --- | --- |
| Folder | `Problems/<Folder>/<Prefix>_<NAME>/` | `Problems/NPComplete/NPC_SUBSETSUM/` |
| File name | `<NAME>_Class.cs` | `SUBSETSUM_Class.cs` |
| Namespace | Matches the folder path, see the table above | `API.Problems.NPComplete.NPC_SUBSETSUM` |
| Class name | Unique across the project; **no** `NPC_`/`NPH_`/`P_` prefix | `SUBSETSUM` |
| Sub-folders | `Solvers/`, `Verifiers/`, `Visualizations/`, `ReduceTo/`, each with its own sub-namespace | `...NPC_SUBSETSUM.Solvers` |
| Implements | `IProblem<Solver, Verifier, Visualization>`, or `IGraphProblem<...>` for graph problems | `IProblem<SubsetSumBruteForce, SubsetSumVerifier, DummyVisualization>` |

**The folder is a filing label, not the truth.** A problem's real complexity class is the `complexityClass` value you declare in the class ([Interfaces/ComplexityClass.cs](../../Interfaces/ComplexityClass.cs)). The API trusts that value, not the folder. A few existing problems sit in a folder that does not match their declared class (for example `NPC_MAXCUT` declares `NPHard`). Put yours in the folder that matches your declared class, and make the two agree.

One more detail: only classes whose namespace is exactly `API.Problems.<Folder>.<ProblemFolder>` are listed as top-level problems in the navigation endpoints. A helper problem class nested deeper (for example the `Inherited` folder under `NPC_CLIQUE`) is not listed. Keep your real problem at the top level of its folder.

**How Redux finds your problem.** At startup, `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) scans every class in the project. Any class that implements `IProblem` is registered under its **lowercased class name**. Solvers, verifiers, visualizations, and reductions are registered the same way, each in its own list. Because of that:

- Class names must be unique within each list (ignoring case). Two problem classes both called `SUBSETSUM`, or one called `Subsetsum`, crash startup for everyone.
- You do not edit any list. Creating the class is enough.

**Endpoints you get for free** once your problem, solver, verifier, and visualization exist (all in `ProblemProvider.cs` unless stated):

| Endpoint | What it does for your problem |
| --- | --- |
| `GET /ProblemProvider/info?interface=<ClassName>` | Returns the problem's metadata, including `instanceFormat` and `certificateFormat`. |
| `POST /ProblemProvider/problemInstance?problem=<ClassName>` | Parses an instance string and returns the parsed problem. Bad input gives a 400 that quotes your `instanceFormat`. |
| `POST /ProblemProvider/solve?solver=<SolverClass>` | Runs a solver on an instance. |
| `POST /ProblemProvider/verify?verifier=<VerifierClass>` | Checks a certificate. |
| `POST /ProblemProvider/visualize?visualization=<VisClass>` | Returns drawing data. |
| `GET /Navigation/ALL_ProblemsRefactor` and `GET /Navigation/NPC_ProblemsRefactor`, `P_ProblemsRefactor`, `NPHard_ProblemsRefactor` | Your problem appears in the list for its **declared** complexity class. |
| `GET /Navigation/allInfo` and the other `Navigation/all...` lists ([Nav_Batch.cs](../../AdditionalControllers/Navigation/Nav_Batch.cs)) | Your problem is included automatically. |

> The template README mentions older conventions (a `Reductions` folder, "only NP-Complete problems"). Trust this guide and the code: the folder is `ReduceTo`, and `NPHard/` and `P/` are both in use.

## 4. Step by step

### Step 1. Get the template

You have two options. Both give you the same files.

- **Copy it by hand.** The templates are in [ProblemTemplate/Templates/](../../ProblemTemplate/Templates/). The problem class template is [PROBLEM_Class.txt](../../ProblemTemplate/Templates/PROBLEM_Class.txt). Copy it to your new folder, rename it `<NAME>_Class.cs`, and replace `{NAME}`, `{NAME_UPPERCASE}`, and `{NAME_PASCAL_CASE}`.
- **Download it from a running API.** Start the API (see [setup.md](setup.md)), then call this, using your problem's human name:

  ```bash
  curl -o ProblemTemplate.zip "http://127.0.0.1:27000/ProblemTemplate?problemName=Subset%20Sum"
  ```

  (`27000` is the port `dotnet run` uses, set by the `API` profile in [launchSettings.json](../../Properties/launchSettings.json). Use whatever address your API prints on startup if it differs.) The endpoint is in [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs).

  **Its only query parameter is `problemName`**, the human-readable name, with spaces allowed (for example `Subset Sum` or `Traveling Sales Person`). The code turns it into an ALL-CAPS class name (`SUBSETSUM`, `TRAVELINGSALESPERSON`) and a PascalCase prefix for the helper classes (`SubsetSum`, `TravelingSalesPerson`), dropping any characters that are not letters, digits, or `_`.

  It returns `ProblemTemplate.zip` with six files, with the placeholders already filled in (shown here for `Subset Sum`):

  ```text
  README.md
  NPC_SUBSETSUM/SUBSETSUM_Class.cs
  NPC_SUBSETSUM/Solvers/SubsetSumSolver.cs
  NPC_SUBSETSUM/Verifiers/SubsetSumVerifier.cs
  NPC_SUBSETSUM/Visualizations/SubsetSumVisualization.cs
  redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs
  ```

  **Do not unzip it over the repo.** The first folder name is only a label, and the `README.md` is not meant for the repo. Copy each file to the place in the table below.

#### Where each file in the zip goes

Paths are from the repo root (the folder that contains `Redux.slnx`), shown for `Subset Sum`. Replace `SUBSETSUM` and `SubsetSum` with your own names.

| The file in the zip | Where it goes in the repo | What it is, and what to do with it |
| --- | --- | --- |
| `README.md` | Nowhere. It is not copied into the repo. | Reference only: a field-by-field description of every member you fill in. Read it, then leave it in the zip. |
| `NPC_SUBSETSUM/SUBSETSUM_Class.cs` | `Problems/NPComplete/NPC_SUBSETSUM/SUBSETSUM_Class.cs` | The problem class. Fill in the metadata and the constructor that parses the instance (Steps 2 and 3). |
| `NPC_SUBSETSUM/Solvers/SubsetSumSolver.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Solvers/SubsetSumSolver.cs` | The default solver. Throws `NotImplementedException` until you write it ([adding-a-solver.md](adding-a-solver.md)). |
| `NPC_SUBSETSUM/Verifiers/SubsetSumVerifier.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Verifiers/SubsetSumVerifier.cs` | The default verifier, already named `"Default Subset Sum Verifier"`. Throws `NotImplementedException` until you write it ([adding-a-verifier.md](adding-a-verifier.md)). |
| `NPC_SUBSETSUM/Visualizations/SubsetSumVisualization.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Visualizations/SubsetSumVisualization.cs` | A starting visualization ([adding-a-visualization.md](adding-a-visualization.md)). **If you use `DummyVisualization` instead, delete this file and the `Visualizations` folder**, and point the class's `defaultVisualization` at `DummyVisualization`. |
| `redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs` | `redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs` | Your test file. Some tests are finished and some contain a `TODO` and fail on purpose until you fill them in (section 5 below). |

The `NPC_` folders go under `Problems/NPComplete/`, as in the table. If your problem belongs in `Problems/NPHard/` or `Problems/P/`, put the folder there instead, rename it with the `NPH_` or `P_` prefix, and fix the namespaces in the class, solver, verifier, and visualization (see section 3). The test file's folder uses the same prefix, and its `using` lines need the same fix.

There are separate download endpoints for just a solver, verifier, visualization, or reduction. Those are covered in the matching guides.

**The generated files are starting points, not finished code.** The template spells out every member you must decide. Tests fail until you replace each `Unclassified` or empty value and each `NotImplementedException`, and that is intentional. Look for the `TODO` comments:

1. In the class, `complexityClass` and `problemType` are declared as `Unclassified` (Step 2).
2. The generated solver and verifier throw `NotImplementedException` until you write them, and the solver's `solverType`, `complexityBucket`, and `complexity` are left for you to fill in. The verifier is already named `"Default <Problem Name> Verifier"`, which is what a test requires. See [adding-a-solver.md](adding-a-solver.md) and [adding-a-verifier.md](adding-a-verifier.md).
3. The generated visualization has `VisualizationType.Unimplemented`. A test rejects new classes that stay `Unimplemented`. Either finish it ([adding-a-visualization.md](adding-a-visualization.md)) or delete the generated visualization and use `DummyVisualization`.

Also remove every `"TODO"` before you open the PR (the contributor name `"TODO"` is tolerated by one test, but it is not acceptable in a PR).

### Step 2. Fill in the members

Every problem implements the members in [Interfaces/ProblemInterface.cs](../../Interfaces/ProblemInterface.cs). Here is each one in plain words, with the value used by the worked example, [`SUBSETSUM_Class.cs`](../../Problems/NPComplete/NPC_SUBSETSUM/SUBSETSUM_Class.cs) (Subset Sum, from Karp's 1972 paper). It is a good one to copy from because it is small, uses the current conventions, and uses `DummyVisualization`.

| Member | What it is for | Worked example value |
| --- | --- | --- |
| `problemName` | The name people see in the GUI. | `"Subset Sum"` |
| `problemLink` | A link to a page that explains the problem. Not in the interface, but every template and problem has it. | the Wikipedia page |
| `formalDefinition` | The problem written like math: `Name = <inputs> \| condition`. | `"Subset Sum = <S, T> \| S is a set of positive integers and ..."` |
| `problemDefinition` | The same idea in plain English. | `"The problem is to determine whether there exists a sum of elements that totals to the number T."` |
| `inputDescription` | A short name for the input. | `"S, a set of integers, and T, a target sum"` |
| `outputDescription` | A short description of the answer. | `"True or False, whether some subset of S sums to T"` |
| `source` | A proper citation for where the problem comes from. | Karp, "Reducibility among combinatorial problems", 1972 |
| `sourceLink` | A link to that citation. Not in the interface, but present in the template and in problems. | the Karp PDF link |
| `defaultInstance` | A reasonably sized example instance. Used by the tests and the GUI. It **must** parse. | `"({1,7,12,15},28)"` |
| `instance` | The instance string this object was built from. The constructor sets it. | set in the constructor |
| `instanceFormat` | One sentence plus a real example that tells a human, the GUI, or an AI agent how to write an instance. Returned inside every parse-error response. | `"Format: {(S,T) \| S subset int, T is int} Example: ({1,7,12,15},28)"` |
| `certificateFormat` | The same, for the certificate the **default verifier** accepts. | built from the verifier's `CertificateGrammar` and `CertificateExample` |
| `complexityClass` | The complexity class you declare (see below). | `ComplexityClass.NPComplete` |
| `problemType` | The subject area you declare (see below). | `ProblemType.SetsAndPartitions` |
| `contributors` | Full names of the people who wrote it (see below). | `{ "Garret Stouffer", "Caleb Eardley" }` |
| `wikiName` | Deprecated, but still required by the interface. Set it to an empty string. | `""` |
| `defaultSolver` | An object of the default solver. | `new SubsetSumBruteForce()` |
| `defaultVerifier` | An object of the default verifier. | `new SubsetSumVerifier()` |
| `defaultVisualization` | An object of the default visualization. | `new DummyVisualization()` |

Your class also needs the **fields that hold the parsed instance** (here a list `S` and a number `T`) and **two constructors** (see Step 3).

**The two declared metadata members** are the ones people forget. The template declares both as `Unclassified` with a `TODO` comment, and tests fail if you leave them that way (section 5):

- `complexityClass`: pick from [Interfaces/ComplexityClass.cs](../../Interfaces/ComplexityClass.cs): `P`, `NPComplete`, `NPHard`, `NP`, or one of the quantum classes. It is a public correctness claim the API serves to callers, so check the literature.
- `problemType`: pick the subject area from [Interfaces/ProblemType.cs](../../Interfaces/ProblemType.cs) (for example `GraphTheory`, `SetsAndPartitions`, `Logic`). Use `Miscellaneous` only when nothing else fits.

"Declared" means a human decides. No tool computes them for you.

**Contributors.** `contributors` is a list of names. Every name must match a key in [wwwroot/contributorInfo.json](../../wwwroot/contributorInfo.json) exactly (ignoring upper/lower case). A test checks every problem, solver, verifier, visualization, and reduction (section 5). If you are new:

1. Add an entry for yourself to `wwwroot/contributorInfo.json`, copying the shape of an existing entry: `email`, `education`, `major`, `bio`, `githubUsername`, and the two stats objects.
2. **Leave both stats objects at zero** (`"reduxStats": { "prsMerged": 0, "reviews": 0 }` and the same for `reduxGuiStats`). A test fails if non-zero counts are committed; the release build fills them in. The tool that does this is in [Tools/ContributorStatsSync](../../Tools/ContributorStatsSync/Program.cs).
3. A GitHub account can belong to only one entry. If you use a second account, list the extra one under `"otherGithubUsernames"`.

Use full names ("Garret Stouffer", not "Garret"), one name per array element.

### Step 3. Parse the instance

The two constructors are the part to copy exactly. The empty one is what the tests and the navigation endpoints call. The `string` one is what the API calls:

```csharp
public SUBSETSUM() : this(_defaultInstance) {

}

public SUBSETSUM(string instance) {
    this.instance = instance;
    ...
}
```

**Parsing with SPADE.** Describe the shape of an instance as a short pattern, then ask SPADE to read it. In the worked example:

```csharp
public const string InstanceGrammar = "{(S,T) | S subset int, T is int}";
```

reads as "an instance is a pair `(S,T)`, where `S` is a subset of integers and `T` is an integer". Then the constructor does:

```csharp
StringParser parser = new(InstanceGrammar);
...
parser.parse(instance);
parsedS = parser["S"].ToList().Select(x => x.ToString()).ToList();
tStr = parser["T"].ToString();
```

SPADE's own documentation lives in the [SPADE repository](https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md). Other real grammars to compare against:

| Kind of problem | Grammar | Example problem |
| --- | --- | --- |
| A plain set of integers | `{N \| N subset int}` | [PARTITION_Class.cs](../../Problems/NPComplete/NPC_PARTITION/PARTITION_Class.cs) |
| Weighted undirected graph | `{(N,E) \| N is set, E subset {(e, w) \| e is N unorderedcross N, w is int}}` | [MAXCUT_Class.cs](../../Problems/NPComplete/NPC_MAXCUT/MAXCUT_Class.cs) |
| Unweighted undirected graph | `{(N,E) \| N is set, E subset N unorderedcross N}` | the template's comments |

**Match existing formats.** If your problem is a graph, a set, or a number list, use the same instance shape as the problems above, so the GUI and reductions can share code. For graph problems, implement `IGraphProblem<Solver, Verifier, Visualization, Graph>` and expose a `graph` property (see [MAXCUT_Class.cs](../../Problems/NPComplete/NPC_MAXCUT/MAXCUT_Class.cs) and the graph helpers in [Interfaces/graphs](../../Interfaces/graphs/)).

**Throw `ProblemParseException` on bad input.** [Interfaces/ParseExceptions.cs](../../Interfaces/ParseExceptions.cs) defines it. Its arguments are the problem name, the text you received, and a short reason. The controllers catch it and answer with HTTP 400 and your `instanceFormat`, so a caller can fix the input. The worked example does it twice:

```csharp
} catch (Exception ex) {
    throw new ProblemParseException(problemName, instance, ex.Message);
}

foreach (string element in parsedS) {
    if (!int.TryParse(element, out _))
        throw new ProblemParseException(problemName, instance,
            $"'{element}' is not a valid integer in S");
}
```

Two habits from the worked example:

- **Read everything inside the `try`.** SPADE can accept a string and only fail later, when you list its parts or ask for a missing name. So call `.ToList()` and `.ToString()` inside the `try`.
- **Check the contents, not only the shape.** SPADE says `{1,x,3}` is a set; only your own check notices `x` is not a number.

If you forget, the API still protects itself. `ParseGuard` ([ParseExceptions.cs](../../Interfaces/ParseExceptions.cs)) wraps any other exception thrown by your constructor into a `ProblemParseException`, so the caller gets a 400 and not a 500. Throwing it yourself gives a better message.

**Hand parsing** (without SPADE) is allowed for odd formats, as long as the same rules hold: a clear error on bad input, never a crash.

### Step 4. Add the solver, verifier, and visualization

Your class refers to these three by name, so they must exist before it compiles. Follow the matching guide for each:

1. [adding-a-solver.md](adding-a-solver.md): at least one solver, in `Solvers/`. It becomes `defaultSolver`.
2. [adding-a-verifier.md](adding-a-verifier.md): at least one verifier, in `Verifiers/`. It becomes `defaultVerifier`. Its `CertificateGrammar` and `CertificateExample` constants are what your `certificateFormat` quotes, so the two stay in sync.
3. [adding-a-visualization.md](adding-a-visualization.md): a real visualization in `Visualizations/`, or `DummyVisualization`.

Tip: get the problem class compiling with a very small solver and verifier first, then improve them.

### Step 5. Reductions (optional)

If your problem should connect to others, add reductions. They go in `ReduceTo/` inside your folder, see [adding-a-reduction.md](adding-a-reduction.md). A problem with no reductions is allowed.

## 5. Write tests

### Tests that already check your problem for free

You do not have to register your problem with these tests. They find it automatically (they look at every problem Redux discovers), and each one runs over every problem. Run them all with the normal test command in [building-and-testing.md](building-and-testing.md).

**These tests will check your problem automatically:**

| Test file | What it checks about your problem |
| --- | --- |
| [Metadata/ProblemInstantiation_Tests.cs](../../redux-tests/Metadata/ProblemInstantiation_Tests.cs) | Your class can be built with its empty constructor, which means `defaultInstance` parses. A problem that throws here silently disappears from several endpoints. |
| [Metadata/ComplexityClass_Tests.cs](../../redux-tests/Metadata/ComplexityClass_Tests.cs) | `complexityClass` is not `Unclassified`, and the value is served as text (`"NPComplete"`) by the API. |
| [Metadata/ProblemType_Tests.cs](../../redux-tests/Metadata/ProblemType_Tests.cs) | `problemType` is not `Unclassified`. |
| [Metadata/NamingConvention_Tests.cs](../../redux-tests/Metadata/NamingConvention_Tests.cs) | The class name has no `P_`, `NPC_`, or `NPH_` prefix. Also, your verifier is named exactly `"Default <problemName> Verifier"`, and your solver and reduction names match the naming shapes (see their guides). |
| [Metadata/VisualizationType_Tests.cs](../../redux-tests/Metadata/VisualizationType_Tests.cs) | Your visualization's `visualizationType` is not `Unimplemented` (only a short list of existing classes, including `DummyVisualization`, is excused), and the list of visualization types matches the manifest [Documentation/visualization-types.json](../visualization-types.json) (only matters if you add a new type). |
| [Metadata/SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) | Your solver declares its type, complexity bucket, and Big-O text (see [adding-a-solver.md](adding-a-solver.md)). |
| [Navigation/ContributorProfile_Tests.cs](../../redux-tests/Navigation/ContributorProfile_Tests.cs) | Every name in `contributors` (problems, solvers, verifiers, visualizations, reductions) has an entry in `wwwroot/contributorInfo.json`; the file is valid; committed stats are zero; no GitHub account is shared between two people. |
| [Endpoints/ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs) | Sending garbage (`""`, `{{{`, `not an instance`, `{a,b}:::`) to `/problemInstance` for your problem, and to your solver, verifier, and visualization, **never produces a 500**. A bad instance must be a 400. |
| [Endpoints/Navigation_Endpoint_Tests.cs](../../redux-tests/Endpoints/Navigation_Endpoint_Tests.cs) | The navigation lists put each problem in the list for its declared class, and find its solvers, verifiers, and visualizations. **This one needs an edit from you:** it keeps a hard-coded list of every problem in each class (`NpcProblems_MembershipIsExactlyDeclaredNPComplete`, `PProblems_MembershipIsExactlyDeclaredP`, `NpHardProblems_MembershipIsExactlyDeclaredNPHard`), so the API's lists never change by accident. Add your problem's class name (for example `"SUBSETSUM"`) to the list for the `complexityClass` you declared. |
| [Endpoints/ProblemProvider_Endpoint_Tests.cs](../../redux-tests/Endpoints/ProblemProvider_Endpoint_Tests.cs) | `/info` and the other `ProblemProvider` endpoints work across the discovered classes. |
| [Endpoints/Performance_Tests.cs](../../redux-tests/Endpoints/Performance_Tests.cs) | The `allInfo` and `allProblems` lists, and startup, stay inside a time budget. Building every problem's default instance counts, so keep your constructor fast. These run as a separate "Performance" step; see [building-and-testing.md](building-and-testing.md). |

The reduction tests ([ReductionSmoke_Tests.cs](../../redux-tests/Metadata/ReductionSmoke_Tests.cs), [ReductionValidity_Tests.cs](../../redux-tests/Metadata/ReductionValidity_Tests.cs), and others) also use your problem's `complexityClass`, which is one more reason to get it right.

Those tests only prove the basics. They do not prove your problem, solver, or verifier is **correct**. That is your job.

> Do not edit the "allowlist" arrays inside those test files to make a failure go away. They are meant to stay empty (or only hold old, known exceptions). Fix the problem instead.

### Tests you write yourself

> **The build check won't tell you if you skipped this.** CI checks that every test passes, not that you wrote any. Your own tests are the only thing that proves your code gives the right answers, so reviewers will look for the ones in this guide's checklist. See [building-and-testing.md](building-and-testing.md#4-how-to-read-the-rbs-report).

Problem tests live in the test project, one folder per problem:

```text
redux-tests/Problems/<Prefix>_<NAME>/<NAME>_Tests.cs
```

**If you used the template download in Step 1, you already have this file.** It is the last file in the zip, `redux-tests/Problems/NPC_<NAME>/<NAME>_Tests.cs`, and it is generated for your problem's name. The generic tests (the default instance builds, bad instances and bad certificates throw the right exceptions, the solver's answer is accepted by the verifier) pass once your code follows the rules. The stub tests (the default instance's fields, a valid certificate, a wrong certificate, no solution, the timer) contain a `TODO` and fail on purpose until you replace each `TODO` with a value you worked out by hand. Copy-paste examples of finished tests are in [building-and-testing.md, section 5](building-and-testing.md#5-required-tests-for-each-kind-of-change).

If you copied the template by hand, create the file yourself at that path, using the template [PROBLEM_Tests.txt](../../ProblemTemplate/Templates/Tests/PROBLEM_Tests.txt) the same way as the other templates.

The worked example's tests are in [redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). That file covers, in order:

- **Instantiation**: the default instance builds, and `S` and `T` hold the expected values.
- **Bad instances**: a theory (a test that runs once per listed input) that checks the constructor throws `ProblemParseException` for old formats, empty text, a bare string, and non-integers.
- **Verifier**: valid certificates accepted, invalid ones rejected, and malformed ones throw `CertificateParseException`.
- **Solver**: the solver's answer passes the verifier, a one-element solution is found, and "no solution" returns an empty set.
- **Reductions** that start from the problem.

A test from that file looks like this:

```csharp
[Theory]
[InlineData("{{1,7,12,15} : 28}")] // old colon format
[InlineData("")]                    // empty
[InlineData("101")]                 // bare string
[InlineData("({1,x,3},5)")]         // non-integer element in S
[InlineData("({1,2,3},y)")]         // non-integer T
public void SUBSETSUM_Constructor_Throws_On_Invalid_Instance(string instance) {
    Assert.Throws<ProblemParseException>(() => new SUBSETSUM(instance));
}
```

Things to copy:

- `using Xunit;` plus `using` lines for your problem's namespaces, `namespace redux_tests;`, and a `public class <NAME>_Tests`.
- `[Fact]` for a single check and `[Theory]` with `[InlineData(...)]` for the same check on many inputs.
- Test names that read like sentences: `<PROBLEM>_<What>_<Expectation>`.
- **Small hand-picked instances** whose answers you worked out on paper.

A minimal outline for your own problem (angle brackets are yours to fill in):

```csharp
[Fact]
public void NAME_Default_Instantiation() {
    NAME problem = new NAME();
    Assert.Equal("<default instance>", problem.instance);
    Assert.Equal(<expected>, problem.<field>);
}

[Theory]
[InlineData("")]
[InlineData("<an instance with a wrong value>")]
public void NAME_Constructor_Throws_On_Invalid_Instance(string instance) {
    Assert.Throws<ProblemParseException>(() => new NAME(instance));
}
```

Aim for at least: one instantiation test, one bad-instance test, and the verifier and solver tests described in their guides.

## 6. Build, test, and get CI green

Run these from the repo root (the folder that contains `Redux.slnx`). The full explanation, with what each result looks like and what to do when CI is red, is in [building-and-testing.md](building-and-testing.md).

```bash
dotnet format Redux.slnx
dotnet build Redux.slnx -c Release
dotnet test Redux.slnx -c Release --filter "Category!=Performance"
dotnet test Redux.slnx -c Release --filter "Category=Performance"
```

To run only your own tests while you work:

```bash
dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~SUBSETSUM"
```

(Replace the text after `~` with part of your test names.)

To see your problem working, start the API (see [setup.md](setup.md)) and call `GET /ProblemProvider/info?interface=<ClassName>`. Success looks like a JSON object with your `problemName`, `defaultInstance`, `instanceFormat`, and the declared `complexityClass` as text.

## 7. Definition of done

A new problem is done when:

- It implements the interfaces correctly.
- It has at least one solver and one verifier.
- Its tests pass (including the automatic ones above).
- Every metadata field is filled in. No `"TODO"` is left, and `complexityClass` and `problemType` are declared.

## 8. Checklist before opening the PR

- [ ] The folder is `Problems/<Folder>/<Prefix>_<NAME>/`, the file is `<NAME>_Class.cs`, and the namespace matches the folder.
- [ ] The class name is unique in the whole project and has no `NPC_`/`NPH_`/`P_` prefix.
- [ ] The class implements `IProblem<Solver, Verifier, Visualization>` (or `IGraphProblem<...>`) and has both constructors.
- [ ] `problemName`, `problemLink`, `formalDefinition`, `problemDefinition`, `inputDescription`, `outputDescription`, `source`, and `sourceLink` are filled in.
- [ ] `defaultInstance` parses, and `instanceFormat` and `certificateFormat` each have a real example.
- [ ] `complexityClass` and `problemType` are declared (not `Unclassified`), and the folder matches the class.
- [ ] Your problem's class name is added to the matching hard-coded list in [Navigation_Endpoint_Tests.cs](../../redux-tests/Endpoints/Navigation_Endpoint_Tests.cs) (NP-complete, P, or NP-hard).
- [ ] Bad instances throw `ProblemParseException`.
- [ ] There is a default solver, verifier, and visualization (or `DummyVisualization`), each following its own guide.
- [ ] Every name in every `contributors` list is in `wwwroot/contributorInfo.json`, with stats left at zero.
- [ ] You added `redux-tests/Problems/<Prefix>_<NAME>/<NAME>_Tests.cs`.
- [ ] `dotnet format Redux.slnx` was run, and build and tests pass.
- [ ] You read the rbs report on your PR (see [building-and-testing.md](building-and-testing.md)).
- [ ] The PR targets `CSharpAPI`.

## 9. Common mistakes

- **Running `dotnet build` with no file name.** It fails with `MSB1011`. Always add `Redux.slnx`.
- **Skipping `dotnet format`.** Bad formatting fails the Release build itself, not only a separate check.
- **Leaving `complexityClass` or `problemType` as `Unclassified`.** The generated template declares both as `Unclassified` on purpose. Tests fail with a message naming your class until you pick real values.
- **Forgetting the hard-coded list in `Navigation_Endpoint_Tests.cs`.** This failure only appears *after* you declare `complexityClass`, so it can look like fixing one test broke another. The message says `NPC_ProblemsRefactor membership changed` (or the P / NP-hard version) and prints an Expected and an Actual list. Your problem is the one name in Actual that is missing from Expected. Add it to that test's list.
- **A class name with a prefix** such as `NPC_MYPROBLEM`. The folder gets the prefix, the class does not.
- **Reusing a class name**, even with different capitals. Names are keys, ignoring case. A duplicate crashes startup.
- **Folder and declared class disagree**, or the namespace does not match the folder.
- **A `defaultInstance` that does not parse.** The empty constructor fails, and the problem vanishes from several endpoints.
- **Not throwing `ProblemParseException`** (or letting a weird exception escape from the solver instead of the constructor). Garbage input must give a 400, never a 500.
- **Only checking the shape of the input.** `{1,x,3}` looks like a set. Check that each value is a number.
- **Renaming the generated verifier away from `"Default <Problem Name> Verifier"`.** The template already uses that name, and a test requires it. If you change the problem's `problemName`, change the verifier's name to match.
- **A generated visualization left as `Unimplemented`.** Finish it or use `DummyVisualization`.
- **A name in `contributors` that is not in `contributorInfo.json`**, or a first name only. Use the full name as it appears in the file.
- **Committing non-zero contributor stats.** Leave them at zero.
- **Writing a controller.** Not needed. Reflection exposes your class.
- **Editing the test allowlists** to turn a failure green. Fix the cause.
