## Problem Template

> **New here?** This page is a field-by-field reference. For step-by-step instructions and the checklists to finish before a pull request, use the contributor guides: <https://github.com/ReduxISU/Redux/blob/CSharpAPI/Documentation/guides/README.md> (adding a problem, solver, verifier, reduction, or visualization).

New problems go in the back end repository in the `Problems/NPComplete` folder. (Problems from other complexity classes live in `Problems/NPHard` and `Problems/P`; which class a problem really belongs to is decided by the `complexityClass` it declares, not by its folder.) All files relating to a problem go in a folder titled `NPC_PROBLEMNAME`. This folder should include:
* A file named `PROBLEMNAME_Class.cs`
* A folder named "Solvers"
* A folder named "Verifiers"
* A folder named "Visualizations" (if the problem has a visualization; otherwise use `DummyVisualization`)
* A folder named `ReduceTo/` (if the problem has reductions). Each reduction goes in `ReduceTo/NPC_<TO>/`, where `<TO>` is the problem it reduces **to**.

### Where each file in the zip goes

Do not unzip the download over the repo. Copy each file to the place below (paths are from the repo root, the folder that contains `Redux.slnx`; `NPC_SUBSETSUM` and `SubsetSum` stand for your problem's names):

| The file in the zip | Where it goes in the repo |
| --- | --- |
| `README.md` | Nowhere. This page is reference only. |
| `NPC_SUBSETSUM/SUBSETSUM_Class.cs` | `Problems/NPComplete/NPC_SUBSETSUM/SUBSETSUM_Class.cs` |
| `NPC_SUBSETSUM/Solvers/SubsetSumSolver.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Solvers/SubsetSumSolver.cs` |
| `NPC_SUBSETSUM/Verifiers/SubsetSumVerifier.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Verifiers/SubsetSumVerifier.cs` |
| `NPC_SUBSETSUM/Visualizations/SubsetSumVisualization.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Visualizations/SubsetSumVisualization.cs` (delete it, and the folder, if you use `DummyVisualization`) |
| `redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs` | `redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs` (some tests contain a `TODO` and fail on purpose until you fill them in) |

The separate downloads work the same way. A solver zip has `NPC_<PROBLEM>/Solvers/<Name>.cs` (goes to `Problems/NPComplete/NPC_<PROBLEM>/Solvers/`), a verifier zip has `NPC_<PROBLEM>/Verifiers/<Name>.cs`, a visualization zip has `NPC_<PROBLEM>/Visualizations/<Name>.cs`, and a reduction zip has `NPC_<FROM>/ReduceTo/NPC_<TO>/<Name>.cs` (goes to `Problems/NPComplete/NPC_<FROM>/ReduceTo/NPC_<TO>/`). Use the `NPH_` or `P_` folder and prefix, and fix the namespaces, if the problem lives under `Problems/NPHard/` or `Problems/P/`.

The full explanation, with a table for each download, is in the guides: <https://github.com/ReduxISU/Redux/blob/CSharpAPI/Documentation/guides/adding-a-problem.md#where-each-file-in-the-zip-goes>. The tests to write are listed in <https://github.com/ReduxISU/Redux/blob/CSharpAPI/Documentation/guides/building-and-testing.md#5-required-tests-for-each-kind-of-change>.

The generated files spell out every member you must decide. Metadata is declared as `Unclassified` (or an empty string) with a `TODO` comment, and unfinished methods throw `NotImplementedException`. Tests fail until you replace each of those, and that is intentional.

Members that come from the interfaces are required, unless they are marked "optional" below. Members that are marked "convention" are not part of the interface, but every problem, solver, and so on carries them and the website shows them.

### Problem Class
The `PROBLEMNAME_Class.cs` should implement the `IProblem` interface or one of its variants, found in `Interfaces/ProblemInterface.cs`. This includes
* `string problemName` : Human readable problem name, this is what the problem will appear as in the GUI
* `string problemLink` : (convention) Link to information on the problem, not necessarily a formal definition
* `string formalDefinition` : Definition in the form of {[problem variables] | "definition" }
* `string problemDefinition` : A more easily readable form of the above definition.
* `string inputDescription` : A short plain-English name for the problem's input, e.g. "Φ, a boolean formula"
* `string outputDescription` : A short plain-English description of the problem's output, e.g. "True or False"
* `string source` : A formal citation of the source material for the problem definition
* `string sourceFile` : Repo-relative path of the class's own source file. Leave it as the template's `SourceFile.Path()`; the compiler fills in the path.
* `string sourceLink` : (convention) A link to the formal citation
* `string wikiName` : Deprecated and unused, but still a required member of the interface. Declare it as an empty string (`= ""`) and do not put anything in it.
* `string defaultInstance` : A reasonably sized example of the problem, and the necessary format. *If the problem is of a similar form to an existing problem, such as a directed graph, the format should match the existing problems.*
* `string instance` : The instance string this object was built from.
* `string instanceFormat` : Short descriptive sentence with a concrete embedded example of the instance string the problem accepts. Consumed by `/ProblemProvider/info` and by parse-error responses, so this is the canonical place to tell an LLM, the GUI, or a human what shape your instances take. Prefer a sentence + example over a formal grammar. (The interface supplies an empty default, but every problem should fill it in.)
* `string certificateFormat` : Same shape as `instanceFormat`, but describes what the verifier accepts as a certificate. The default verifier defines this; if your problem has multiple verifiers, declare the format the *default* one expects here.
* `ComplexityClass complexityClass` : Declared by a person, never guessed. The template declares it as `Unclassified`, and the metadata tests fail while it stays that way.
* `ProblemType problemType` : Declared subject-matter category (Garey and Johnson's taxonomy). Same rule as `complexityClass`; the template declares it as `Unclassified` too.
* `string[] contributors` : A list of names of all developers who have worked on the problem
* `T defaultSolver` : An object of the default solver for the problem
* `U defaultVerifier` : An object of the default verifier for the problem
* `V defaultVisualization` : An object of the default visualization for the problem

The `PROBLEMNAME_Class.cs` should also include any necessary problem variables, any functions necessary for parsing string instances into a variable, and two constructors. One constructor that takes a string instance, and one which uses the default instance.

The string-instance constructor should **throw `ProblemParseException`** (defined in `Interfaces/ParseExceptions.cs`) on malformed input rather than silently constructing a half-populated object. (Any other exception thrown while parsing, for example from SPADE or `int.Parse`, is turned into one for you.) The controller layer catches this exception and returns HTTP 400 with `instanceFormat` quoted back to the caller, which is what lets every client (MCP wrappers, the GUI, an LLM agent) recover from a bad input by reading the structured error rather than parsing a stack trace.

### Reductions
The `ReduceTo/NPC_<TO>/` folder contains the reduction files from this problem to problem `<TO>`. Each implements the `IReduction<FROM, TO>` interface found in `Interfaces/ReductionInterface.cs`. This includes
* `string reductionName` : Human readable name of reduction algorithm, this is what will appear in the GUI
* `string reductionDefinition` : A brief description of the algorithm used to reduce the problem
* `string source` : Formal citation of the source of the reduction algorithm
* `string sourceFile` : Repo-relative path of the class's own source file. Leave it as the template's `SourceFile.Path()`; the compiler fills in the path.
* `string sourceLink` : (convention) A link to the formal citation
* `string[] contributors` : A list of names of all developers who have worked on the reduction
* `List<Gadget> gadgets` : (optional, defaults to an empty list) A list of gadgets used in the reduction to visually represent the reduction elements
* `{FROM} reductionFrom` : An instance of the problem we are reducing from (the template keeps it in a private `_reductionFrom` field)
* `{TO} reductionTo` : An instance of the problem we are reducing to (the template keeps it in a private `_reductionTo` field)
* `{TO} reduce()` : The reduction algorithm itself
* `string mapSolutions(string)` : Turns a solution of the FROM problem into the matching solution of the TO problem
* `ReductionCost cost`, `ReductionType reductionType`, `ReductionComplexityBucket complexityBucket` : Declared by a person, never guessed. The template declares them as `Unclassified`, and the metadata tests fail while they stay that way.
* `string? complexity` : Free-text Big-O note about running time. It is not part of the interface, but the metadata tests require it to be non-empty, so the template declares it as `""` for you to fill in.

### Solvers
The Solvers folder should contain all solver files for that problem. Each of which implements the `ISolver` interface found in `Interfaces/SolverInterface.cs`. This includes
* `string solverName` : Human readable name of solving algorithm, this is what will appear in the GUI
* `string solverDefinition` : A brief description of the algorithm used to solve the problem
* `string source` : Formal citation of the source of the solving algorithm
* `string sourceFile` : Repo-relative path of the class's own source file. Leave it as the template's `SourceFile.Path()`; the compiler fills in the path.
* `string[] contributors` : A list of names of all developers who have worked on the solver
* `bool timerHasExpired` : bool that says if the timer for a problem has expired. Check it now and then in long loops and return if it is true.
* `SolverType solverType`, `SolverComplexityBucket complexityBucket` : Declared by a person. The template declares them as `Unclassified`, and the metadata tests fail while they stay that way.
* `string complexity` : A Big-O string. The template declares it as `""` and a metadata test fails until you fill it in. Only write one you are confident of.

The file should also include a function which takes a problem object and returns a string of the solution, as well as any other necessary functions. Optionally it can provide `GetSteps` for step-by-step views.\
*Because for now, most problems are NP-Complete, solution algorithms should return a complete solution.*

### Verifiers
The Verifiers folder should contain all verifier files for that problem. Each of which implements the `IVerifier` interface found in `Interfaces/VerifierInterface.cs`. This includes
* `string verifierName` : Human readable name of verifier, this is what will appear in the GUI. It must be exactly `Default <Problem Name> Verifier` (checked by `NamingConvention_Tests`).
* `string verifierDefinition` : A brief description of the algorithm used to verify the problem
* `string source` : Formal citation of the source of the verifier algorithm
* `string sourceFile` : Repo-relative path of the class's own source file. Leave it as the template's `SourceFile.Path()`; the compiler fills in the path.
* `string sourceLink` : (convention) A link to the formal citation
* `string certificate` : The certificate this verifier was last given. The template supplies it.
* `string[] contributors` : A list of names of all developers who have worked on the verifier

The file should also include a function which takes a problem object and certificate, and returns a Boolean for if the certificate is a solution to the given problem. As well as any other necessary functions.

The verifier should **throw `CertificateParseException`** (defined in `Interfaces/ParseExceptions.cs`) on malformed certificate input rather than silently returning `false`. The controller catches it and returns HTTP 400 with `problem.certificateFormat` quoted back to the caller. Return `false` only when the certificate parses successfully but fails the verification predicate.

### Visualizations
The Visualizations folder should contain all visualization files for that problem. Each of which implements the `IVisualization` interface found in `Interfaces/VisualizationInterface.cs`. This includes
* `string visualizationName` : Human readable name of visualization, this is what will appear in the GUI
* `string visualizationDefinition` : A brief description of the visualization used to visualize the problem
* `string source` : Formal citation of the source of the visualization algorithm
* `string sourceFile` : Repo-relative path of the class's own source file. Leave it as the template's `SourceFile.Path()`; the compiler fills in the path.
* `string sourceLink` : (convention) A link to the formal citation
* `VisualizationType visualizationType` : Which renderer the GUI should use for the JSON you return. The template uses `Unimplemented`, and a test fails until you pick a real type. If no renderer fits, use `DummyVisualization` as the problem's default visualization instead. Adding a new type means updating `Documentation/visualization-types.json` and adding a renderer in Redux_GUI.
* `string[] contributors` : A list of names of all developers who have worked on the visualization
* `ISolver solver` : The solver to use for this visualization. The template uses the problem's default solver, which is fine if `StepsVisualization` is not implemented

The file should also include a `visualize` function which takes a problem object and returns an API object of the visualization for the given problem. `SolvedVisualization` (problem plus solution string) and `StepsVisualization` are optional. As well as any other necessary functions.

## Before you open a pull request

The checklists for what "done" means are in the guides, one per kind of addition, so they stay current in one place: <https://github.com/ReduxISU/Redux/blob/CSharpAPI/Documentation/guides/README.md>.
