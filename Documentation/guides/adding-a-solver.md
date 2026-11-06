# Adding a solver

This guide walks you through adding a new **solver** to Redux, from an empty file to a green pull request (PR). It uses a real, small solver already in the repo as the worked example.

New to the repo? Read the [guides index](README.md) first. When you reach the "build and test" step, the details live in [building-and-testing.md](building-and-testing.md). A solver and a verifier usually travel together, so also see [adding-a-verifier.md](adding-a-verifier.md).

---

## 1. What a solver is

A **solver** finds an answer to a problem. You give it a question and it gives back a proposed answer.

Everyday analogy: a blank Sudoku grid is the question. Filling it in is the solver's job. Afterwards a **verifier** looks at the filled grid and checks whether it follows the rules (checking is much easier than solving).

Some words you will see:

- **Instance**: one specific question for a problem, written as a string. For Subset Sum, `({1,7,12,15},28)` means "can some of the numbers 1, 7, 12, 15 add up to 28?".
- **Certificate**: an answer, also written as a string. For Subset Sum, `{1,12,15}` means "use 1, 12, and 15". The solver's output is a certificate.
- **Brute force**: trying every possible answer one by one until one works. Simple, but slow.
- **Complexity**: how fast the work grows as the question gets bigger. "Exponential" means each extra input item roughly doubles the work (or worse).

In Redux:

- A solver is one C# class. It takes a problem object (already built from the instance string) and returns a certificate string.
- Most problems in Redux are **NP-complete**, which means no one knows a fast general way to solve them. So most solvers are brute force (or clever search) and get slow quickly. That is why the default instance of every problem is small, and why solvers should be written to give up when told to (section 4, step 3).
- Each problem has one **default solver** (the problem class points to it). A problem may have extra solvers too.
- You do not write a controller or register anything. Redux finds every solver class automatically (see section 3), and the API exposes it as `POST /ProblemProvider/solve`.

## 2. Before you start

1. **The problem must already exist** in `Problems/NPComplete/` (or `NPHard/` or `P/`). If it does not, add it first, see [adding-a-problem.md](adding-a-problem.md).
2. **Know the problem's certificate format.** Look at the problem class's `certificateFormat` (for Subset Sum: `{K | K is set}`, example `{1,12,15}`). Your solver's output must match it (section 4, step 4).
3. Work on a branch based on `CSharpAPI`. Pull requests go back to `CSharpAPI`.

   ```bash
   git fetch origin
   git switch -c add-solver-subsetsum origin/CSharpAPI
   ```

   Pick your own branch name, for example `add-solver-<problem>-<approach>`.
4. Make sure the project builds before you change anything. See [building-and-testing.md](building-and-testing.md).

## 3. Where the file goes

A solver file lives inside the folder of the problem it solves, in a `Solvers` folder:

```text
Problems/NPComplete/NPC_<PROBLEM>/Solvers/<ClassName>.cs
```

Rules that matter:

| Thing | Rule | Worked example |
| --- | --- | --- |
| Folder | `NPC_<PROBLEM>/Solvers/` (use `NPH_` under `NPHard/`, `P_` under `P/`) | `NPC_SUBSETSUM/Solvers/` |
| File name | Same as the class name | `SubsetSumBruteForce.cs` |
| Namespace | `API.Problems.NPComplete.NPC_<PROBLEM>.Solvers` | `API.Problems.NPComplete.NPC_SUBSETSUM.Solvers` |
| Class name | Unique across the whole project (see below) | `SubsetSumBruteForce` |
| Implements | `ISolver<PROBLEM>` | `ISolver<SUBSETSUM>` |
| `solverName` | Matches one of two name shapes (see section 5) | `"Subset Sum Brute Force"` |

The `Solvers` folder holds only solver classes, and **a solver is one file**: the algorithm and the steps it records for the visualizations are written together in `<ClassName>.cs`. A problem has no `Steps` folder (a metadata test fails if one appears). Code that several solvers share goes somewhere else:

- **A fact about the instance** (the cost of a tour, the clauses an assignment leaves false) goes on the problem class or next to it: for example `TSP.Cost` in [TSP_Class.cs](../../Problems/NPComplete/NPC_TSP/TSP_Class.cs), or [NPC_SAT3/SatFormula.cs](../../Problems/NPComplete/NPC_SAT3/SatFormula.cs).
- **A picture's ids and names** (`VariableId`, `ClauseId`, `EdgeIds`, `Assigned`) live with that picture's frames in `Interfaces/`, for example [GroupedGraphFrames.cs](../../Interfaces/graphs/GroupedGraphFrames.cs) and [GraphTourFrames.cs](../../Interfaces/graphs/GraphTourFrames.cs).
- **A recorder that a whole family of algorithms shares** goes in [Interfaces/Steps/](../../Interfaces/Steps/), for example [RestartSteps.cs](../../Interfaces/Steps/RestartSteps.cs) for local searches that restart.

**How Redux finds your solver.** At startup, `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) scans every class in the project. Any class that implements `ISolver` is registered under its **lowercased class name**. Because of that:

- Your class name must be unique. Two classes with the same name (ignoring upper/lower case) crash startup.
- You do not edit any list. Creating the class is enough.
- The list of solvers for a problem, at `GET /Navigation/Problem_SolversRefactor?chosenProblem=SUBSETSUM` ([Nav_Solvers.cs](../../AdditionalControllers/Navigation/Nav_Solvers.cs)), is built from the type argument of `ISolver<PROBLEM>`. Pick it carefully. (`Problem_SolversRefactor` is the controller name with the word "Controller" dropped.)
- Being **listed** is different from being the **default**. Every solver class for the problem shows up in that list. The default is the one marked `[DefaultFor(typeof(PROBLEM))]` ([Interfaces/DefaultFor.cs](../../Interfaces/DefaultFor.cs)), at most one per problem. Older problems on `IProblem<Solver, Verifier, Visualization>` name it in the problem class's `defaultSolver` instead; to change one of those, edit the problem class (for Subset Sum: [SUBSETSUM_Class.cs](../../Problems/NPComplete/NPC_SUBSETSUM/SUBSETSUM_Class.cs)). To add an extra solver, add no attribute and leave `defaultSolver` alone. Subset Sum has two solvers: the brute force one is the default, and [FastApproximation.cs](../../Problems/NPComplete/NPC_SUBSETSUM/Solvers/FastApproximation.cs) is an extra.

The API endpoint you get for free: `POST /ProblemProvider/solve?solver=<ClassName>`, with the instance as a JSON string in the body. Once the API is running (it normally listens on port 27000), try the worked example:

```bash
curl -X POST "http://localhost:27000/ProblemProvider/solve?solver=SubsetSumBruteForce" \
  -H "Content-Type: application/json" \
  -d '"({1,7,12,15},28)"'
