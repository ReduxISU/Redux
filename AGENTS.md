# Instructions for AI coding agents

Redux is a C# (.NET 10) API for NP-complete problems and the reductions between them. The sibling frontend is the separate Redux_GUI repo.

**Source of truth: [Documentation/guides/README.md](Documentation/guides/README.md).** Read the guide for your task before writing code (setup, how-the-code-works, building-and-testing, troubleshooting, and adding a problem, solver, verifier, reduction, or visualization). This file only lists the traps. Do not copy the guides here.

## Commands (run from the repo root)

- Always name the solution file. A bare `dotnet build` or `dotnet test` fails with `MSB1011` because `API.csproj` and `Redux.slnx` sit side by side. (`dotnet run` is fine.)
- Format first: `dotnet format Redux.slnx`. The Release build runs `dotnet format --verify-no-changes` and fails on bad formatting. Warnings are errors too.
- Build: `dotnet build Redux.slnx -c Release`
- Tests: `dotnet test Redux.slnx -c Release --filter "Category!=Performance"`
- Performance tests are a separate step: `dotnet test Redux.slnx -c Release --filter "Category=Performance"`
- One test: `dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~<TestName>"`
- Run the API: `dotnet run` (port 27000, Swagger at http://127.0.0.1:27000/swagger/index.html). Stop any server you start.

## Conventions

- Problems live in `Problems/NPComplete/NPC_<NAME>/` (also `NPHard/NPH_<NAME>/`, `P/P_<NAME>/`): `<NAME>_Class.cs`, `Solvers/`, `Verifiers/`, `Visualizations/`, and `ReduceTo/NPC_<TO>/` for reductions away from that problem.
- Tests go in `redux-tests/Problems/NPC_<NAME>/<NAME>_Tests.cs`. `redux-tests/Metadata/` tests run automatically against every class.
- Do not write controllers. `AdditionalControllers/ProblemProvider.cs` finds every class by reflection and keys it by lowercase class name, so class names must be unique ignoring case within a kind, or the API will not start.
- Declared metadata (`complexityClass`, `problemType`, `solverType`, `cost`, `reductionType`, `complexityBucket`, and so on) must be set by reasoning about the code, never left `Unclassified`. Do not edit test allowlists to make a failure go away.
- Throw `ProblemParseException` or `CertificateParseException` (`Interfaces/ParseExceptions.cs`) on bad input so the API answers 400, not 500.
- Do not change `AdditionalControllers/Navigation/` routes or JSON shapes, and do not touch `Documentation/visualization-types.json`, without being asked. Redux_GUI and CI depend on them.

## Pull requests

- PRs target `CSharpAPI`. There is no `develop` branch.
- The rbs check on a PR is soft and always shows green. Read its report (the bot comment, or the job summary) and fix any red `format-check` or `lint` gate. `unit-test` also enforces 80% coverage (currently about 85%), so add tests for the code you write.
- Do not weaken or delete a test to turn a build green.
