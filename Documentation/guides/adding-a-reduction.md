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
| `gadgets` | Pieces the GUI draws to show how parts of A become parts of B. Optional: the interface supplies an empty list if you do nothing. | not used here |

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