```

Success looks like `"{1,12,15}"`. A solver is also used by the visualization endpoint (`POST /ProblemProvider/visualize`), which calls `solve`, or, if the solver records steps, `Solve(problem, rec)` (section 4, step 5). You can read any solver's metadata with `GET /ProblemProvider/info?interface=SubsetSumBruteForce`.

> The folder layout above matches the one in [ProblemTemplate/Templates/README.md](../../ProblemTemplate/Templates/README.md), but that file lists the interface fields only loosely. The interface itself, [Interfaces/SolverInterface.cs](../../Interfaces/SolverInterface.cs), is the source of truth.

## 4. Step by step

### Step 1. Get the template

You have two options. Both give you the same file.

- **Copy it by hand.** The template is [ProblemTemplate/Templates/Solvers/ProblemSolver.txt](../../ProblemTemplate/Templates/Solvers/ProblemSolver.txt). Copy it to your new folder, rename it `<ClassName>.cs`, and replace the placeholders `{PROBLEM}`, `{SOLVER}`, and `{SOLVER_PASCAL_CASE}`.
- **Download it from a running API.** Start the API, then call `GET /ProblemTemplate/solver?problemName=SUBSETSUM&solverName=My Subset Sum Solver`. The two query parameters are `problemName` (the problem's class name, for example `SUBSETSUM`) and `solverName` (the display name; the class name is made from it by removing spaces and odd characters). It returns a zip containing `NPC_SUBSETSUM/Solvers/MySubsetSumSolver.cs` and a README. The code for this is in [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs).

**Where each file in the zip goes.** Paths are from the repo root (the folder that contains `Redux.slnx`):

| The file in the zip | Where it goes in the repo | What it is, and what to do with it |
| --- | --- | --- |
| `NPC_SUBSETSUM/Solvers/MySubsetSumSolver.cs` | `Problems/NPComplete/NPC_SUBSETSUM/Solvers/MySubsetSumSolver.cs` | Your solver. Throws `NotImplementedException` until you write it. The zip always uses the `NPC_` prefix: for a problem under `Problems/NPHard/` or `Problems/P/`, use that problem's own folder (`NPH_` or `P_`) and fix the namespace. |
| `README.md` | Nowhere. It is not copied into the repo. | Reference only: a description of every member you fill in. Read it, then leave it in the zip. |

The tests for your solver go in the problem's test file (see "Tests you write yourself" below).

### Step 2. Fill in the members

Every solver implements the members in [Interfaces/SolverInterface.cs](../../Interfaces/SolverInterface.cs). Here is each one in plain words, with the value used by the worked example, [`SubsetSumBruteForce.cs`](../../Problems/NPComplete/NPC_SUBSETSUM/Solvers/SubsetSumBruteForce.cs).

| Member | What it is for | Worked example value |
| --- | --- | --- |
| `solverName` | The name people see in the GUI. A test checks its shape (see section 5). | `"Subset Sum Brute Force"` |
| `solverDefinition` | One sentence on how it works. | `"This is a brute force solver for Subset Sum"` |
| `source` | A citation for where the algorithm comes from. May be empty for a plain brute force. | `""` |
| `sourceFile` | The repo-relative path of this class's source file. The template sets it to `SourceFile.Path()`; leave that line as it is, because the compiler fills in the path. | `SourceFile.Path()` |
| `contributors` | Names of the people who wrote it. | `{ "Caleb Eardley", "Garret Stouffer" }` |
| `timerHasExpired` | A true/false flag that means "stop now". See step 3. | `{ get; set; }` |
| `solverType` | The style of algorithm (see below). | `SolverType.BruteForce` |
| `complexityBucket` | How the worst-case work grows (see below). | `SolverComplexityBucket.Exponential` |
| `timeComplexity` | The worst-case running time, as a function the API can evaluate. | `new("n * 2^n", ", n = \|S\|")` |
| `solve(PROBLEM)` | **The actual algorithm.** Takes a problem object, returns a certificate string. | see step 3 |
| `Solve(PROBLEM, StepRecorder<TPartial>)` | Optional. The same algorithm, also reporting its steps for step-by-step visualizations. See step 5. | not used here |

The three **declared metadata** members are the ones people forget. "Declared" means a human decides. No tool works them out for you. Their rules are in [SolverType.cs](../../Interfaces/SolverType.cs) and [SolverComplexityBucket.cs](../../Interfaces/SolverComplexityBucket.cs), and the doc comments there explain every choice. Short version:

- `solverType`: read your `solve()` and pick the closest style: `BruteForce` (tries everything, no pruning), `Backtracking` (searches but gives up on dead ends early), `DynamicProgramming`, `Greedy`, `Approximation`, `Heuristic`, and so on. `Quantum` means the solver calls the external quantum-simulator service (see the note at the end of this section). If several fit, declare each, joined with `|`: First-Fit Decreasing is `SolverType.Approximation | SolverType.Greedy` (a proven ratio, reached by a greedy pass). There is no main one, and the order you write them in doesn't matter: the API lists them in the enum's order as `solverTypes`. The old single `solverType` field holds the first of that list for older clients and is deprecated.
- `complexityBucket`: the **worst case** growth: `Polynomial`, `Exponential`, or `Factorial`. It describes the worst case even if your solver is fast on typical inputs.
- `timeComplexity`: the worst-case running time, written without the `O( )`, such as `new("n * 2^n", ", n = |S|")`. See [MathematicalFunction.cs](../../Interfaces/MathematicalFunction.cs) for the notation. Only write one you are confident about. Never guess.

**Do not leave any of the three as `Unclassified` (or `null`).** Tests fail if you do (section 5). The template declares them as `Unclassified` and `null` with a `TODO` comment, so you replace each value. The template's `solve` also ends with a `NotImplementedException`: an unfinished solver fails loudly instead of claiming "no solution". Replace it with your real return.

You also need a **public constructor with no arguments** (`public SubsetSumBruteForce() { }`). Redux builds your class this way, and so do the tests.

> **About quantum solvers.** Some solvers (`ShorsQuantumSolver`, `SATGroverSolver`, and a few others) do not solve anything themselves. They send the problem to a separate quantum-simulator service whose address is the `BaseURL` setting in [QuantumSolverSettings.cs](../../QuantumSolverSettings.cs) (default `http://localhost:27100`). Those are special cases with their own allowlist in the tests. If you add one, send its request through `QuantumServerAPI.Ask` and throw when the response has no answer, and declare `solverType = SolverType.Quantum`, so a failing service becomes HTTP 503 `solver_unavailable` (whose `hint` and `options` point at the problem's other solvers) and never an answer (see [QUANTUM_API_README.md](../../Tools/QUANTUM_API_README.md)). If you are not adding one, you can ignore this.

