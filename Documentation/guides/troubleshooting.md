# Troubleshooting

Common errors in one place, organized as **what you see**, **what it means**, **how to fix it**. Search this page (`Ctrl+F`) for a piece of your error message. Each section links to the guide that explains more.

Some words you will see:

- **CI**: the robot on GitHub that builds and tests your PR.
- **rbs**: the Redux Build System, a longer list of quality checks that also runs on your PR.
- **Reflection**: C# code that finds your classes by looking at the program while it runs. Redux uses it to discover problems, solvers, and so on.

Commands assume you are in the repo root (the folder with `Redux.slnx`) unless stated.

---

## 1. Setting up and running

| What you see | What it means | How to fix it |
| --- | --- | --- |
| `error MSB1011: Specify which project or solution file to use because this folder contains more than one project or solution file.` | You ran `dotnet build` or `dotnet test` with no file name. The repo root holds both `API.csproj` and `Redux.slnx`, so .NET will not guess. | Name the file: `dotnet build Redux.slnx`, `dotnet test Redux.slnx`. (Plain `dotnet run` is fine, because it only considers project files.) See [building-and-testing.md](building-and-testing.md). |
| `dotnet : The term 'dotnet' is not recognized` (or `command not found`) | The .NET SDK is not installed, or your terminal was opened before you installed it. | Install the .NET 10 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download), then open a **new** terminal. See [setup.md](setup.md). |
| `The current .NET SDK does not support targeting .NET 10.0` or `NETSDK1045` | Your newest SDK is older than 10. | Run `dotnet --list-sdks`. If no line starts with `10.`, install the .NET 10 SDK. Older SDKs can stay installed. Then open a new terminal and check `dotnet --version` again. |
| `Failed to bind to address http://0.0.0.0:27000: address already in use` | Port 27000 is taken, usually by another copy of Redux (a forgotten terminal, `dotnet watch`, or a Docker container). | Find and stop it. Windows PowerShell: `Get-NetTCPConnection -LocalPort 27000` shows the process id, then `Stop-Process -Id <id>`. Mac or Linux: `lsof -i :27000`, then `kill <pid>`. Docker: `docker ps`, then `docker stop reduxapi`. |
| The Docker container runs but `http://127.0.0.1:27000` says "connection refused" | Wrong port mapping. The app listens on 27000 inside the container. | Use `-p 27000:27000`, not `-p 27000:80`. See [setup.md](setup.md). |
| The website (Redux_GUI) shows no problems | The API is not running, or is not on port 27000. | Start the API ([setup.md](setup.md)) and open <http://127.0.0.1:27000/health>. It should say `ok`. |
| The API crashes at startup with `An item with the same key has already been added. Key: <something>` | Two classes of the same kind share a name, ignoring case. See the next section. | See section 2. |

## 2. The API will not start, or a class is missing

| What you see | What it means | How to fix it |
| --- | --- | --- |
| Startup (or every test) fails with `System.TypeInitializationException` for `ProblemProvider` and an inner `ArgumentException: An item with the same key has already been added. Key: yourclassname` | `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) files every class under its **lowercase class name**, and two problems (or two solvers, and so on) have the same one. The most common cause is copying a file and forgetting to rename the class. | Rename your class so it is unique, ignoring case. Search the repo for the name you used (for example `grep -rn "class MySolver" Problems`). Tip: a good name includes the problem, such as `SubsetSumBruteForce` rather than `BruteForce`. See [how-the-code-works.md](how-the-code-works.md) section 4. |
| Your new class does not show up in Swagger or the website | The class does not implement the interface, is in the wrong place, or the app was not restarted. | Check the class declaration (`: IProblem<...>`, `: ISolver<...>`, and so on). Restart the API (or use `dotnet watch`). Then call `GET /Navigation/Reductions` or `GET /ProblemProvider/info?interface=<ClassName>` to check. |
| Your reduction is not in `/Navigation/Reductions` | The two type arguments of `IReduction<FROM, TO>` decide where it appears. | Check them (see [adding-a-reduction.md](adding-a-reduction.md)). |

## 3. Build, formatting, and warnings

| What you see | What it means | How to fix it |
| --- | --- | --- |
| The Release build fails with output mentioning `dotnet format`, `Formatted code file`, or `Run 'dotnet format' to fix` | Your code is not formatted. Formatting is part of the Release build ([Directory.Build.targets](../../Directory.Build.targets)). | Run `dotnet format Redux.slnx`, then build again. Commit the files it changed. |
| `error CS...: <something> ... (warning treated as error)` | Warnings are errors ([Directory.Build.props](../../Directory.Build.props)). An unused variable, a possibly-null value, or a missing XML comment fails the build. | Fix the line it points to. Do not turn off the warning setting. |
| `warning CS8618` or `CS8600` family ("non-nullable ... must contain a non-null value", "possible null reference") | C# is asking you to handle "this might be empty". | Give the field a starting value, add `?` to the type, or check for null. The compiler message names the exact line. |
| `CS1591: Missing XML comment for publicly visible type or member` | Public API classes need a `///` comment. | Add a `/// <summary>...</summary>` above it, like the neighbouring code. |
| `CS0246: The type or namespace name 'X' could not be found` | A missing `using`, or your `namespace` does not match your folder. | Add the `using` line. Check the namespace follows the folder: `API.Problems.NPComplete.NPC_<NAME>...`. |
| The build passes locally but fails in CI | You built Debug locally, but CI builds Release (which checks formatting). Or leftover files hide a problem. | Run the same commands as CI: `dotnet build Redux.slnx -c Release`. See [building-and-testing.md](building-and-testing.md). |

