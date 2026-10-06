# Building and testing

This guide shows how to check your work before you push, and what to do when the automatic checks on your pull request (PR) turn red.

Some words you will see:

- **Build**: turning the C# source code into a program the computer can run. If the code is broken, the build fails.
- **Solution file** (`Redux.slnx`): a list that tells .NET which projects belong together (here, the API and its tests). You name it in every command.
- **CI** (continuous integration): a robot on GitHub that builds and tests your code every time you open or update a PR. If it fails, the PR cannot be merged.
- **Formatting**: spacing, indentation, and similar style rules. They are kept in [.editorconfig](../../.editorconfig), and a tool fixes them for you.
- **Coverage**: the percentage of the code that tests actually run.
- **Dev container**: a ready-made, identical Linux workspace inside Docker, so everyone builds the same way.

Need the one-time setup (installing .NET, cloning, running the API)? Start with [setup.md](setup.md).

---

## 1. What CI checks on your PR

PRs to the Redux repo target the branch `CSharpAPI`. Four automatic checks run.

| Check | Blocks merging? | What it does |
| --- | --- | --- |
| **dotnet build** ([main.yml](../../.github/workflows/main.yml)) | Yes | Restores packages, builds in Release mode, then runs the unit tests, then runs the "Performance" tests. |
| **rbs** ([rbs.yml](../../.github/workflows/rbs.yml)) | **No, and it always shows green.** Read its report. | Runs a longer list of quality gates (see section 4). |
| **CodeQL** ([codeql.yml](../../.github/workflows/codeql.yml)) | Look at its results | GitHub's security scanner for C# and the workflow files. You cannot run it on your laptop. Open the PR's "Code scanning" results to see any alerts. |
| docker ([docker.yml](../../.github/workflows/docker.yml)) | Not applicable | Only runs after a merge. You do not need to do anything for it. |

The commands inside the **dotnet build** check are exactly these:

```bash
dotnet restore Redux.slnx
dotnet build Redux.slnx --no-restore --configuration Release
dotnet test Redux.slnx --no-restore --no-build --configuration Release --filter "Category!=Performance"
dotnet test Redux.slnx --no-restore --no-build --configuration Release --filter "Category=Performance"
```

Three things about them that surprise people:

1. **Always name `Redux.slnx`.** A bare `dotnet build` in the repo root fails with `error MSB1011`, because the root folder holds both `API.csproj` and `Redux.slnx` and .NET will not guess which one you mean.
2. **Formatting is part of the build.** [Directory.Build.targets](../../Directory.Build.targets) runs `dotnet format Redux.slnx --verify-no-changes` as part of every Release build. Badly formatted code makes the **build** fail. The fix is to run `dotnet format Redux.slnx` first. Note that this check runs only *after* the code compiles. If your code has a compile error, you will see that error first, and the formatting errors only appear once it is fixed. So a build that fails, gets fixed, and then fails again with `WHITESPACE` errors is normal.
3. **Warnings are errors.** [Directory.Build.props](../../Directory.Build.props) sets `TreatWarningsAsErrors`. So a compiler warning (an unused variable, say) fails the build. In practice the compiler is the linter.

**Performance tests** are tests tagged `Category=Performance`. They run as their own step. If one fails, it means "too slow", not "wrong answer". Run it again once to rule out a slow moment on your computer, then look at the code you just added.

## 2. The "before I push" routine

Run these from the repo root, the folder that contains `Redux.slnx`.

**Step 1. Fix formatting.**

```bash
dotnet format Redux.slnx
```

No output means nothing needed fixing, or it fixed things quietly. Either is fine.

**Step 2. Build.**

```bash
dotnet build Redux.slnx -c Release
```

Success looks like this (the time will differ):

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Step 3. Run the tests.**

```bash
dotnet test Redux.slnx -c Release --no-build --filter "Category!=Performance"
```

Success looks like this (your counts will differ):

```text
Passed!  - Failed:     0, Passed:  3006, Skipped:     4, Total:  3010, Duration: 45 s - redux-tests.dll (net10.0)
```

"Skipped" tests are on purpose. You are looking for `Failed: 0`. The first run can take a minute or two.

