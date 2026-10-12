# Adding a reduction

This guide walks you through adding a new **reduction** to Redux, from an empty file to a green pull request (PR). It uses a real, small reduction already in the repo as the worked example.

New to the repo? Read the [guides index](README.md) first. When you reach the "build and test" step, the details live in [building-and-testing.md](building-and-testing.md).

---

## 1. What a reduction is

A **reduction** turns a question about problem A into a question about problem B, so that answering B also answers A.

Everyday analogy: you want to know whether you can pay exactly $28 using the coins in your pocket. A friend only knows how to answer "can this pile of money be split into two equal halves?". You can still get your answer. You add two extra coins to the pile, ask your friend, and read the result as the answer to your original question. You never solved your problem yourself. You rewrote it as one your friend can solve.

In Redux:

- Problem A is the **FROM** problem. Problem B is the **TO** problem.
- A reduction is one C# class. It takes an instance of A (a string such as `({3,5,7},8)`), builds an instance of B, and exposes it as `reductionTo`.
- A reduction also does **solution mapping**: given a solution to the FROM problem (A), it produces the matching solution to the TO problem (B). In code this is the `mapSolutions` method.
- You do not write a controller or register anything. Redux finds every reduction class automatically (see section 3), and the API exposes it.

## 2. Before you start

1. **Both problems must already exist** in `Problems/NPComplete/`. If the FROM or TO problem is missing, add it first. See [adding-a-problem.md](adding-a-problem.md).
2. Work on a branch based on `CSharpAPI`. Pull requests go back to `CSharpAPI`.

   ```bash
   git fetch origin
   git switch -c add-reduction-subsetsum-to-partition origin/CSharpAPI
   ```

   Pick your own branch name, for example `add-reduction-<from>-to-<to>`.
3. Make sure the project builds before you change anything. See [building-and-testing.md](building-and-testing.md).

## 3. Where the file goes

A reduction file lives inside the folder of the problem it reduces **from**, then in a folder named for the problem it reduces **to**:

```text
Problems/NPComplete/NPC_<FROM>/ReduceTo/NPC_<TO>/<ClassName>.cs
```

Rules that matter:

| Thing | Rule | Worked example |
| --- | --- | --- |
| Folder | `NPC_<FROM>/ReduceTo/NPC_<TO>/` | `NPC_SUBSETSUM/ReduceTo/NPC_PARTITION/` |
| File name | Same as the class name | `SubsetSumToPartitionReduction.cs` |
| Namespace | `API.Problems.NPComplete.NPC_<FROM>.ReduceTo.NPC_<TO>` | `API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION` |
| Class name | Unique across the whole project (see below) | `SubsetSumToPartitionReduction` |
| Implements | `IReduction<FROM, TO>` | `IReduction<SUBSETSUM, PARTITION>` |