### Step 3. Write `solve()`

`solve()` receives a problem object, so the instance text is already parsed for you. In the worked example, a Subset Sum object has a list `S` of numbers and a target `T`. The idea is:

1. Try every non-empty subset of `S`. With `n` numbers there are `2^n` subsets. A loop counter `mask` from `1` to `2^n - 1` stands for each subset: bit number `i` of `mask` being 1 means "include `S[i]`".
2. Turn the subset into a certificate string such as `{1,12,15}`.
3. Ask the verifier whether this certificate works. If yes, return it.
4. If nothing works, return `"{}"`.

The loop at the heart of the worked example:

```csharp
for (int mask = 1; mask < (1 << n); mask++) {
    if (timerHasExpired) return SolveRun.TimedOutAnswer;

    string certificate = SubsetToCertificate(mask, subsetSum.S);
    if (subsetSum.defaultVerifier.verify(subsetSum, certificate)) {
        return certificate;
    }
}
return "{}";
```

Notice two good habits here:

- **It reuses the problem's verifier** (`subsetSum.defaultVerifier.verify(...)`) instead of re-writing the rule. Then the solver and verifier cannot disagree. This is the standard pattern for brute-force solvers.
- **It checks `timerHasExpired` inside the loop.** See below.

**Why small instances matter.** Brute force doubles its work with every extra number. The default instance `({1,7,12,15},28)` has 4 numbers, so 16 subsets: instant. With 40 numbers it would be about a trillion subsets: not instant. Keep the problem's `defaultInstance` small enough to solve in well under a second, and use small instances in your tests.

