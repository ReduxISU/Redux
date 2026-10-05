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

**How Redux finds your solver.** At startup, `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) scans every class in the project. Any class that implements `ISolver` is registered under its **lowercased class name**. Because of that:

- Your class name must be unique. Two classes with the same name (ignoring upper/lower case) crash startup.
- You do not edit any list. Creating the class is enough.
- The list of solvers for a problem, at `GET /Navigation/Problem_SolversRefactor?chosenProblem=SUBSETSUM` ([Nav_Solvers.cs](../../AdditionalControllers/Navigation/Nav_Solvers.cs)), is built from the type argument of `ISolver<PROBLEM>`. Pick it carefully. (`Problem_SolversRefactor` is the controller name with the word "Controller" dropped.)
- Being **listed** is different from being the **default**. Every solver class for the problem shows up in that list. The one the problem class names in its `defaultSolver` property is the default. To make your solver the default, edit the problem class (for Subset Sum: [SUBSETSUM_Class.cs](../../Problems/NPComplete/NPC_SUBSETSUM/SUBSETSUM_Class.cs)). To add an extra solver, leave `defaultSolver` alone. Subset Sum has two solvers: the brute force one is the default, and [FastApproximation.cs](../../Problems/NPComplete/NPC_SUBSETSUM/Solvers/FastApproximation.cs) is an extra.

The API endpoint you get for free: `POST /ProblemProvider/solve?solver=<ClassName>`, with the instance as a JSON string in the body. Once the API is running (it normally listens on port 27000), try the worked example:

```bash
curl -X POST "http://localhost:27000/ProblemProvider/solve?solver=SubsetSumBruteForce" \
  -H "Content-Type: application/json" \
  -d '"({1,7,12,15},28)"'