## 4. Tests

First, run just the failing test so you do not wait for everything:

```bash
dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~<part of the test name>"
```

| What you see | What it means | How to fix it |
| --- | --- | --- |
| A metadata test fails and names your class, with text such as `Unclassified` or "declared ... Unclassified" | You left a declared metadata member at the `Unclassified` (or empty) value the template put there. These are `complexityClass` and `problemType` (problems), `solverType`, `complexityBucket`, and `complexity` (solvers), `cost`, `reductionType`, `complexityBucket`, and `complexity` (reductions). The template spells out every member you must decide, and these tests fail until you replace each one. That is intentional. | Set a real value. The enum files in [Interfaces/](../../Interfaces/) have comments explaining each choice. If you truly cannot decide, ask in your PR. Do not add yourself to an allowlist in a test file. |
| `NamingConvention_Tests` fails with a message about `solverName`, `verifierName`, `visualizationName`, or `reductionName` | The human-readable name has the wrong shape. A raw class name (`SubsetSumToPartition`) or a placeholder (`Default Reduction`, `TODO`) fails. | Use title-case words such as `"Partition Reduction"`, or a person's name with the kind, such as `"Karp's 3SAT Reduction"`. The message shows the accepted shapes. The task guides say more. |
| A smoke or instantiation test fails with "did not default-construct" | The no-argument constructor throws, usually because `defaultInstance` does not match the parser. | Make `defaultInstance` parse with your own constructor. Check that it follows the grammar in `instanceFormat`. |
| `ReductionSmoke_Tests` fails: the reduction output "does not parse" | `reduce()` produced a string the TO problem cannot read. | In a unit test, pass the output to `new TO("...")`. Fix `reduce()` until that works. See [adding-a-reduction.md](adding-a-reduction.md). |
| `ReductionValidity_Tests` fails | You reduce from a harder class to an easier one, or a problem has the wrong `complexityClass`. | Check both problems' `complexityClass` and the order of FROM and TO. |
| `ParseError_Endpoint_Tests` fails with status 500 | Garbage input crashed your code instead of being rejected. | Make your constructor, verifier, or `mapSolutions` throw a normal exception on bad text so it becomes a 400. See [how-the-code-works.md](how-the-code-works.md) section 8. |
| `visualization-types` test fails (`VisualizationType_Tests`) | Your `visualizationType` is not in [Documentation/visualization-types.json](../visualization-types.json). | Follow [adding-a-visualization.md](adding-a-visualization.md). Do not edit the JSON file casually, because code and the GUI check it. |
| A test fails with "Allowlist ... stale" or "not added to the allowlist" | The metadata ratchet tests compare against short lists in the test files. | Do not edit the lists to hide a failure. Fix your metadata. If the message says an entry is stale because you classified a problem, remove that one line. |
| "No test matches the given testcase filter" | Typo in `--filter`, or tests are not built. | Check the spelling. Leave out `--no-build`. |
| Performance tests fail (`--filter "Category=Performance"`) | Something is too slow, not wrong. | Run again once. If it still fails, look at what you added (a solver on the default instance, or `reduce()`). |
| A test passes alone but fails with the others | Shared state, such as a static variable changed by one test. | Avoid `static` fields that tests change. Build a fresh object in each test. |