**How Redux finds your reduction.** At startup, `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) scans every class in the project. Any class that implements `IReduction` is registered under its **lowercased class name**. Because of that:

- Your class name must be unique. Two classes with the same name (ignoring upper/lower case) crash startup.
- You do not edit any list. Creating the class is enough.
- The reduction graph at `/Navigation/Reductions` ([Nav_Reductions.cs](../../AdditionalControllers/Navigation/Nav_Reductions.cs)) is built from the two type arguments of `IReduction<FROM, TO>`. Pick them carefully.

The API endpoints you get for free (all in `ProblemProvider.cs`): `POST /ProblemProvider/reduce`, `/mapSolution`, `/gadgets`, and `/visualizeReduction`, each taking `?reduction=<ClassName>`.

> Older docs said each reduction needs its own controller. That is out of date. Reflection does the wiring now.

Other real examples to compare against:

- [`GraphColoringToCliqueCover.cs`](../../Problems/NPComplete/NPC_GRAPHCOLORING/ReduceTo/NPC_CLIQUECOVER/GraphColoringToCliqueCover.cs) (also builds gadgets for the visualization)
- [`sipserReductionVertexCover.cs`](../../Problems/NPComplete/NPC_CLIQUE/ReduceTo/NPC_VertexCover/sipserReductionVertexCover.cs) (a longer one; its folder is spelled `NPC_VertexCover`, which is unusual, so follow the all-caps style of the other folders instead)

## 4. Step by step

### Step 1. Get the template

You have two options. Both give you the same file.

- **Copy it by hand.** The template is [ProblemTemplate/Templates/ReduceTo/NPC_PROBLEM/Reduction.txt](../../ProblemTemplate/Templates/ReduceTo/NPC_PROBLEM/Reduction.txt). Copy it to your new folder, rename it `<ClassName>.cs`, and replace the placeholders `{REDUCE_FROM}`, `{REDUCE_TO}`, `{REDUCTION_PASCAL_CASE}`, and `{REDUCTION}`.
- **Download it from a running API.** Start the API, then call `GET /ProblemTemplate/reduction?problemFrom=SUBSETSUM&problemTo=PARTITION&reductionName=Partition Reduction`. It returns a zip with the placeholders already filled in. The code for this is in [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs).

  **Where the file in the zip goes.** The zip has one entry, and no README. The path is from the repo root (the folder that contains `Redux.slnx`):

  | The file in the zip | Where it goes in the repo | What it is, and what to do with it |
  | --- | --- | --- |
  | `NPC_SUBSETSUM/ReduceTo/NPC_PARTITION/PartitionReduction.cs` | `Problems/NPComplete/NPC_SUBSETSUM/ReduceTo/NPC_PARTITION/PartitionReduction.cs` | Your reduction. Several members throw `NotImplementedException` or are left as `Unclassified` until you fill them in. The zip always uses the `NPC_` prefix: if the FROM or TO problem lives under `Problems/NPHard/` or `Problems/P/`, use that problem's own folder name (`NPH_` or `P_`) and fix the namespace. |


The template's own notes are also worth a read: [ProblemTemplate/Templates/README.md](../../ProblemTemplate/Templates/README.md).

### Step 2. Fill in the members

Every reduction implements the members in [Interfaces/ReductionInterface.cs](../../Interfaces/ReductionInterface.cs). Here is each one in plain words, with the value used by the worked example,
[`SubsetSumToPartitionReduction.cs`](../../Problems/NPComplete/NPC_SUBSETSUM/ReduceTo/NPC_PARTITION/SubsetSumToPartitionReduction.cs) (Subset Sum to Partition, from Karp's 1972 paper).

| Member | What it is for | Worked example value |
| --- | --- | --- |
| `reductionName` | The name people see in the GUI. A test checks its shape (see section 5). | `"Partition Reduction"` |
| `reductionDefinition` | One sentence on what the algorithm does. | `"Karp's Reduction from Subset Sum to Partition"` |
| `source` | A proper citation for where the algorithm comes from. | Karp, "Reducibility among combinatorial problems", 1972 |
| `sourceFile` | The repo-relative path of this class's source file. The template sets it to `SourceFile.Path()`; leave that line as it is, because the compiler fills in the path. | `SourceFile.Path()` |
| `sourceLink` | A link to that source. The template includes it. | the Karp PDF link |
| `contributors` | Names of the people who wrote it. | `{ "Andrija Sevaljevic" }` |
| `cost` | How much bigger the produced instance is than the input (see below). | `ReductionCost.Linear` |
| `reductionType` | What kind of proof technique the construction uses (see below). | `ReductionType.Restriction` |
| `complexityBucket` | How long `reduce()` takes to run (see below). | `ReductionComplexityBucket.Linear` |
| `complexity` | Free-text Big-O note about running time. It is not part of the interface, but a metadata test requires it to be non-empty. | `"O(n), n = \|SUBSETSUM.S\|"` |
| `reductionFrom` | The FROM problem instance you were given. | a `SUBSETSUM` |
| `reductionTo` | The TO problem instance you produced. | a `PARTITION` |
| `reduce()` | **The actual algorithm.** Reads `reductionFrom`, builds and returns the TO instance. | see Step 3 |
| `mapSolutions(string)` | Takes a solution to the FROM problem and returns the matching solution to the TO problem. | see Step 4 |
| `gadgets` | Pieces the GUI draws to show how parts of A become parts of B. Optional: the interface supplies an empty list if you do nothing. Build each with `new Gadget(GadgetKind.Element, sourceIds, targetIds)`; `GadgetKind` is the one shared list (`Element`, `Group`, `EdgeRule`, `Palette`, `OrGadget`, `Bound`). Use the same ids as that problem's picture: graph problems use node names; SAT formulas use `c{i}` for clause i, `c{i}-{j}` for its j-th literal, `v:{name}` for a variable. Pass `legacyFromIds`/`legacyToIds` only if the old GUI ids differ (SAT uses `i` and `i-j`). | not used here |

The four **declared metadata** members (`cost`, `reductionType`, `complexityBucket`, and `complexity`) are the ones people forget. Their rules are in [ReductionCost.cs](../../Interfaces/ReductionCost.cs), [ReductionType.cs](../../Interfaces/ReductionType.cs), and [ReductionComplexityBucket.cs](../../Interfaces/ReductionComplexityBucket.cs). The doc comments there explain every choice. Short version:

- `cost`: read your `reduce()`. If the output has about as many numbers/nodes as the input, that is `Linear`. If it loops over all pairs, that is `Quadratic`. And so on.
- `reductionType`: `Restriction` (the instance is mostly relabeled), `LocalReplacement` (each piece is swapped for a fixed piece), or `ComponentDesign` (gadgets that must work together).
- `complexityBucket`: how long `reduce()` itself runs: `Linear`, `Polynomial`, and so on.
- `complexity`: a Big-O string for `reduce()`, such as `"O(n), n = |SUBSETSUM.S|"`. Only write one you are confident about.

"Declared" means a human decides. No tool computes these for you. The template declares each one as `Unclassified` (or `""` for `complexity`) with a `TODO` comment. **Do not leave them that way**: tests fail until you replace every value, on purpose (section 5). The template's `mapSolutions` also ends with a `NotImplementedException`, so an unfinished mapping fails loudly. `reduce()` is left as is, because it runs in the constructor.

The three constructors are the part you should not change. The template gives you all three, and the rest of Redux depends on them:

```csharp
public SubsetSumToPartitionReduction(SUBSETSUM from) { ... _reductionTo = reduce(); }
public SubsetSumToPartitionReduction(string instance) : this(new SUBSETSUM(instance)) { }
public SubsetSumToPartitionReduction() : this(new SUBSETSUM()) { }   // uses the default instance
```

The `string` one is what the API calls. The empty one is what the tests call.

### Step 3. Write `reduce()`

`reduce()` reads the FROM object's fields and builds the TO instance. In the worked example, a Subset Sum instance is a list of numbers `S` and a target `T`. For `({3,5,7},8)` it works like this:

1. Copy every number from `S` into the new Partition list: `3, 5, 7`.
2. Add `T + 1` = `9`.
3. Add `sum - T + 1` = `15 - 8 + 1` = `8`.
4. The result is the Partition instance `{3,5,7,9,8}`.

Then it stores the result in `reductionTo` and returns it. The part to copy as a pattern is the end of the method: build a string in the TO problem's instance format, set the TO object's fields (`S` and `instance` here), and return it.

Two rules for the output:

- It must be a valid instance string for the TO problem. A test feeds it back into the TO problem's constructor (section 5).
- It must be safe to call `reduce()` a second time on the same object. A test does exactly that.

### Step 4. Write `mapSolutions`

`mapSolutions(string problemFromSolution)` receives a solution to the **FROM** problem (as a string, in the FROM problem's certificate format) and must return the matching solution to the **TO** problem, in the TO problem's certificate format. It must handle bad input without crashing the server. The API wraps errors in a friendly 400 response, so throwing a normal exception on bad input is acceptable. Never let it cause a 500.

Be honest about what your method does. The worked example's `mapSolutions` is a simple stand-in (it returns the first number of the Partition list), and a few reductions in the repo return an empty string. These are known to be weak. Do not copy them. A good `mapSolutions` really converts the answer. The PR checklist in section 7 below expects a working solution mapping. If you truly cannot map a solution, say so in your PR description.

### Step 5 (optional). Record construction steps

A reduction can also tell the story of how it built the target, one step per gadget, in both problems' words ("Clause C2 (x1 | !x2 | x3) becomes nodes x1_1, !x2_1, x3_1"). The Reduction View plays these as its Build phase. Every reduction does it since #695; copy one of them (`SipserReduceToCliqueStandard` is the fullest for a graph target, `KarpIntProgStandard` the shortest for a table, `KarpVertexCoverToSetCover` for a set target, `LawlerKarp` for a directed one and `FengReduction` the shortest for a packing target).

1. Implement `IRecordedReduction<FROM, TO>` next to your other interfaces.
2. Move the body of `reduce()` into a private `Build(StepRecorder<BuildDelta> rec)` and make `reduce()` call `Build(StepRecorder<BuildDelta>.Off)`. `Build` starts with `gadgets.Clear()`, so calling `reduce()` again does not add a second copy of each gadget. Do the same for the constructor: a private one takes the recorder, the public ones pass `Off`. Implement `Rebuild(rec)` by constructing a fresh copy with the recorder and returning `new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps)`. Also add a public constructor `(string instance, StepRecorder<BuildDelta> rec)` that passes the recorder on: `/buildSteps` and `/reductionView` build your reduction with it, so one build gives both the reduction and its steps (a test checks every recorded reduction has it). The steps must come from the real build code. Do not write a second, parallel version of the construction to narrate it.
3. Inside `Build`, call `rec.Accept(...)` for each piece you build and `rec.Reject(...)` for a piece the rule leaves out. End with `rec.Done(new BuildDelta { Bound = K }, true, caption)`. Every step is recorded; when there are more than 150 before Done, `BuildRun` keeps the first 150 and folds the rest into one step whose caption says they were combined, so replaying the steps always gives the whole target. Pass lambdas for the delta and the caption. Anything you must prepare in order to record (a list of ids, a bucket per pair) goes behind `if (rec.Enabled)`, so that recording off costs nothing.
4. A step's delta (`BuildDelta`) holds only what that step **added**: the target `Nodes` and `Edges` (each a `new BuildEdge(u, v)`, which carries its two ends and the id the Graph picture gives that edge), the `Skipped` pairs (also `BuildEdge`s), the indices of the `Gadgets` it builds, and the `SourceFocus` ids it came from. Its `Focus` holds the target ids being looked at. Replaying the deltas in order must give exactly the target. Source ids use the gadget convention (graph nodes by name; formulas `c{i}`, `c{i}-{j}`, `v:{name}`).
   "Nodes" and "edges" are the target picture's pieces, whatever it calls them, and every piece of its payload is added by exactly one step:

   | Target picture | `Nodes` | `Edges` |
   | --- | --- | --- |
   | Graph | nodes | edges (`new BuildEdge(u, v)`) |
   | Grouped Graph (SAT, 3SAT) | clauses `c{i}`, literals `c{i}-{j}`, variables `v:{name}` | complement and clause edges, with the picture's own id: `new BuildEdge(GroupedGraphFrames.EdgeId(a, b), a, b)` |
   | Tripartite Matching (3DM) | elements `x{i}`, `y{i}`, `z{i}` and triples `T{i}` | none |
   | Table (0-1 ILP) | rows `r{i}` and columns `c{j}` (their cells come with them) | none |
   | Layered Graph (Feedback Arc Set, Feedback Node Set) | nodes, by `LayeredFrames.NodeId(name)` | arcs, `LayeredFrames.BuildArc(from, to)` (id `from>to`) |
   | Bipartite Graph (Set Cover, Exact Cover) | elements `BipartiteFrames.ElementId(name)` and sets `S{i}` | memberships, `BipartiteFrames.BuildMembership(element, setIndex)` |
   | Packing (Subset Sum, Knapsack, Partition) | items `PackingFrames.ItemId(i)` and containers `PackingFrames.ContainerId(c)` (Subset Sum's target, the knapsack, Partition's two sides) | none |

   A clause-by-clause CNF target can take each clause's pieces from `CnfPieces.ClausePieces`. Set Cover and Exact Cover read equal sets as one set, and the Bipartite Graph draws the sets as read, so name a set by its place among the sets the problem parses from your instance text (`new SETCOVER(instance).subsets`), not by its place in the text. Partition, Subset Sum and Knapsack are different: they keep every copy of a repeated number or item (#628), so a target whose numbers repeat is the one you built, and a picture names each copy by its place (`I1`, `I2`, ...). Read answers to them with `RepeatedElements.Parse` (or `PartitionVerifier.ReadSides` for Partition's two sides), not with a SPADE set, which keeps one copy. A target bound (K, T, W) goes in the step's `Bound`, which is an int: work numbers out in `long` and refuse an instance whose numbers do not fit with a `ReductionInputException` that says why (a 400), never a wrapped or rounded number (#829, #830).

5. Every gadget in `gadgets` must be named by exactly one step (a step names its gadgets in `Gadgets`; the target ids a gadget lists must be in that step's focus or among the edges it adds), and each step needs a caption that says what happened in plain words. Keep steps to one per gadget or per pair or node, not per edge, so default instances stay under the cap.
6. Test it the way `redux-tests/Reductions/BuildSteps_Tests.cs` does: the target built with recording equals the normal target, replaying the steps reproduces it, every gadget is named, and the ids are real. Add your reduction to the `/reduce` and `/gadgets` snapshot first, before touching the code.

The endpoint is `POST /ProblemProvider/buildSteps?reduction=...` with the source instance as the body. A reduction without steps answers 400 `{"error":"no_build_steps"}`.

### Step 6 (optional). Show it in the Reduction View

`POST /ProblemProvider/reductionView?reduction=...&solver=...` (source instance as the body) draws the whole reduction side by side: problem A and problem B each in their own picture, the gadget map between them, and three phases of frames: **Build** (your construction steps), **Solve** (B's solver: `solver=`, else the one the reduction names with `ReductionViewSolver()`, else B's default; 3SAT to Graph Coloring and to 3-Dimensional Matching name their backtracking solvers, since the brute forces do not finish their default formulas within the 10 s limit, #814) and **Map back** (B's answer turned into A's). It reuses the pictures the two problems already have; a reduction does not draw anything itself. To appear there a reduction needs four things:

1. **Stable gadget ids that match the pictures, with a kind.** Use the kind that says what the gadget is: `element` (one piece to one piece), `group` (one piece to many), `edgeRule` (the edges a rule adds, such as a complement's, filed under the node whose turn adds them), `palette`, `orGadget`, and `bound` for K, which names no piece on either side. Keep the legacy `reductionFromIds`/`reductionToIds` Redux_GUI reads as they were; pass `legacyToIds` when the picture's ids differ from them. Every id in a gadget's `sourceIds` must be an id in A's picture, and every `targetIds` id one in B's. The convention is in the `Gadget` class: graph nodes by name, edges `a-b`, and for formulas clause `c{i}`, its j-th literal `c{i}-{j}`, variable `v:{name}`. The Reduction View links the two panes through these ids, so a gadget id the picture does not have breaks the link.
2. **Construction steps** (Step 5 above): `IRecordedReduction<FROM, TO>`. Without them the view answers 400 `{"error":"no_build_steps"}`.
3. **A backward map**: `IReversibleReduction<FROM, TO, TPartial>`, where `TPartial` is A's answer shape from `Interfaces/Steps/Shapes.cs`. Its `MapBack` records one step per element of B's answer it reads (the step's focus is that B element): an `Accept` for a piece it puts into A's answer and a `Reject` for one it leaves out. The view draws the map as an incremental run, so a `Reject` marks only that piece and judges nothing else (the Packing picture counts it as still building since #695). Without it the view answers 400 `{"error":"no_backward_map"}`.
4. **Typed pictures on both ends.** A's and B's default visualizations must build frames (`IFramesVisualization`, for example `IGraphSubsetVisualization`), and their `StepShape` must be the shape the backward map and the solver record. Otherwise the answer is 400 `no_source_picture` or `no_target_picture`.

Every reduction does all of this today: 3SAT to Clique, Clique to Vertex Cover and Independent Set to Clique (#692); 3SAT to Graph Coloring, 3SAT to 3-Dimensional Matching, 3SAT to 0-1 Integer Programming and SAT to 3SAT (#693); the graph reductions (#694): Sipser's Clique to 3SAT, Lawler and Karp's Vertex Cover to Feedback Arc Set, Vertex Cover to Feedback Node Set and to Set Cover, Directed to Undirected Hamiltonian Cycle, and Graph Coloring to Clique Cover, to Exact Cover and to SAT; and the set and number reductions (#695): Karp's Exact Cover to Subset Sum, Hitting Set to Exact Cover, Partition to Weighted Cut, Feng's Subset Sum to Knapsack and Subset Sum to Partition. A new reduction should arrive with both. The tests that need a reduction without construction steps or a backward map use the test-only ones in `redux-tests/Reductions/TestOnlyReductions.cs`. `redux-tests/Endpoints/ReductionView_Graph_Tests.cs` and `ReductionView_SetNumber_Tests.cs` (Packing and Bipartite panes, numbers past an int) are the fullest test templates: they read every picture kind, check each case against an answer worked out by hand (yes maps to yes, no to no), run every solver of the target, and pin one whole payload per reduction. To check yours, add it to `Cases()` in `redux-tests/Reductions/Reduction_Output_Snapshot_Tests.cs` and to the `Pictures` table in `redux-tests/Endpoints/ReductionView_Endpoint_Tests.cs` (graph pictures), or to `Cases()` and `TargetPicture` in `redux-tests/Endpoints/ReductionView_Sat3_Tests.cs` (any picture), and run those tests: they check that every gadget id is in its picture, that the phases come in order, that the Build phase ends with exactly the target's nodes and edges, and that the last Solve and Map frames agree with the problems' own verifiers. The view has exactly one `Done` frame, its last, at the end of Map back, and only that frame carries `ok`: the Build phase ends on a plain "target built" `Accept`, and the Solve phase closes as an `Accept` (B's verifier accepts the answer) or a `Reject` (it does not, or the solver was stopped).

## 5. Write tests

### Tests that already check your reduction for free

You do not have to register your reduction with these tests. They find it automatically through the reduction graph, and each one runs once per reduction. All are in [redux-tests/Metadata/](../../redux-tests/Metadata/) unless stated.

**These tests will check your reduction automatically:**

| Test file | What it checks about your reduction |
| --- | --- |
| [ReductionSmoke_Tests.cs](../../redux-tests/Metadata/ReductionSmoke_Tests.cs) | Building it from the FROM problem's default instance works, `reduce()` does not throw, the output instance is not empty, and the output parses through the TO problem's string constructor. |
| [NamingConvention_Tests.cs](../../redux-tests/Metadata/NamingConvention_Tests.cs) | `reductionName` matches one of two shapes: `"<Problem> <Approach>"` (title-case words, 2 or more, for example `"Partition Reduction"`) or `"<Person's name> ... Reduction"` (for example `"Karp's 3SAT Reduction"`). A raw class name like `SubsetSumToPartition`, or a placeholder like `"Default Reduction"`, fails. |
| [ReductionCost_Tests.cs](../../redux-tests/Metadata/ReductionCost_Tests.cs) | `cost` is not `Unclassified`. |
| [ReductionType_Tests.cs](../../redux-tests/Metadata/ReductionType_Tests.cs) | `reductionType` and `complexityBucket` are not `Unclassified`, and `complexity` is not empty. |
| [ReductionValidity_Tests.cs](../../redux-tests/Metadata/ReductionValidity_Tests.cs) | You are not reducing from a harder complexity class to an easier one (for example NP-Complete to P). A failure here usually means one of the two problems has the wrong `complexityClass`. |
| [ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs) | Sending garbage to `/reduce`, `/gadgets`, and `/mapSolution` for your reduction never produces a 500 error. |
| [Performance_Tests.cs](../../redux-tests/Endpoints/Performance_Tests.cs) | `reduce()` on the default instance runs within a time budget. This runs as a separate "Performance" step; see [building-and-testing.md](building-and-testing.md). |

Those tests only prove the basics. They do not prove your algorithm is **correct**. That is your job.

> Do not edit the "allowlist" arrays inside those test files to make a failure go away. They are meant to stay empty. Fix the reduction instead.

### Tests you write yourself

> **The build check won't tell you if you skipped this.** CI checks that every test passes, not that you wrote any. Your own tests are the only thing that proves your code gives the right answers, so reviewers will look for the ones in this guide's checklist. See [building-and-testing.md](building-and-testing.md#4-how-to-read-the-rbs-report).

Reduction tests live in the test project, in one folder per FROM problem:

```text
redux-tests/Problems/NPC_<FROM>/<FROM>_Tests.cs
```

The worked example's tests are in [redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). If a test file for your FROM problem exists, add to it. If not, create one. An existing test for the worked example looks like this:

```csharp
[Fact]
public void SUBSETSUM_PartitionReduction_Accepts_New_Instance_Format() {
    SubsetSumToPartitionReduction reduction = new SubsetSumToPartitionReduction("({3,5,7},8)");
    Assert.Equal(3, reduction.reductionFrom.S.Count);
    Assert.NotNull(reduction.reductionTo);
}
```

Things to copy from the existing tests:

- `using Xunit;` and a `using` line for your reduction's namespace.
- `namespace redux_tests;` and a `public class <FROM>_Tests`.
- `[Fact]` for a single check. Test names read like sentences: `<PROBLEM>_<What>_<Expectation>`.
- Build the reduction with a **small hand-picked instance** whose answer you worked out on paper, then assert on `reductionTo` (and on `mapSolutions` if you implemented it).

A minimal outline for your own reduction (placeholders in angle brackets are yours to fill in):

```csharp
[Fact]
public void FROM_ToTO_ProducesExpectedInstance() {
    var reduction = new YourReductionClass("<small FROM instance>");
    Assert.Equal("<TO instance you worked out by hand>", reduction.reductionTo.instance);
}