```

Success looks like `"{1,12,15}"`. A solver is also used by the visualization endpoint (`POST /ProblemProvider/visualize`), which calls `solve` and, if you wrote one, `GetSteps` (section 4, step 5). You can read any solver's metadata with `GET /ProblemProvider/info?interface=SubsetSumBruteForce`.

> The folder layout above matches the one in [ProblemTemplate/Templates/README.md](../../ProblemTemplate/Templates/README.md), but that file lists the interface fields only loosely. The interface itself, [Interfaces/SolverInterface.cs](../../Interfaces/SolverInterface.cs), is the source of truth.

## 4. Step by step

### Step 1. Get the template

You have two options. Both give you the same file.

- **Copy it by hand.** The template is [ProblemTemplate/Templates/Solvers/ProblemSolver.txt](../../ProblemTemplate/Templates/Solvers/ProblemSolver.txt). Copy it to your new folder, rename it `<ClassName>.cs`, and replace the placeholders `{PROBLEM}`, `{SOLVER}`, and `{SOLVER_PASCAL_CASE}`.
- **Download it from a running API.** Start the API, then call `GET /ProblemTemplate/solver?problemName=SUBSETSUM&solverName=My Subset Sum Solver`. The two query parameters are `problemName` (the problem's class name, for example `SUBSETSUM`) and `solverName` (the display name; the class name is made from it by removing spaces and odd characters). It returns a zip containing `NPC_SUBSETSUM/Solvers/MySubsetSumSolver.cs` and a README. The code for this is in [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs).

### Step 2. Fill in the members

Every solver implements the members in [Interfaces/SolverInterface.cs](../../Interfaces/SolverInterface.cs). Here is each one in plain words, with the value used by the worked example, [`SubsetSumBruteForce.cs`](../../Problems/NPComplete/NPC_SUBSETSUM/Solvers/SubsetSumBruteForce.cs).

| Member | What it is for | Worked example value |
| --- | --- | --- |
| `solverName` | The name people see in the GUI. A test checks its shape (see section 5). | `"Subset Sum Brute Force"` |
| `solverDefinition` | One sentence on how it works. | `"This is a brute force solver for Subset Sum"` |
| `source` | A citation for where the algorithm comes from. May be empty for a plain brute force. | `""` |
| `contributors` | Names of the people who wrote it. | `{ "Caleb Eardley", "Garret Stouffer" }` |
| `timerHasExpired` | A true/false flag that means "stop now". See step 3. | `{ get; set; }` |
| `solverType` | The style of algorithm (see below). | `SolverType.BruteForce` |
| `complexityBucket` | How the worst-case work grows (see below). | `SolverComplexityBucket.Exponential` |
| `complexity` | Free-text Big-O note. | `"O(n * 2^n), n = \|S\|"` |
| `solve(PROBLEM)` | **The actual algorithm.** Takes a problem object, returns a certificate string. | see step 3 |
| `GetSteps(PROBLEM)` | Optional. Intermediate snapshots for step-by-step visualizations. Defaults to an empty list. | not used here |

The three **declared metadata** members are the ones people forget. "Declared" means a human decides. No tool works them out for you. Their rules are in [SolverType.cs](../../Interfaces/SolverType.cs) and [SolverComplexityBucket.cs](../../Interfaces/SolverComplexityBucket.cs), and the doc comments there explain every choice. Short version:

- `solverType`: read your `solve()` and pick the closest style: `BruteForce` (tries everything, no pruning), `Backtracking` (searches but gives up on dead ends early), `DynamicProgramming`, `Greedy`, `Approximation`, `Heuristic`, and so on. `Quantum` means the solver calls the external quantum-simulator service (see the note at the end of this section).
- `complexityBucket`: the **worst case** growth: `Polynomial`, `Exponential`, or `Factorial`. It describes the worst case even if your solver is fast on typical inputs.
- `complexity`: a Big-O string such as `"O(n * 2^n)"`. Only write one you are confident about. Never guess.

**Do not leave any of the three as `Unclassified` (or empty).** Tests fail if you do (section 5). The template ships them commented out, and a commented-out line means "not declared", so you must remove the comments and fill them in.

You also need a **public constructor with no arguments** (`public SubsetSumBruteForce() { }`). Redux builds your class this way, and so do the tests.

> **About quantum solvers.** Some solvers (`ShorsQuantumSolver`, `SATGroverSolver`, and a few others) do not solve anything themselves. They send the problem to a separate quantum-simulator service whose address is the `BaseURL` setting in [QuantumSolverSettings.cs](../../QuantumSolverSettings.cs) (default `http://localhost:27100`). Those are special cases with their own allowlist in the tests. If you are not adding one, you can ignore this.

### Step 3. Write `solve()`

`solve()` receives a problem object, so the instance text is already parsed for you. In the worked example, a Subset Sum object has a list `S` of numbers and a target `T`. The idea is:

1. Try every non-empty subset of `S`. With `n` numbers there are `2^n` subsets. A loop counter `mask` from `1` to `2^n - 1` stands for each subset: bit number `i` of `mask` being 1 means "include `S[i]`".
2. Turn the subset into a certificate string such as `{1,12,15}`.
3. Ask the verifier whether this certificate works. If yes, return it.
4. If nothing works, return `"{}"`.

The loop at the heart of the worked example:

