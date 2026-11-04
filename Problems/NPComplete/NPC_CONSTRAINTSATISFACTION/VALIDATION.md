# Validation results

Validation ran on the authorized `feature/llm-csp-solver-verifier` checkout, starting at `9654cc72c5c146f72b691324378d75f4a1aed950`. No other branches, history, previous commits, PRs, or diffs were inspected.

- `dotnet build Redux.slnx --no-restore -v quiet`: succeeded, 0 warnings and 0 errors.
- New solver/verifier tests: 83 passed, including independent exhaustive-oracle checks, live Redux timer cancellation, valid/invalid/malformed certificates, unsupported expression errors, constant constraints, empty variable sets, integer boundaries, and satisfiable/unsatisfiable instances.
- Final `dotnet test redux-tests/redux-tests.csproj --no-restore`: 4,392 passed, 25 failed, 0 skipped (4,417 total).

Remaining failures, with existing tests and unrelated code left unchanged:

| Existing test | Count | Observed cause |
| --- | ---: | --- |
| `CONSTRAINTSATISFACTION_Tests.MalformedInstance_ThrowsProblemParseException` | 19 | Tests expect `ProblemName` to be `Constraint Satisfaction Problem`; the unchanged problem class reports `General Finite-Domain Constraint Satisfaction Problem`. The expected exception is thrown. |
| `SolverTimeLimit_Tests.EverySolver_StopsWhenTheTimeLimitHasPassed` | 3 | `WeightedMaxSatBranchAndBoundSolver`, `WeightedMaxSatBruteForceSolver`, and `WeightedMaxSatConditionalExpectationsSolver` return `{}` instead of the timeout message. |
| `ContributorProfile_Tests.AllDeclaredContributors_MatchAContributorInfoJsonEntry` | 1 | The existing CSP problem's three credited contributors have no matching `contributorInfo.json` entries. |
| `VisualizationType_Tests.NoNewUndeclared` | 1 | Existing `ConstraintSatisfactionVisualization` remains unimplemented. |
| `Navigation_Endpoint_Tests.NpcProblems_MembershipIsExactlyDeclaredNPComplete` | 1 | The expected membership list omits the existing `CONSTRAINTSATISFACTION` problem. |

These results describe this checkout after implementation; no separate historical/baseline test run was performed. All original tests are preserved. New tests are in `redux-tests/Problems/NPC_CONSTRAINTSATISFACTION/CspSolverVerifierTests.cs`.