[Fact]
public void FROM_ToTO_MapsSolutionBack() {
    var reduction = new YourReductionClass("<small FROM instance>");
    Assert.Equal("<expected solution>", reduction.mapSolutions("<a solution>"));
}
```

Aim for at least: one test for the produced instance, one for `mapSolutions`, and one for an edge case (an instance that has no solution, or the smallest allowed input).

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
dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~SUBSETSUM_PartitionReduction"
```

(Replace the text after `~` with part of your test name.)

## 7. Checklist before opening the PR

- [ ] The FROM and TO problems both already exist.
- [ ] The file is at `Problems/NPComplete/NPC_<FROM>/ReduceTo/NPC_<TO>/<ClassName>.cs`, and the namespace matches the folder.
- [ ] The class name is unique in the whole project.
- [ ] The class implements `IReduction<FROM, TO>` and has all three constructors.
- [ ] `reductionName`, `reductionDefinition`, `source`, `sourceLink`, and `contributors` are filled in (no `"TODO"` left).
- [ ] `cost`, `reductionType`, and `complexityBucket` are declared (not `Unclassified`), and `complexity` is a non-empty Big-O string.
- [ ] `reduce()` produces a valid TO instance string, and `mapSolutions` really maps solutions (or your PR says it does not).
- [ ] You added tests for the produced instance and for `mapSolutions`.
- [ ] `dotnet format Redux.slnx` was run, and build and tests pass.
- [ ] You read the rbs report on your PR (see [building-and-testing.md](building-and-testing.md)).
- [ ] The PR targets `CSharpAPI`.