**Timeouts: `timerHasExpired`.** Every compute request has a time limit (`ComputeLimits:TimeLimitSeconds` in `appsettings.json`, 10 seconds by default; #935). The interface has a flag, `timerHasExpired`, and two helper methods that flip it (`TimerExpired()` sets it to `true`, `ResetTimer()` sets it back to `false`). The API starts a timer for every solve and calls `TimerExpired()` when the limit runs out. A solver must check the flag inside its main loops and stop when it is `true`. Rules:

- Check it **inside your main loop** (and inside any recursion), not just once at the start. A check that never runs again cannot stop anything. If you build a big list of candidates up front, check while building it too.
- Check it often enough to matter, but not so often that the checks slow the real work down. Reading the flag is cheap, so once per candidate or per recursive call is fine.
- When it is `true`, return `SolveRun.TimedOutAnswer`. Never return a made-up answer, and not `"{}"`: that means "there is no solution", which the solver does not know. The API answers such a request with HTTP 422 and the error `time_limit`, whatever the solver returned.
- [SolverTimeLimit_Tests.cs](../../redux-tests/Steps/SolverTimeLimit_Tests.cs) checks every solver: with the flag already set, `solve` on the problem's default instance must return `SolveRun.TimedOutAnswer`. A solver that never checks the flag fails it.
- A typed solver (`ISolver<T, TPartial>`, which reports steps to a `StepRecorder`) is stopped by its recorder instead, at the next step it reports, so it must report steps inside its loops.
- This is not true cancellation: a solver that never reaches a check runs on. Running each job in its own process (#936) and on its own threads (#937) will stop those too.

**What to return.** The interface does not define a special "no solution" or "timed out" value. The solver's result is just a string, and today Redux uses these conventions:

| Situation | Common return value | Notes |
| --- | --- | --- |
| A solution was found | the certificate, in the problem's `certificateFormat` | for example `{1,12,15}` |
| There is no solution | `"{}"` (an empty certificate) | the worked example. About 60 solvers do this. Some return `""` or other text, so the convention is not universal. |
| The timer expired | `SolveRun.TimedOutAnswer` | the same for every solver (#935). Callers check that the run timed out, not this text. |

Whatever you pick, document it in a comment, and make sure the **verifier handles it** (an empty `{}` certificate must not crash it, it should just return `false` or, if your problem's format rejects it, throw `CertificateParseException`). Prefer `"{}"` or an empty certificate in the problem's own format for "no solution", because the GUI and verifier already understand that shape.

**Do not throw for "no solution".** Throwing is for real errors. The API turns bad *instance text* into a clean 400 for you, because the problem's constructor throws `ProblemParseException` before `solve()` runs. A crash inside `solve()` itself becomes a 500 error.

### Step 4. Match the certificate format

The string your solver returns **is** a certificate. It should look exactly like what the problem's `certificateFormat` describes, because three other things read it:

1. **The verifier** (a solved certificate should verify as `true`).
2. **The GUI**, which shows it and may draw it.
3. **Reductions** that map solutions (`mapSolutions`) from one problem to another.

For Subset Sum the format is `{K | K is set}` with example `{1,12,15}`, and the solver builds exactly that shape. Check yours by feeding it to the verifier in a test (see section 5). Is a solver's answer required to pass the verifier? In practice, yes whenever the solver returns a real answer. There is no single automatic test that checks this for every solver, so **your own tests must** (for the empty/no-solution value you return, make sure you test what the verifier does with it).

### Step 5 (optional). Record steps for the visualizations

A step-by-step visualization shows the solver at work, one picture per **step**. Each step has three parts:

- the **partial answer** at that moment (the assignment so far, the tour so far);
- the **focus**: the pieces of the picture the step is about (a variable, a clause, an edge);
- the **why**: one sentence saying what happened and why.

Each step starts with a **keyword** that says what kind of step it is:

| Keyword | It means | Typical step |
| --- | --- | --- |
| **Try** | look at something that may still fail | try an assignment, start a tour from a city |
| **Update** | change the working state; there is nothing to judge yet | fill a table cell, read a symbol, apply a gate, set the starting distances |
| **Accept** | keep it: a move made, the best so far, a piece of the answer | settle a node, push flow along a path, take a cell into the trace-back |
| **Reject** | it fails | a clause with no true literal, a dead end, a move the machine does not have |
| **Backtrack** | undo a move and go back | back out of a dead end in a search |
| **Done** | the end, with the answer and whether there is one | the final verdict |

Update is only in `RunSteps` (a search's steps are tries and verdicts). It is not an event of its own: it records a Try, so the pictures, the API's JSON and both front ends see a Try. The keyword is there so the solver's code says what it means.

**Which keyword?** Ask whether the step has a verdict. If it can still fail, it is a Try. If nothing is being judged, it is an Update. A verdict is an Accept or a Reject, and the very last step is always Done. One example per kind of algorithm:

| Kind of algorithm | Its steps | Example |
| --- | --- | --- |
| brute force | a Reject per failed candidate, an Accept for the answer | [SATBruteForceSolver.cs](../../Problems/NPComplete/NPC_SAT/Solvers/SATBruteForceSolver.cs) |
| greedy | a Try to start, an Accept per move, a Reject for a dead end | [TSPGreedy.cs](../../Problems/NPComplete/NPC_TSP/Solvers/TSPGreedy.cs) |
| backtracking search | Try, Reject and Accept per choice, Backtrack to go back | [Sat3BacktrackingSolver.cs](../../Problems/NPComplete/NPC_SAT3/Solvers/Sat3BacktrackingSolver.cs) |
| dynamic programming | an Update per cell filled, an Accept per cell of the trace-back | [EditDistanceDPSolver.cs](../../Problems/NPComplete/NPC_EDITDISTANCE/Solvers/EditDistanceDPSolver.cs) |
| automaton simulation | an Update per symbol read, a Reject when there is no move | [DFASolver.cs](../../Problems/P/P_DFA/Solvers/DFASolver.cs) |
| quantum circuit | an Update per column of gates, an Accept or Reject for the measurement | [UnstructuredGroverSolver.cs](../../Problems/NPComplete/NPC_UNSTRUCTUREDSEARCH/Solvers/UnstructuredGroverSolver.cs) |
| shortest paths (relaxation) | an Update to set the starting distances, an Accept per node settled (its relaxations in the why) | [SSSPSolver.cs](../../Problems/P/P_SSSP/Solvers/SSSPSolver.cs) |
| network flow | an Update for the empty flow, an Accept per augmenting path | [MinSTCutSolver.cs](../../Problems/P/P_MINSTCUT/Solvers/MinSTCutSolver.cs) |

The same word can mean two things. A DFA *accepts* a string, but its run never records an Accept step: a run keeps nothing, it only moves. Whether the machine accepts is the Done's verdict (`ok`).

The idea is simple. You already know the algorithm, so you add one line wherever it does something worth showing. That line says the keyword and why, and you get the pictures for free. A visualization draws your steps only if they have the shape of answer it draws. If the shapes don't match, nothing breaks: the picture shows the start and the answer instead.

#### What to change

1. Implement `ISolver<PROBLEM, TPartial>` in place of `ISolver<PROBLEM>`. `TPartial` is the shape of the answer a visualization draws, such as `Assignment` for SAT or `Tour` for TSP. The shapes are in [Interfaces/Steps/Shapes.cs](../../Interfaces/Steps/Shapes.cs).
2. Write the algorithm as `Solve(PROBLEM problem, StepRecorder<TPartial> rec)`. Make the plain `solve` call it with recording off: `public string solve(TSP tsp) => Solve(tsp, StepRecorder<Tour>.Off);`. With recording off, the steps cost nothing: no picture is built and no text is formatted.
3. Pick the recorder that fits the algorithm:

| The algorithm... | Use | Example |
| --- | --- | --- |
| does not branch: it tries candidates one after another, or builds one answer piece by piece | `RunSteps` ([RunSteps.cs](../../Interfaces/Steps/RunSteps.cs)) | [SATBruteForceSolver.cs](../../Problems/NPComplete/NPC_SAT/Solvers/SATBruteForceSolver.cs), [TSPGreedy.cs](../../Problems/NPComplete/NPC_TSP/Solvers/TSPGreedy.cs) |
| branches and backtracks, so its steps form a search tree | `SearchSteps` ([SearchSteps.cs](../../Interfaces/Steps/SearchSteps.cs)) | [Sat3BacktrackingSolver.cs](../../Problems/NPComplete/NPC_SAT3/Solvers/Sat3BacktrackingSolver.cs) |
| is a local search that restarts from a random start | `RestartSteps` ([RestartSteps.cs](../../Interfaces/Steps/RestartSteps.cs)) | [WalkSAT.cs](../../Problems/NPComplete/NPC_SAT3/Solvers/WalkSAT.cs) |

   **A search that keeps its own tree.** `SearchSteps` builds the search tree for you, from its `Root`, `Expand`, `Try`, `Accept` and `Reject` calls, with the node states those give. A few searches already keep a tree of their own, with node states `SearchSteps` does not give: a "start" root, squares or colors skipped as pruned nodes with a note, a choice marked failed when it is taken back. [GraphColoringBacktracking.cs](../../Problems/NPComplete/NPC_GRAPHCOLORING/Solvers/GraphColoringBacktracking.cs), [ThreeDimensionalMatchingBacktracking.cs](../../Problems/NPComplete/NPC_DM3/Solvers/ThreeDimensionalMatchingBacktracking.cs) and [NQueensBacktracking.cs](../../Problems/P/P_NQUEENS/Solvers/NQueensBacktracking.cs) do. They use `RunSteps` with its `tree:` option: a converter in `NewSteps` that turns a step's state into the search-tree nodes the step adds, and `Done(..., tree: () => ...)`, which names the answer's path, root first, so the tree ends on it.

   ```csharp
   // The Search Tree picture's node only: "b=1", with the clashing neighbour's color as the note of a pruned one.
   tree: m => WithRoot(Node(m.Id, m.Parent, m.Node, m.Color, m.Tree, m.Clash < 0 ? null : $"{_nodes[m.Clash]} has {m.Color}")));
   ...
   steps.Done(new Groups(coloring), true, $"Every node is colored ...", tree: () => search.AnswerPath());
   ```

   Use `tree:` only when your search already keeps such a tree. A new search that branches should use `SearchSteps`, and a backtracking search that records no tree stays on plain `RunSteps`.

4. Say **once**, at the top of the class, how a step shows the solver's state. This is the **set-once block**, a `NewSteps` method. It has two group comments, so a reader can see what is shared and what is per picture.
5. In the algorithm, write **one line per step**: the keyword, then the state, then (for a search) the choice, then the why, then any extras. For example, `steps.Reject(state, $"why")` or `steps.Try(state, value, $"why")`.

#### Worked example: SAT brute force

[SATBruteForceSolver.cs](../../Problems/NPComplete/NPC_SAT/Solvers/SATBruteForceSolver.cs) tries every assignment in turn. It does not branch, so it uses `RunSteps`. Its state is the candidate assignment, together with the first clause the candidate leaves false (`-1` means none). The set-once block says how that state shows:

```csharp
// How the steps show a candidate: the assignment, and the clause it leaves with no true literal (-1: none).
private static RunSteps<(Dictionary<string, bool> Assignment, int FalseClause), Assignment> NewSteps(StepRecorder<Assignment> rec) => new(rec,
    // What every picture shows: the candidate assignment, and the clause that fails it.
    partial: s => ToAssignment(s.Assignment),
    focus: s => s.FalseClause < 0 ? [] : [ClauseId(s.FalseClause)]);
```

The algorithm then gets one line per step. The why is written right there, in full:

```csharp
int failed = FirstFalseClause(formula, assignment);
if (failed < 0) {
    steps.Accept((assignment, -1), $"Try {Assigned(assignment)}: every clause has a true literal.");
    steps.Done(ToAssignment(assignment), true, $"The formula is satisfied by {Assigned(assignment)}.");
    return ...;
}
steps.Reject((assignment, failed),
    $"Try {Assigned(assignment)}: clause {FailedClause(failed, formula.Clauses[failed].Literals.Select(l => l.ToString()))}.");
```

The facts the why needs come from the algorithm itself. The search already worked out `failed` to decide what to do, so the step reuses that number instead of looking it up again. `FailedClause` is a shared wording helper ("C2 (x1 | !x2) has no true literal"; see rule 3).

A step that is about something other than the usual state can say so for that one step. `TSPGreedy`'s dead end is about a single city:

```csharp
steps.Reject(route, $"Dead end at {currentNode}: ...", focus: () => [currentNode]);
```

**A solver that branches.** Read [Sat3BacktrackingSolver.cs](../../Problems/NPComplete/NPC_SAT3/Solvers/Sat3BacktrackingSolver.cs). Its `SearchSteps` block adds two things:

- converters for a choice made from a state (`childPartial`, `rejectFocus`);
- a second group, "The Search Tree picture's node text only." (`childLabel`, `rejectNote`, `solvedNote`).

Its step lines name the choice too, for example `steps.Try(curSat, value, $"Try {curSat.nextVar} = {value}...")`. It also uses `steps.Root(...)` for the start and `steps.Expand(...)` each time it takes a state off its queue, which records a Backtrack when the search jumps back.

**Always write the text as `$"..."`, even when it has no `{holes}`.** The text parameters are special string builders. They build the text only when the step will be kept, so a run with recording off formats nothing. A plain `"..."` string does not compile, on purpose. Otherwise text built in a variable, or joined with `+`, would be built on every step, even when nobody is watching.

#### The rules

1. **Picture helpers shared per picture type are fine.** They live in `Interfaces/`, next to that picture's frames. Examples: `VariableId`, `ClauseId`, `EdgeId`, `Assigned`, `Num`, `Words.Count`.
2. **No shared step helpers for one problem.** A problem has no `Steps` folder. An instance fact goes on the problem class. A recorder shared by a family of algorithms goes in `Interfaces/Steps`.
3. **No private why helpers in the solver.** Write each step's why inline, in the step line. A reader should see what a step says right where it happens. One kind of helper is allowed: a **shared wording helper** in `Interfaces/`, next to the picture helpers it belongs with, when the same wording repeats across solvers or would need a branch (has or have, one or many) inside the why line. The SAT solvers share `FailedClause`, `NoTrueLiteral`, `ClauseCheck`, `FreeVariables` and `FreeSetToFalse` in [GroupedGraphFrames.cs](../../Interfaces/graphs/GroupedGraphFrames.cs). A wording helper gives a piece of a sentence, so the step line still reads as one: `$"{v} = {value} fails: clause {FailedClause(c, clauses[c])} left."`. Call it only inside a `$"..."` hole, never before the step line, so a run with recording off never calls it. A sentence only one solver says stays inline.
   - **Wording built by a loop over the picture's pieces.** Some wording has to walk the picture's pieces to say anything: which elements a candidate uses too often, which sets it leaves uncovered. For 3-Dimensional Matching Brute Force's Reject, `TripartiteFrames.WhyRejected` ([TripartiteFrames.cs](../../Interfaces/Tripartite/TripartiteFrames.cs)) counts how often each element is used and says "a is used 3 times, and b and c twice". That may live as a shared helper next to the picture's other helpers even when only one solver uses it, because it explains the picture's pieces, not one solver's reasoning. Call it inside a hole as usual: `$"Try {...}: {TripartiteFrames.WhyRejected(spec!, combination)}."`. [BipartiteFrames.WhyRejected](../../Interfaces/graphs/BipartiteFrames.cs) does the same for the set problems (Exact Cover, Hitting Set, Set Cover).
   - **The exception: solvers that restart.** WalkSAT, Schöning and PPZ make random tries, and they cannot know which try will succeed until the run is over. So they do not record steps as they go. They report each try and its moves to `RestartSteps`, which records the steps after the run: the first try and the successful one in full, and the tries in between as one step. The why text for those steps is written once, in the `RestartSteps` converters of `NewSteps` (`startWhy`, `moveWhy`, `closeWhy`, and so on), not at each call, because the moves are replayed there, after the run, and not where they happen. The same rules apply inside those converters: write the text there in full, and use only shared wording helpers. A run that ends before any try (a formula with no variables) records its Done at the call as usual, `steps.Done(new Assignment([]), false, $"The formula has no variables to set: no answer found.")`.
4. **The facts a why needs come from the algorithm where possible.** Reuse what the algorithm already worked out, such as the clause that failed or the path's cost. Look a fact up only when the algorithm never has it, and then do it inside the step line, so it runs only when the step is kept.
5. **The solver's own algorithm helpers stay.** Examples: `FirstFalseClause`, or the next-permutation code. Those are the algorithm, not the steps.
6. **Plumbing is set once.** How a state becomes the partial answer and the focus is said once, in `NewSteps`. Don't pass a lambda for it on every step.
7. **Comment thoroughly, in plain words.** Put a header comment above the class that lists which steps the solver records and what each one means. Keep the two group comments in the set-once block. Write comments on the algorithm that a student can follow. Leave out issue numbers and change history; those belong in the PR.
8. **One sentence per step line: no branching inside a why.** `dotnet format` joins any line break inside a `{ }` hole, so a why with two or more `?:` (or `switch`) choices inside it ends up as one very long line nobody can read. When the sentence changes by case, write one step line per case instead. One short `?:` that picks a word (`is` or `are`) is fine. Before (Edit Distance's mismatched cell):

   ```csharp
   steps.Update(cell, $"... Insert '{y[j - 1]}': 1 + {insert}. The cheapest is {(replace == cheapest ? "replace" : delete == cheapest ? "delete" : "insert")}, so {dp[i, j]}.");
   ```

   After:

   ```csharp
   if (replace == cheapest)
       steps.Update(cell, $"... Insert '{y[j - 1]}': 1 + {insert}. The cheapest is replace, so {dp[i, j]}.");
   else if (delete == cheapest)
       steps.Update(cell, $"... Insert '{y[j - 1]}': 1 + {insert}. The cheapest is delete, so {dp[i, j]}.");
   else
       steps.Update(cell, $"... Insert '{y[j - 1]}': 1 + {insert}. The cheapest is insert, so {dp[i, j]}.");
   ```

   The lines repeat, but each one reads as the sentence the step will show. [UnstructuredGroverSolver.cs](../../Problems/NPComplete/NPC_UNSTRUCTUREDSEARCH/Solvers/UnstructuredGroverSolver.cs) does the same with one `Done` per outcome.
9. **A fact only the picture needs, and that costs work, is worked out only when recording.** Check `rec.Enabled` before computing it, and use an empty value otherwise. Edmonds-Karp ([MinSTCutSolver.cs](../../Problems/P/P_MINSTCUT/Solvers/MinSTCutSolver.cs)) works on the residual graph, but its picture and why need what each push did to the instance's edges (where it ran backward, which edges it filled). The algorithm never needs that, so it is read off the residual graph only while recording:

   ```csharp
   // What the push did on the instance's edges, for the step (worked out only while recording).
   FlowPush push = rec.Enabled ? flow.Value.Push(residual, parent, s, t, bottleneck) : FlowPush.None;
   ```

   A plain solve gets `FlowPush.None` and does no extra work. Facts the algorithm already has (the bottleneck, the flow's value) need no guard: keep them as plain values and let the `$"..."` holes format them only for a kept step.
10. **A caption is a sentence, and never opens with a label of its own.** The step bar's "Step N of M" is the only label. A counter of your own ("Try 2:", "Flip 3:") would not match it, because a capped or summarized run skips steps, and a position label ("Hour 0:", "Row 2:", "Cell (1,1):") puts a second label right after the bar's and can disagree with it (steps count from 1, hours from 0). Say what the step does instead. A count or a position that is real information stays inside the sentence ("found by try 37", "In pass 2, ...", "At hour 0, ..."). A caption led by a verb that names a thing ("Put 3 in bin 1: ...", "Take 2: ...", "Go 1 → 3: ...") is already a sentence. Before (N-Queens Backtracking): `$"Row {row}: every column is attacked, so back up and move the queen in row {row - 1}."`, which reads "Row 2: every column is attacked, ...". After: `$"In row {row}, every column is attacked, so back up and move the queen in row {row - 1}."`, which reads "In row 2, every column is attacked, ...". [CaptionCounter_Tests.cs](../../redux-tests/Steps/CaptionCounter_Tests.cs) checks every recording solver for both.

To draw your steps you also need a visualization that draws your answer's shape. Most shapes already have one. See [adding-a-visualization.md](adding-a-visualization.md) for the other half.

## 5. Write tests

### Tests that already check your solver for free

You do not have to register your solver with these tests. They find it automatically by scanning every solver class, and each one runs once per solver.

**These tests will check your solver automatically:**

| Test file | What it checks about your solver |
| --- | --- |
| [NamingConvention_Tests.cs](../../redux-tests/Metadata/NamingConvention_Tests.cs) (`SolverName_MatchesNamingConvention`) | Your class builds with no arguments, and `solverName` matches one of two shapes: `"<Problem> <Approach>"` (title-case words, 2 or more, for example `"Subset Sum Brute Force"`) or `"<Person's name> ... Algorithm"` (for example `"Dijkstra's Algorithm"`). A raw class name like `SubsetSumBruteForce`, or a placeholder like `"Default Solver"`, fails. |
| [SolverFolder_Tests.cs](../../redux-tests/Metadata/SolverFolder_Tests.cs) (`EveryTypeInASolversFolder_IsASolver`, `NoProblemHasAStepsFolder`) | Every type declared in a file of a `Solvers` folder is a solver (implements `ISolver`), and no problem has a `Steps` folder. A helper class there fails: move it out (see section 3). |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`NoNewUndeclaredSolverType`) | `solverType` is not `Unclassified`. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`NoNewUndeclaredComplexityBucket`) | `complexityBucket` is not `Unclassified`. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`NoNewUndeclaredComplexity`) | `timeComplexity` is declared, not `null`. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`Info_...` and `AllInfo_...` tests) | The three declared values appear in the API's JSON as words (`"BruteForce"`), not numbers. |
| [ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs) (`Solve_GarbageInstance_IsNever500`) | Sending garbage (`""`, `{{{`, `not an instance`, `{a,b}:::`) as the instance to `POST /ProblemProvider/solve` never produces an HTTP 500. A 400 with the instance format is the expected answer. Because your problem's constructor does the parsing, this normally passes without extra work. |

Checked for free: that your solver stops when `timerHasExpired` is set, on the default instance ([SolverTimeLimit_Tests.cs](../../redux-tests/Steps/SolverTimeLimit_Tests.cs)).

Not checked for free: that your solver gives the **right** answer, and that its answer passes the verifier. There is also no generic performance test over solvers (the Performance tests cover the API's endpoints and reductions), so a solver that is too slow on the default instance will only show up as a slow test of your own. Those are your job.

> The three "Unclassified" tests work with an **allowlist** inside `SolverType_Tests.cs`. Do **not** add your class to it. The allowlists are meant to stay as short as possible and are only for special cases such as quantum solvers. Fix the declarations instead.

### Tests you write yourself

> **The build check won't tell you if you skipped this.** CI checks that every test passes, not that you wrote any. Your own tests are the only thing that proves your code gives the right answers, so reviewers will look for the ones in this guide's checklist. See [building-and-testing.md](building-and-testing.md#4-how-to-read-the-rbs-report).

Solver tests live in the test project, one folder per problem:

```text
redux-tests/Problems/NPC_<PROBLEM>/<PROBLEM>_Tests.cs
```

If you generated a whole problem with `GET /ProblemTemplate` ([adding-a-problem.md](adding-a-problem.md#where-each-file-in-the-zip-goes)), the generated test file already has the generic solver tests and stubs for the "no solution" and timer tests. Fill in the stubs. Copy-paste examples are in [building-and-testing.md, section 5](building-and-testing.md#5-required-tests-for-each-kind-of-change).

The worked example's tests are in [redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). If a test file for your problem exists, add to it. If not, create one. The pattern, from the worked example:

```csharp
[Theory]
[InlineData("({1,7,12,15},28)")]
[InlineData("({3,5,9},14)")] // 5 + 9
[InlineData("({2,4,6},10)")] // 4 + 6
public void SUBSETSUM_Solver_ProducesVerifiableCertificate(string instance) {
    SUBSETSUM problem = new SUBSETSUM(instance);
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    string solution = solver.solve(problem);
    Assert.True(verifier.verify(problem, solution));
}

[Fact]
public void SUBSETSUM_Solver_Returns_Empty_When_No_Solution() {
    SUBSETSUM problem = new SUBSETSUM("({2,4,6},5)");
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    Assert.Equal("{}", solver.solve(problem));
}
```

Things to copy from the existing tests:

- `using Xunit;` and `using` lines for your problem's namespace and its `Solvers` and `Verifiers` namespaces.
- `namespace redux_tests;` and a `public class <PROBLEM>_Tests`.
- `[Theory]` with `[InlineData(...)]` to run one test body on many inputs, and `[Fact]` for a single check. Test names read like sentences: `<PROBLEM>_Solver_<What>`.
- Use **small hand-picked instances** whose answers you worked out on paper.

**Edge-case checklist.** Aim to cover each of these:

- [ ] The problem's default instance: the solver returns a real answer and the verifier accepts it.
- [ ] One or two other small instances that have a solution, and the verifier accepts each answer.
- [ ] An instance with **no solution**: the solver returns your documented "no solution" value (`"{}"` in the worked example).
- [ ] A tricky small case: the answer is the first item only, or the last item only (the worked example has a regression test for exactly this, `SUBSETSUM_Solver_Finds_FirstElement_Only_Solution`).
- [ ] The smallest allowed input (one item, or empty if the problem allows it).
- [ ] The timer: set `solver.timerHasExpired = true;` before calling `solve`, and check that it returns `SolveRun.TimedOutAnswer` instead of searching. (`SolverTimeLimit_Tests` already does this for every solver on the default instance; add a case of your own if the default instance does not reach your main loop.)

For the timer case:

```csharp
SubsetSumBruteForce solver = new SubsetSumBruteForce();
solver.timerHasExpired = true;
Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(new SUBSETSUM("({1,7,12,15},28)")));
```

If you wrote an **approximation or heuristic** solver, it may legitimately return a wrong or empty answer. The worked example's second solver tests that rule: its answer is either `"{}"` or passes the verifier (`FastApproximation_NonEmptyResult_AlwaysPassesVerifier`). Copy that pattern.

### Steps: the capture tool and step snapshots

This part is only for a solver that records steps (section 4, step 5).

**The capture tool.** [StepCapture_Tests.cs](../../redux-tests/Capture/StepCapture_Tests.cs) runs every solver that records steps on many instances: the default, the snapshot instances, and seeded random ones. It runs each one with recording off, with recording on at several caps, under fake time limits, and through every visualization that draws its steps. It writes everything each run gave (answers, steps and frames) to one file per solver. Use it when you change **how** steps are recorded but mean to keep **what** they show, for example when you move a solver to the set-once pattern. Run it once before your change and once after, into two folders, then compare them:

```bash
git stash                     # back to the code before your change
REDUX_CAPTURE_DIR=/c/temp/capture/before dotnet test redux-tests --filter Category=Capture
git stash pop                 # your change again
REDUX_CAPTURE_DIR=/c/temp/capture/after dotnet test redux-tests --filter Category=Capture
diff -r /c/temp/capture/before /c/temp/capture/after
```

In PowerShell, set the folder first with `$env:REDUX_CAPTURE_DIR = "C:\temp\capture\before"`. No output from `diff` means every answer, step and frame is the same. The tool takes about a minute and a half and writes about 150 MB. It is skipped when `REDUX_CAPTURE_DIR` is not set, so the normal test run and CI never run it. `_summary.txt` in each folder lists what was captured and what could not be.

**When a step snapshot test changes.** Some tests compare a solver's steps with saved files in `redux-tests/Endpoints/Snapshots/`:

- the answer files (such as `sat_solver_answers.tsv`) hold what each solver returned before it recorded steps. Recording steps must never change an answer, so these should never change because of steps.
- the step files (such as `searchtree_sat3_steps.tsv`) hold a short hash of every step. They change when any step's words, focus or partial answer change, and the failure shows the step's new JSON.
- the frame files (such as `formula.sat.frames.json`) hold whole pictures.

The step tests in `redux-tests/Steps/` (such as `SatSolvers_Tests.cs`) also check the words and focus of chosen steps.

These should change only when you **mean** to change what a step shows: new wording, a different focus, a new step. They should not change for a clean-up. When one fails:

1. Read the failure. It names the solver, the instance and the step, and shows what the step is now.
2. Check that every changed step is one you meant to change, and that no answer changed.
3. Update the snapshot in the same commit as the change. Some snapshot tests rewrite their file when you run them with `REDUX_UPDATE_SNAPSHOTS=1` (the test file's header comment says so). For the others, copy the new value from the failure.
4. Say in the PR description which steps changed and why, so the reviewer can check the new pictures.

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
dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~SUBSETSUM_Solver"
```

(Replace the text after `~` with part of your test name.) Success looks like `Passed!` with `Failed: 0`.

## 7. Checklist before opening the PR

- [ ] The problem already exists, and you know its `certificateFormat`.
- [ ] The file is at `Problems/NPComplete/NPC_<PROBLEM>/Solvers/<ClassName>.cs`, and the namespace matches the folder.
- [ ] The class name is unique in the whole project.
- [ ] The class implements `ISolver<PROBLEM>` and has a public constructor with no arguments.
- [ ] `solverName` follows `"<Problem> <Approach>"` or `"<Person> ... Algorithm"`; `solverDefinition`, `source`, and `contributors` are filled in (no `"TODO"` left).
- [ ] `solverType`, `complexityBucket`, and `timeComplexity` are declared (not `Unclassified`, not `null`), and you are confident they are true.
- [ ] `solve()` checks `timerHasExpired` inside its main loop.
- [ ] The returned certificate matches the problem's `certificateFormat`, and a returned answer passes the problem's verifier.
- [ ] The "no solution" and "timed out" results are documented in comments and tested.
- [ ] If this is the problem's default solver, it is marked `[DefaultFor(typeof(PROBLEM))]` (or, for an older `IProblem<...>` problem, the problem class's `defaultSolver` points to it), and the problem's default instance solves quickly.
- [ ] You added tests for solved, no-solution, and timer cases.
- [ ] If it records steps: it is one file with a header comment listing its steps, a `NewSteps` block with its two group comments, and every why written inline as `$"..."` (the 7 rules in step 5).
- [ ] If you changed how existing steps are recorded without meaning to change them, the capture tool shows no difference. If a step snapshot changed, the PR says which steps changed and why.
- [ ] `dotnet format Redux.slnx` was run, and build and tests pass.
- [ ] You read the rbs report on your PR (see [building-and-testing.md](building-and-testing.md)).
- [ ] The PR targets `CSharpAPI`.

## 8. Common mistakes

- **Leaving `solverType`, `complexityBucket`, or `timeComplexity` at the template's `Unclassified` / `null`.** Three metadata tests fail with a message naming your class, on purpose, until you declare real values.
- **A `solverName` that is just the class name** (`SubsetSumBruteForce`) or a placeholder. Use `"<Problem> <Approach>"` or `"<Person> ... Algorithm"`.
- **Never checking `timerHasExpired`**, or checking it only once before the loop. Check it inside the loop.
- **Output that does not match `certificateFormat`.** The verifier will throw `CertificateParseException`, or the GUI will not understand it. Always test your solver's output against the verifier.
- **Forgetting the template's placeholder.** The template ends with `throw new NotImplementedException(...)`. Replace it with the real algorithm.
- **A default instance that takes forever.** Brute force grows exponentially. Keep the problem's `defaultInstance` small.
- **Guessing the running time.** A wrong `timeComplexity` is a public claim. Leave out claims you are not sure about, and ask in the PR.
- **Throwing for "no solution"** or for a timeout. Return a documented value instead.
- **Re-writing the verifier rule inside the solver.** Reuse `problem.defaultVerifier` so the two cannot disagree.
- **Reusing a class name.** Class names are keys, ignoring case. A duplicate crashes the registry for everyone.
- **Folder and namespace do not match**, or the file sits under the wrong problem.
- **Writing a controller.** Not needed. Reflection exposes your class.
- **Editing test allowlists** to turn a failure green. Fix the cause.
- **Splitting a solver's steps into another file** (`<ClassName>.Steps.cs`, or a problem's `Steps` folder). Write them in the solver.
- **A private helper that writes a step's sentence** (`RejectCaption(...)`). Write the why inline in the step line, even when it is long.
- **Passing `partial:` or `focus:` lambdas on every step.** Say them once in `NewSteps`; pass one on a step only when that step is about something else.
- **Building the text before the step** (`string why = ...; steps.Reject(s, $"{why}")`). That runs even when nobody is watching. Put the text in the step's own `$"..."`.
- **Updating a snapshot just to make it green.** Read what changed first (section 5).