**Step 4. Run the performance tests.**

```bash
dotnet test Redux.slnx -c Release --no-build --filter "Category=Performance"
```

You should see `Passed!` again (about 25 tests).

**Step 5. Run only one test, or one test class.** Use this while you are working so you do not wait for all 3,000 tests. The text after `~` just has to appear somewhere in the test's full name.

```bash
# One test (here, the name of a test in redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs)
dotnet test Redux.slnx -c Release --no-build --filter "FullyQualifiedName~SUBSETSUM_PartitionReduction_Accepts_New_Instance_Format"

# One whole class of tests
dotnet test Redux.slnx -c Release --no-build --filter "FullyQualifiedName~redux_tests.SUBSETSUM_Tests"
```

The first prints `Passed: 1`, the second `Passed: 34` (at the time of writing). If you see "No test matches the given testcase filter", check your spelling. If you changed code since the last build, leave out `--no-build` so it rebuilds first.

Before you push, run all steps once. That is the same thing CI will do.

## 3. Find your situation

### "I'm on Windows or Mac and I don't have Docker"

Use the commands in section 2. You only need the .NET 10 SDK. This is enough to get the **dotnet build** check green, which is the one that blocks merging. You cannot run the rbs checks locally this way, so you will read their report on the PR instead (section 4).

### "I'm using the dev container"

The dev container is the closest match to CI. Both the Redux and Redux_GUI repos have a `.devcontainer/` folder with the Redux Build System (**rbs**) pre-installed.

1. Install Docker Desktop and VS Code, and start Docker.
2. Open the repo folder in VS Code.
3. Press `F1`, type `Reopen in Container`, and pick it. The first time takes a few minutes.
4. In the terminal inside VS Code, run:

   ```bash
   rbs ci
   ```

   This runs every gate and writes a report named `report.md`. CI runs the same command in the same container, so what you see locally is what you will see on the PR.
5. To run one gate at a time, use `rbs lint`, `rbs unit-test`, `rbs audit`, `rbs format-check`, `rbs build`, or `rbs integration-test`.

### "I only changed the GUI (Redux_GUI)"

Redux_GUI is a separate repo (the Next.js website). PRs there target the branch `ReduxAPI_GUI`. Run these from the root of the Redux_GUI folder:

```bash
npm ci                         # install exactly the packages the project expects
npm run format                 # fixes formatting (runs biome)
npm run lint:fix               # fixes lint problems it can fix (runs eslint)
npm run format:check           # what the rbs format gate runs
npm run lint                   # what the rbs lint gate runs
npm run check:visualizations   # gating check, see below
npm audit --omit=dev           # looks for known-vulnerable packages
npm run build                  # builds the site
```

Things worth knowing:

- Unlike Redux, rbs on Redux_GUI is **not soft**. If any gate fails, the check goes red.
- `unit-test` and `typecheck` show as `skipped` there, because the repo has no `test` script and no `tsconfig.json`. Skipped is fine.
- `npm run check:visualizations` is its own required check ("visualization coverage"). It fails when `components/Visualization/svgs/visualizationTypes.json` does not match the renderer keys in `components/Visualization/svgs/Visualizations.js`. If you add or rename a visualization, update both.
- `npm run test:e2e` runs the Playwright browser tests. It needs the Redux API running (set `REDUX_BASE_URL`, and the API normally listens on port 27000).
- The ESLint scan inside CodeQL does not block merging, but its alerts show up on the PR.

The GUI has its own testing notes. Read them instead of copying them here: [Redux_GUI TESTING.md](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/TESTING.md).

### "CI is red, now what?"

Click **Details** next to the red check, open the failed step, and read the last 30 lines. Then match it up:

| What you see | What it means | Fix |
| --- | --- | --- |
| `MSB1011` | You ran `dotnet build` or `dotnet test` without a file name. | Add `Redux.slnx`. |
| The build step fails and mentions `dotnet format` or "formatting" | Your code is not formatted. | Run `dotnet format Redux.slnx`, commit the changes, push. |
| `error CS...` or a warning shown as an error | The code does not compile, or has a compiler warning. | Fix the line it points to. |
| Unit tests: `Failed: 1` (or more) | A test fails. | The log names the test. Run just that test (section 2, step 5), read the message, and fix the code or the test. |
| Performance tests fail | Something is too slow. | Look at what you just added. Re-run once to rule out a fluke. |
| It works on your machine but not in CI | Different OS or leftover files. | Try the dev container (above). |