## 8. Common mistakes

- **Running `dotnet build` with no file name.** It fails with `MSB1011`. Always add `Redux.slnx`.
- **Skipping `dotnet format`.** Bad formatting fails the Release build itself, not only a separate check.
- **Leaving `cost`, `reductionType`, `complexityBucket`, or `complexity` at the template's `Unclassified` / `""`.** The metadata tests fail with a message naming your class, on purpose, until you declare real values.
- **A `reductionName` that is just the class name** (`SubsetSumToPartition`) or `"Default Reduction"`. Use `"<Problem> <Approach>"` or `"<Person> ... Reduction"`.
- **Reusing a class name.** Class names are keys, ignoring case. A duplicate crashes the registry for everyone.
- **Folder and namespace do not match**, or the file sits under the TO problem instead of the FROM problem.
- **Swapping FROM and TO** in `IReduction<FROM, TO>`. The validity test may flag it, and the graph will show the arrow backwards.
- **Output that the TO problem cannot parse.** Always check your produced instance by passing it to `new TO("...")` in a test.
- **A `reduce()` that only works once**, or that adds to `gadgets` on every call without clearing it. Calling it twice should be safe.
- **Writing a controller.** Not needed. Reflection exposes your class.
- **Editing the test allowlists** to turn a failure green. Fix the cause.