```csharp
for (int mask = 1; mask < (1 << n); mask++) {
    if (timerHasExpired) return "{}";

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

**Timeouts: `timerHasExpired`.** The interface has a flag, `timerHasExpired`, and two helper methods that flip it (`TimerExpired()` sets it to `true`, `ResetTimer()` sets it back to `false`). The idea: something outside the solver can run a stopwatch and, when time is up, call `TimerExpired()`. A well-behaved solver checks the flag every so often inside its main loop and stops early if it is `true`. Rules:

- Check it **inside your main loop**, not just once at the start. A check that never runs again cannot stop anything.
- Check it often enough to matter, but not so often that the checks slow the real work down.
- When it is `true`, return a value that is safe to hand to callers (see "What to return" below).
- Note: at the time of writing, nothing inside this API calls `TimerExpired()` (the code that would start the stopwatch is not in the repo), so the flag is never set in a normal request. You still must write the check, so the solver is ready when a timer is added, and so your own tests can set `solver.timerHasExpired = true` to prove the solver stops.

**What to return.** The interface does not define a special "no solution" or "timed out" value. The solver's result is just a string, and today Redux uses these conventions:

| Situation | Common return value | Notes |
| --- | --- | --- |
| A solution was found | the certificate, in the problem's `certificateFormat` | for example `{1,12,15}` |
| There is no solution | `"{}"` (an empty certificate) | the worked example. About 60 solvers do this. Some return `""` or other text, so the convention is not universal. |
| The timer expired | the same value as "no solution" in the worked example (`"{}"`) | the template returns the text `"timeout"` instead, and about 8 solvers do that. |

Whatever you pick, document it in a comment, and make sure the **verifier handles it** (an empty `{}` certificate must not crash it, it should just return `false` or, if your problem's format rejects it, throw `CertificateParseException`). Prefer `"{}"` or an empty certificate in the problem's own format for "no solution", because the GUI and verifier already understand that shape.

**Do not throw for "no solution".** Throwing is for real errors. The API turns bad *instance text* into a clean 400 for you, because the problem's constructor throws `ProblemParseException` before `solve()` runs. A crash inside `solve()` itself becomes a 500 error.

### Step 4. Match the certificate format

The string your solver returns **is** a certificate. It should look exactly like what the problem's `certificateFormat` describes, because three other things read it:

1. **The verifier** (a solved certificate should verify as `true`).
2. **The GUI**, which shows it and may draw it.
3. **Reductions** that map solutions (`mapSolutions`) from one problem to another.

For Subset Sum the format is `{K | K is set}` with example `{1,12,15}`, and the solver builds exactly that shape. Check yours by feeding it to the verifier in a test (see section 5). Is a solver's answer required to pass the verifier? In practice, yes whenever the solver returns a real answer. There is no single automatic test that checks this for every solver, so **your own tests must** (for the empty/no-solution value you return, make sure you test what the verifier does with it).

### Step 5 (optional). Step-by-step visualizations with `GetSteps`

If a visualization wants to show the solver working step by step, override `GetSteps(PROBLEM)` and return a list of snapshots. By default it returns an empty list, which is fine. The visualization endpoint passes this list to the visualization class. See [adding-a-visualization.md](adding-a-visualization.md) for the other half. A tiny real example that returns a one-item list: `GetSteps` in [PumpSchedulingCMSolver.cs](../../Problems/NPHard/NPH_PUMPSCHEDULINGCM/Solvers/PumpSchedulingCMSolver.cs).

## 5. Write tests

### Tests that already check your solver for free

You do not have to register your solver with these tests. They find it automatically by scanning every solver class, and each one runs once per solver.

**These tests will check your solver automatically:**

| Test file | What it checks about your solver |
| --- | --- |
| [NamingConvention_Tests.cs](../../redux-tests/Metadata/NamingConvention_Tests.cs) (`SolverName_MatchesNamingConvention`) | Your class builds with no arguments, and `solverName` matches one of two shapes: `"<Problem> <Approach>"` (title-case words, 2 or more, for example `"Subset Sum Brute Force"`) or `"<Person's name> ... Algorithm"` (for example `"Dijkstra's Algorithm"`). A raw class name like `SubsetSumBruteForce`, or a placeholder like `"Default Solver"`, fails. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`NoNewUndeclaredSolverType`) | `solverType` is not `Unclassified`. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`NoNewUndeclaredComplexityBucket`) | `complexityBucket` is not `Unclassified`. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`NoNewUndeclaredComplexity`) | `complexity` is not an empty string. |
| [SolverType_Tests.cs](../../redux-tests/Metadata/SolverType_Tests.cs) (`Info_...` and `AllInfo_...` tests) | The three declared values appear in the API's JSON as words (`"BruteForce"`), not numbers. |
| [ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs) (`Solve_GarbageInstance_IsNever500`) | Sending garbage (`""`, `{{{`, `not an instance`, `{a,b}:::`) as the instance to `POST /ProblemProvider/solve` never produces an HTTP 500. A 400 with the instance format is the expected answer. Because your problem's constructor does the parsing, this normally passes without extra work. |

Not checked for free: that your solver gives the **right** answer, that its answer passes the verifier, and that it stops when `timerHasExpired` is set. There is also no generic performance test over solvers (the Performance tests cover the API's endpoints and reductions), so a solver that is too slow on the default instance will only show up as a slow test of your own. Those are your job.

> The three "Unclassified" tests work with an **allowlist** inside `SolverType_Tests.cs`. Do **not** add your class to it. The allowlists are meant to stay as short as possible and are only for special cases such as quantum solvers. Fix the declarations instead.

### Tests you write yourself

Solver tests live in the test project, one folder per problem:

```text
redux-tests/Problems/NPC_<PROBLEM>/<PROBLEM>_Tests.cs
```

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
- [ ] The timer: set `solver.timerHasExpired = true;` before calling `solve`, and check that it returns quickly with your documented value instead of searching.

For the timer case:

```csharp
SubsetSumBruteForce solver = new SubsetSumBruteForce();
solver.timerHasExpired = true;
Assert.Equal("{}", solver.solve(new SUBSETSUM("({1,7,12,15},28)")));
```

If you wrote an **approximation or heuristic** solver, it may legitimately return a wrong or empty answer. The worked example's second solver tests that rule: its answer is either `"{}"` or passes the verifier (`FastApproximation_NonEmptyResult_AlwaysPassesVerifier`). Copy that pattern.

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
- [ ] `solverType`, `complexityBucket`, and `complexity` are declared (not `Unclassified`, not empty), and you are confident they are true.
- [ ] `solve()` checks `timerHasExpired` inside its main loop.
- [ ] The returned certificate matches the problem's `certificateFormat`, and a returned answer passes the problem's verifier.
- [ ] The "no solution" and "timed out" results are documented in comments and tested.
- [ ] If this is the problem's default solver, the problem class's `defaultSolver` points to it, and the problem's default instance solves quickly.
- [ ] You added tests for solved, no-solution, and timer cases.
- [ ] `dotnet format Redux.slnx` was run, and build and tests pass.
- [ ] You read the rbs report on your PR (see [building-and-testing.md](building-and-testing.md)).
- [ ] The PR targets `CSharpAPI`.

## 8. Common mistakes

- **Leaving `solverType`, `complexityBucket`, or `complexity` undeclared.** The template has them commented out. Three metadata tests fail with a message naming your class.
- **A `solverName` that is just the class name** (`SubsetSumBruteForce`) or a placeholder. Use `"<Problem> <Approach>"` or `"<Person> ... Algorithm"`.
- **Never checking `timerHasExpired`**, or checking it only once before the loop. Check it inside the loop.
- **Output that does not match `certificateFormat`.** The verifier will throw `CertificateParseException`, or the GUI will not understand it. Always test your solver's output against the verifier.
- **Forgetting the template's placeholder return.** The template ends with `return "{}";`, which means "no solution" for every input. Replace it with the real algorithm.
- **A default instance that takes forever.** Brute force grows exponentially. Keep the problem's `defaultInstance` small.
- **Guessing the Big-O string.** A wrong `complexity` is a public claim. Leave out claims you are not sure about, and ask in the PR.
- **Throwing for "no solution"** or for a timeout. Return a documented value instead.
- **Re-writing the verifier rule inside the solver.** Reuse `problem.defaultVerifier` so the two cannot disagree.
- **Reusing a class name.** Class names are keys, ignoring case. A duplicate crashes the registry for everyone.
- **Folder and namespace do not match**, or the file sits under the wrong problem.
- **Writing a controller.** Not needed. Reflection exposes your class.
- **Editing test allowlists** to turn a failure green. Fix the cause.