If a failure looks unrelated to your change, say so in a PR comment and ask a maintainer. Do not weaken or delete a test just to turn it green.

## 4. How to read the rbs report

**This is important.** On Redux, rbs runs in "soft" mode, which means its check mark **always shows green, even when gates inside it fail**. So a green rbs check does not mean everything passed. You have to read the report. Do this on every PR.

Where to find it:

- A comment on your PR written by the rbs bot (it gets updated each time you push), and
- the job summary, shown at the bottom of the rbs run's page on GitHub Actions.

The report has one line per gate. For the Redux (dotnet) setup, configured in [rbs.toml](../../rbs.toml), the gates are:

| Gate | What it runs | What to do if it is red |
| --- | --- | --- |
| `audit` | `dotnet list Redux.slnx package --vulnerable --include-transitive` | A package has a known security problem. Mention it in your PR. Do not go hunting through other people's dependencies unless you added the package. |
| `format-check` | `dotnet format Redux.slnx --verify-no-changes` | Run `dotnet format Redux.slnx` and push. |
| `lint` | `dotnet build Redux.slnx -c Release` | Fix the compiler errors and warnings. |
| `typecheck` | skipped | Nothing. The compiler already checked types in `lint`. |
| `unit-test` | `dotnet test Redux.slnx -c Release --collect "XPlat Code Coverage"` | See the coverage note just below. |
| `build`, `integration-test`, `push` | Build and test the Docker image (`push` is a dry run on Redux) | Rarely your fault. Ask a maintainer if these fail. |

**About coverage.** `rbs.toml` asks for at least 80% coverage (`coverage-min = 80`). The project is currently at about 85%, so the `unit-test` line should be green. If it turns red on your PR, the coverage dropped below 80%, which usually means new code went in without tests. Add tests for the code you write, and coverage stays above the line.

**CI does not check that you wrote tests.** The required build check only makes sure every test that exists passes. A new problem, solver, verifier, or reduction with no tests of its own can still pass it, because the shared tests only check the basics (metadata, names, bad input). Coverage won't catch it either: on Redux the coverage line is in the soft rbs report, and one new class barely moves the overall number. Your own tests are the only thing that checks your code gives the *right answers*. For example, a verifier that rejects every correct answer passes every shared test. Each task guide has a "Tests you write yourself" section and a checklist saying which tests to add, and reviewers will look for them.

If a gate is red and you are not sure why, paste the line into a PR comment and ask. A format or lint failure is always worth fixing before you ask for review.

## 5. Required tests for each kind of change

CI does not check that these tests exist (see the note just above), so use this section as your checklist. Reviewers will look for them.