## 5. Bad input and the 400 errors

| What you see | What it means | How to fix it |
| --- | --- | --- |
| API response: HTTP 400 with `"error": "instance_parse_error"` (a `ProblemParseException`) | The instance string you sent does not fit the problem's format. | Read the `instanceFormat` quoted in the response, and copy its example. Instances are strict about braces, commas, and spaces. As a developer: check that your constructor really reads the text in `instanceFormat`. |
| HTTP 400 with `certificate_parse_error` (a `CertificateParseException`) | The certificate (proposed answer) does not fit `certificateFormat`. | Compare with the `certificateFormat` in the response. As a developer: throw this (or let a normal parse exception escape) for unreadable text, and return `false` only when the text parsed but is not a solution. |
| HTTP 400 with `reduction_input_parse_error` | A reduction could not read its input instance, or the solution given to `mapSolution`. | Check the FROM problem's `instanceFormat` or `certificateFormat`. |
| HTTP 500 | A real bug in the algorithm, or bad input that was not turned into a parse error. | Read the server log for the stack trace. If the input was bad text, make the parsing step throw a normal exception so it turns into a 400. |
| SPADE says `Braces not matched` or similar | SPADE's message for text that does not match the grammar. | Same as a 400. The text is wrong, or the grammar does not describe your format. See [how-the-code-works.md](how-the-code-works.md) section 7. |

## 6. Pull requests and CI

| What you see | What it means | How to fix it |
| --- | --- | --- |
| The **dotnet build** check is red | The build, formatting, a test, or a performance test failed. This one blocks merging. | Open the check's **Details**, read the last 30 lines of the failed step, and match them against sections 3 and 4 of this page. |
| The **rbs** check is green, but you suspect problems | rbs is "soft" on Redux, so it is **always green**, even when gates inside it failed. | Read the rbs report: the bot comment on your PR, or the job summary on the Actions page. Fix every red `format-check` and `lint` line. See [building-and-testing.md](building-and-testing.md) section 4. |
| rbs report: `unit-test` is red, mentioning coverage | `rbs.toml` asks for 80% coverage ([rbs.toml](../../rbs.toml)), and the project is below that (about 61% at the time of writing). | Expected, and not your fault. Add tests for your own code so you do not lower it. |
| rbs report: `audit` is red | A package has a known security problem. | Mention it in your PR. Only deal with it yourself if you added that package. |
| rbs report: `build`, `integration-test`, or `push` is red | The Docker side of the pipeline. | Rarely your fault. Ask a maintainer. |
| CodeQL shows alerts | GitHub's security scanner found something. | Open the PR's **Code scanning** results and read the alert. |
| Your branch has merge conflicts | The main repo changed the same lines. | `git fetch upstream`, then `git merge upstream/CSharpAPI`, fix the marked files, build, test, and push. |
| The PR targets the wrong branch | PRs go to `CSharpAPI`. There is no `develop` branch. | On GitHub, click **Edit** next to the PR title and change the base branch to `CSharpAPI`. |

## 7. The website (Redux_GUI)

Redux_GUI is a separate repo. Its troubleshooting notes are in its [README](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/README.md) and [TESTING.md](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/TESTING.md).

| What you see | What it means | How to fix it |
| --- | --- | --- |
| `npm run check:visualizations` fails (the "visualization coverage" check) | `components/Visualization/svgs/visualizationTypes.json` does not match the renderer keys in `components/Visualization/svgs/Visualizations.js`. It happens when you add or rename a visualization type. | Update both files so they agree. Also see [adding-a-visualization.md](adding-a-visualization.md) for the API side. |
| GUI rbs shows `unit-test` and `typecheck` as `skipped` | The repo has no `test` script and no `tsconfig.json`. | Nothing. Skipped is fine. |
| `npm run test:e2e` cannot connect | The Playwright tests need the API running. | Start the API on port 27000 and set `REDUX_BASE_URL`. |

## Still stuck?

1. Re-read the guide for your task: [guides index](README.md).
2. Run only the failing command, and copy the **first** error (later errors are often just fallout from the first).
3. Ask in the PR, in an issue, or on the project Discord (link in the [Readme](../../Readme.md)). Include the command you ran, the folder you ran it from, and the full error text.
4. Do not delete or weaken a test just to turn it green.
