# Building and testing

This guide shows how to check your work before you push, and what to do when the automatic checks on your pull request (PR) turn red.

Some words you will see:

- **Build**: turning the C# source code into a program the computer can run. If the code is broken, the build fails.
- **Solution file** (`Redux.slnx`): a list that tells .NET which projects belong together (here, the API and its tests). You name it in every command.
- **CI** (continuous integration): a robot on GitHub that builds and tests your code every time you open or update a PR. If it fails, the PR cannot be merged.
- **Formatting**: spacing, indentation, and similar style rules. They are kept in [.editorconfig](../../.editorconfig), and a tool fixes them for you.
- **Coverage**: the percentage of the code that tests actually run.
- **Dev container**: a ready-made, identical Linux workspace inside Docker, so everyone builds the same way.

Need the one-time setup (installing .NET, cloning)? A setup guide is coming soon; see the [guides index](README.md).

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
2. **Formatting is part of the build.** [Directory.Build.targets](../../Directory.Build.targets) runs `dotnet format Redux.slnx --verify-no-changes` before every Release build. Badly formatted code makes the **build** fail. The fix is to run `dotnet format Redux.slnx` first.
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

**About coverage.** `rbs.toml` asks for at least 80% coverage (`coverage-min = 80`), and the project is currently at about 61%. So the `unit-test` line is expected to be red until coverage improves. Do not panic about that gap, because you did not cause it. Do add tests for the code you write, so you do not make it worse.

If a gate is red and you are not sure why, paste the line into a PR comment and ask. A format or lint failure is always worth fixing before you ask for review.