**If you used the problem template, you already have most of these tests.** `GET /ProblemTemplate?problemName=...` also gives you a test file, `redux-tests/Problems/NPC_<NAME>/<NAME>_Tests.cs`, with the tests for a new problem already written. The generic ones pass once your code follows the rules. The stub ones contain a `TODO` and fail on purpose until you fill them in. See [adding-a-problem.md, "Where each file in the zip goes"](adding-a-problem.md#where-each-file-in-the-zip-goes). The examples below are for when you add a test by hand, or want to see a finished version of a stub.

Every example is copied from a real test in the repo, so you can look it up. Lines that start with `// replace:` show what to change for your own problem. Each example sits in a collapsed block under its checklist: click it to open.

**Where tests go.** One test file per problem: `redux-tests/Problems/<Prefix>_<NAME>/<NAME>_Tests.cs`, for example [redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). Solver, verifier, and visualization tests go in their problem's file. Reduction tests go in the file of the problem you reduce **from**. If the file exists, add to it. If not, create it.

**How to write them.**
- Use small, hand-picked instances whose answers you worked out on paper.
- Use `[Fact]` for one check, and `[Theory]` with `[InlineData(...)]` to run the same check on many inputs.
- Name tests like sentences: `<PROBLEM>_<What>_<Expectation>`, for example `SUBSETSUM_Verifier_RejectsNumberNotInS`.
- Test both directions. A verifier that says "yes" to everything passes an "accepts the right answer" test, so also test that it rejects wrong answers.

### Adding a problem

Details: [adding-a-problem.md, "Tests you write yourself"](adding-a-problem.md#tests-you-write-yourself).

- [ ] The default instance builds, and its fields hold the values you expect.
- [ ] A malformed instance throws `ProblemParseException`. Use a `[Theory]` with several bad inputs: an empty string, the wrong shape, and a non-number where a number belongs.
- [ ] The verifier tests and the solver tests below. A new problem comes with both.
- [ ] **Edit an existing test:** add your problem's class name to the hard-coded list for its complexity class in [Navigation_Endpoint_Tests.cs](../../redux-tests/Endpoints/Navigation_Endpoint_Tests.cs) (NP-complete, P, or NP-hard). This test fails until you do.

<details>
<summary>Example tests for a problem (copy and adapt)</summary>

From [SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). The top of the file has the `using` lines and the class:

```csharp
using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETSUM;
// replace: SUBSETSUM with your class name, in every line below

namespace redux_tests;
#pragma warning disable CS1591

public class SUBSETSUM_Tests {
    // tests go here
}
```

The default instance builds, and its fields hold the values you expect:

```csharp
[Fact]
public void SUBSETSUM_Default_Instantiation() {
    SUBSETSUM problem = new SUBSETSUM();
    // replace: your default instance string, and your own fields and values
    Assert.Equal("({1,7,12,15},28)", problem.instance);
    Assert.Equal("({1,7,12,15},28)", problem.defaultInstance);
    Assert.Equal(4, problem.S.Count);
    Assert.Equal(28, problem.T);
}
```

A malformed instance throws `ProblemParseException`. One `[Theory]` runs the same check on every `[InlineData]` line:

```csharp
[Theory]
[InlineData("{{1,7,12,15} : 28}")] // old colon format
[InlineData("")]                    // empty
[InlineData("101")]                 // bare string
[InlineData("({1,x,3},5)")]         // non-integer element in S
[InlineData("({1,2,3},y)")]         // non-integer T
public void SUBSETSUM_Constructor_Throws_On_Invalid_Instance(string instance) {
    // replace: your problem's class name; the bad inputs are the ones to think about
    Assert.Throws<ProblemParseException>(() => new SUBSETSUM(instance));
}
```

The "edit an existing test" item has no code: open [Navigation_Endpoint_Tests.cs](../../redux-tests/Endpoints/Navigation_Endpoint_Tests.cs) and add your class name to the list for your complexity class. The verifier and solver items are in the next two blocks.

</details>

### Adding a verifier

Details: [adding-a-verifier.md, "Tests you write yourself"](adding-a-verifier.md#tests-you-write-yourself).

- [ ] A valid certificate is accepted.
- [ ] The smallest valid certificate is accepted (for example, one element).
- [ ] Well-formed but wrong certificates are rejected (`false`). Try several kinds of wrong: the wrong total, a value that is not in the instance, a value used too many times, a missing piece.
- [ ] A malformed certificate throws `CertificateParseException`: a non-number where a number belongs, unbalanced braces, the wrong shape.
- [ ] An empty string `""`. It usually throws. Say in a comment which behavior your problem uses.
- [ ] The problem's own solver output for the default instance is accepted.

<details>
<summary>Example tests for a verifier (copy and adapt)</summary>

From [SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). Valid certificates are accepted. The second row is the smallest valid certificate (one element):

```csharp
[Theory]
[InlineData("({1,7,12,15},28)", "{1,12,15}")] // 1 + 12 + 15 = 28
[InlineData("({5,1,2},5)", "{5}")]             // singleton {S[0]} is a valid witness
[InlineData("({3,5,9},8)", "{3,5}")]           // 3 + 5 = 8
public void SUBSETSUM_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
    // replace: SUBSETSUM, SubsetSumVerifier, and the instance/certificate pairs (work them out by hand)
    SUBSETSUM problem = new SUBSETSUM(instance);
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    Assert.True(verifier.verify(problem, certificate));
}
```

Well-formed but wrong certificates are rejected with `false`. The rows try a wrong total, a value that is not in the instance, a bare number that is not in the instance, and a value used too many times:

```csharp
[Theory]
[InlineData("({1,7,12,15},28)", "{1,7}")]   // sum 8 != 28
[InlineData("({1,7,12,15},28)", "{1,99}")]  // 99 is not a member of S
[InlineData("({1,7,12,15},28)", "101")]     // SPADE reads a bare scalar as {101}; 101 is not in S
[InlineData("({7,1},14)", "{7,7}")]         // reuses 7 beyond its multiplicity in S (#259)
public void SUBSETSUM_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
    SUBSETSUM problem = new SUBSETSUM(instance);
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    Assert.False(verifier.verify(problem, certificate));
}
```

A malformed certificate throws `CertificateParseException`. The first row is the empty string. Subset Sum throws for it, so the test says so. If your problem does something else with `""`, test that instead and say which in a comment:

```csharp
[Theory]
[InlineData("")]          // empty: SPADE parse failure
[InlineData("{1,x,3}")]   // non-integer token
public void SUBSETSUM_Verifier_Throws_On_Malformed_Certificate(string certificate) {
    // replace: a valid default instance for your problem, and your own malformed certificates
    SUBSETSUM problem = new SUBSETSUM("({1,7,12,15},28)");
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
}
```

The problem's own solver output for the default instance is accepted:

```csharp
[Fact]
public void FastApproximation_DefaultInstance_FindsVerifiedSolution() {
    SUBSETSUM problem = new SUBSETSUM();
    // replace: the default solver for your problem (here, the extra solver FastApproximation)
    string solution = new FastApproximation().solve(problem);

    Assert.NotEqual("{}", solution);
    Assert.True(new SubsetSumVerifier().verify(problem, solution));
}
```

</details>

### Adding a solver

Details: [adding-a-solver.md, "Tests you write yourself"](adding-a-solver.md#tests-you-write-yourself).

- [ ] On the default instance, the solver returns an answer that the verifier accepts.
- [ ] One or two other small instances that have a solution, each answer accepted by the verifier.
- [ ] An instance with **no solution** returns your documented "no solution" value (usually `"{}"`).
- [ ] A tricky small case: the answer is the first item only, or the last item only.
- [ ] The smallest allowed input (one item, or empty if the problem allows it).
- [ ] The timer: with `timerHasExpired = true` before calling `solve`, it returns quickly with your documented value.
- [ ] Approximation or heuristic solvers only: the answer is either the "no solution" value or passes the verifier.

<details>
<summary>Example tests for a solver (copy and adapt)</summary>

From [SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). The solver's answer is accepted by the verifier, on the default instance and on other small instances that have a solution:

```csharp
[Theory]
[InlineData("({1,7,12,15},28)")]
[InlineData("({3,5,9},14)")] // 5 + 9
[InlineData("({2,4,6},10)")] // 4 + 6
public void SUBSETSUM_Solver_ProducesVerifiableCertificate(string instance) {
    // replace: SUBSETSUM, SubsetSumBruteForce, SubsetSumVerifier, and the instances
    SUBSETSUM problem = new SUBSETSUM(instance);
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    string solution = solver.solve(problem);
    Assert.True(verifier.verify(problem, solution));
}
```

An instance with no solution returns the documented "no solution" value (for Subset Sum, `"{}"`):

```csharp
[Fact]
public void SUBSETSUM_Solver_Returns_Empty_When_No_Solution() {
    // replace: an instance with no solution, and your "no solution" value
    SUBSETSUM problem = new SUBSETSUM("({2,4,6},5)");
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    Assert.Equal("{}", solver.solve(problem));
}
```

A tricky small case: the only answer is the first item. (An old bug skipped this one.)

```csharp
[Fact]
public void SUBSETSUM_Solver_Finds_FirstElement_Only_Solution() {
    // Regression for the enumeration bug: the only subset summing to 5 is {5},
    // the singleton of the first element, which the old solver never tested.
    SUBSETSUM problem = new SUBSETSUM("({5,1,2},5)");
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    string solution = solver.solve(problem);
    Assert.Equal("{5}", solution);
}
```

The smallest allowed input, one item:

```csharp
[Fact]
public void SUBSETSUM_Solver_Handles_Single_Element_Instance() {
    // The smallest useful input: one element that equals the target.
    SUBSETSUM problem = new SUBSETSUM("({5},5)");
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    Assert.Equal("{5}", solver.solve(problem));
}
```

The timer: with `timerHasExpired = true`, the solver gives up and returns its documented value. Subset Sum's brute force returns `"{}"`. The template's solver returns `"timeout"`, so write whichever your solver does:

```csharp
[Fact]
public void SUBSETSUM_Solver_Returns_Empty_When_Timer_Has_Expired() {
    // An expired timer makes the solver give up and return the "no solution" value.
    SUBSETSUM problem = new SUBSETSUM();
    SubsetSumBruteForce solver = new SubsetSumBruteForce();
    solver.timerHasExpired = true;
    Assert.Equal("{}", solver.solve(problem));
}
```

Approximation or heuristic solvers only: the answer is either the "no solution" value or passes the verifier. This is the real test for Subset Sum's `FastApproximation`, with some of its tricky instances (the real test has three more `[InlineData]` rows, left out here to keep the example short):

```csharp
[Theory]
[InlineData("({1,7,12,15},28)")]
[InlineData("({1000,1001,5},1006)")]
[InlineData("({3,5,9},1)")]   // no solution
[InlineData("({3,5,9},0)")]   // empty-sum target
[InlineData("({3,5,9},-4)")]  // negative target
public void FastApproximation_NonEmptyResult_AlwaysPassesVerifier(string instance) {
    SUBSETSUM problem = new SUBSETSUM(instance);
    FastApproximation solver = new FastApproximation();

    string solution = solver.solve(problem);

    Assert.True(solution == "{}" || new SubsetSumVerifier().verify(problem, solution),
        $"FastApproximation returned unverifiable certificate {solution} for {instance}");
}
```

</details>

### Adding a reduction

Details: [adding-a-reduction.md, "Tests you write yourself"](adding-a-reduction.md#tests-you-write-yourself).

- [ ] The produced instance (`reductionTo`) matches the one you worked out by hand.
- [ ] `mapSolutions` turns a solution of the FROM problem into the matching solution of the TO problem.
- [ ] An edge case: an instance with no solution, or the smallest allowed input.

<details>
<summary>Example tests for a reduction (copy and adapt)</summary>

The produced instance (`reductionTo`) matches the one you worked out by hand. From [SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). The second row is also the smallest-input edge case:

```csharp
[Theory]
[InlineData("({3,5,7},8)", "{3,5,7,9,8}")] // sum 15: appends T+1 = 9 and sum-T+1 = 8
[InlineData("({5},5)", "{5,6,1}")]         // smallest input: sum 5: appends 6 and 1
public void SUBSETSUM_PartitionReduction_Produces_Expected_Instance(string instance, string expected) {
    // replace: your reduction class, the FROM instances, and the TO instances you worked out by hand
    SubsetSumToPartitionReduction reduction = new SubsetSumToPartitionReduction(instance);
    Assert.Equal(expected, reduction.reductionTo.instance);
}
```

`mapSolutions` turns a solution of the FROM problem into a solution of the TO problem. The best way to test it is to run it and ask the TO problem's verifier. This is the real test from [SAT3_Tests.cs](../../redux-tests/Problems/NPC_SAT3/SAT3_Tests.cs) (SAT3 to Clique):

```csharp
[Theory]
[InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
[InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3) & (x1 | !x2 | x3)")]
public void SAT3_To_CLIQUE_Reduction_SAT3_Solution_Maps_To_Valid_Clique_Certificate(string sat3Instance) {
    // A valid SAT3 assignment must map to a valid k-clique certificate.
    // replace: the FROM problem, solver, reduction, TO problem, and TO verifier
    SAT3 sat3 = new SAT3(sat3Instance);
    SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
    CLIQUE clique = reduction.reductionTo;

    Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
    string sat3Solution = solver.solve(sat3);
    Assert.NotEqual("No Solution", sat3Solution);

    string cliqueCertificate = reduction.mapSolutions(sat3Solution);

    CliqueVerifier cliqueVerifier = new CliqueVerifier();
    Assert.True(cliqueVerifier.verify(clique, cliqueCertificate));
}
```

Remember the `using` lines for both problems and for the reduction's namespace (the top of `SUBSETSUM_Tests.cs` and `SAT3_Tests.cs` show them). Put the tests in the file of the problem you reduce **from**.

</details>

### Adding a visualization

Details: [adding-a-visualization.md, "Tests you write yourself"](adding-a-visualization.md#tests-you-write-yourself).

- [ ] `visualize` returns the payload you expect for a small instance.
- [ ] `SolvedVisualization` marks the parts of the solution (for example, with the solution color).
- [ ] An empty or bad solution string is handled.
- [ ] New visualization **type** only: the Redux_GUI side passes `npm run check:visualizations` (see the visualization guide).

<details>
<summary>Example tests for a visualization (copy and adapt)</summary>

From [MAXCUT_Tests.cs](../../redux-tests/Problems/NPC_MAXCUT/MAXCUT_Tests.cs). The extra `using` lines you need:

```csharp
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MAXCUT.Visualizations;
// replace: MAXCUT with your problem, and Graphs with the namespace of your payload class
```

`visualize` returns the payload you expect for a small instance:

```csharp
[Fact]
public void MAXCUT_Visualization_Returns_Graph() {
    MAXCUT problem = new MAXCUT();
    MaxCutVisualization viz = new MaxCutVisualization();
    // replace: the cast (API_GraphJSON) with your payload class, and the counts with your own
    var graph = (API_GraphJSON)viz.visualize(problem);
    Assert.Equal(5, graph.nodes.Count);
    Assert.Equal(6, graph.links.Count);
}
```

`SolvedVisualization` marks the parts of the solution with the solution color:

```csharp
[Fact]
public void MAXCUT_SolvedVisualization_Colors_S_Nodes() {
    string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
    MAXCUT problem = new MAXCUT(instance);
    MaxCutVisualization viz = new MaxCutVisualization();
    // replace: a small instance and a solution you worked out by hand
    var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{3}");

    var node3 = graph.nodes.FirstOrDefault(n => n.name == "3");
    Assert.NotNull(node3);
    Assert.Equal("Solution", node3!.color);
}
```

An empty solution is handled. Nothing is marked as part of the solution:

```csharp
[Fact]
public void MAXCUT_SolvedVisualization_Empty_Solution_Returns_Plain_Graph() {
    MAXCUT problem = new MAXCUT();
    MaxCutVisualization viz = new MaxCutVisualization();
    var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{}");
    Assert.All(graph.nodes, n => Assert.NotEqual("Solution", n.color));
    Assert.All(graph.links, l => Assert.NotEqual("Solution", l.color));
}
```

</details>

### Tests that run automatically

You write nothing for these. They find your new class by themselves and check the basics, which is why they cannot tell whether your answers are right. Each task guide lists the ones that apply to it.

| What they check | Where |
| --- | --- |
| Metadata is declared, not left `Unclassified` (complexity class, problem type, solver, reduction, and visualization types) | [redux-tests/Metadata/](../../redux-tests/Metadata/) |
| Names follow the naming rules (for example, `Default <Problem> Verifier`) | [NamingConvention_Tests.cs](../../redux-tests/Metadata/NamingConvention_Tests.cs) |
| Every name in a `contributors` list has an entry in `wwwroot/contributorInfo.json` | [ContributorProfile_Tests.cs](../../redux-tests/Navigation/ContributorProfile_Tests.cs) |
| Garbage input never causes a server error (500) | [ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs) |
| Every reduction builds and produces an instance the TO problem can read | [ReductionSmoke_Tests.cs](../../redux-tests/Metadata/ReductionSmoke_Tests.cs) |
| Default instances run within a time budget (the separate "Performance" step) | [Performance_Tests.cs](../../redux-tests/Endpoints/Performance_Tests.cs) |

**Never** add your class to a test's allowlist, or weaken or delete a test, to turn it green. Fix the cause instead. If you think a test itself is wrong, say so in your PR and ask.
