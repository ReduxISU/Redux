# Redux API error-code overhaul — handoff (issue #996)

Anything marked *inferred* is standard ASP.NET behaviour deduced from configuration, not
observed.
**Repo:** `Redux`, branch `996-enhance-error-codes` (cut from `origin/CSharpAPI` tip
`01a3b0bd`, the merge of #934). PRs target `CSharpAPI` and need one approving review.

---

## 0. The one-paragraph version

Redux emits 15 error codes today in four unrelated body shapes, built in a dozen places; several
failure paths still fall through to raw 500s or to 200s carrying an error string, and there is no global
exception handler, no logging, and no trace id. Issue #933 (yours) already proposes the right contract,
and #934 / #943 shipped two codes in it by hand; #992 and #994 are about to hand-roll three more. This
work turns that into **one catalog of codes with categories, one builder, one exception handler, a
complete taxonomy, tests that enforce the shape for every code, and docs** — delivered as about nine
small PRs, each reviewable on its own. Motivation (#996): mcpredux hands Redux's error body to the model
verbatim, so every error must say what went wrong and what to do next, in a shape an agent can rely on.

---

## 1. Background you need

### 1.1 What Redux is, in error terms
ASP.NET Core / .NET 10 REST API. One main controller, `AdditionalControllers/ProblemProvider.cs`
(`/ProblemProvider/{verify,solve,info,problemInstance,visualize,visualizeReduction,reduce,mapSolution,
mapSolutionBack,gadgets}`), plus `AdditionalControllers/Navigation/*` (catalog lookups),
`Interfaces/Tools/ProblemInstanceGenerators.cs` (`/ProblemGenerator/*`), `ProblemTemplate/ProblemTemplate.cs`,
and a minimal-API `/health` in `Program.cs`. ~51 problem classes, 52 verifiers, 85 solvers, 50
visualizations, 20 reductions under `Problems/**`, discovered by reflection into the dictionaries
`ProblemProvider.Problems/Verifiers/Solvers/Visualizers/Reductions` (keyed by lowercased class name).

### 1.2 Consumers of the errors (the compatibility constraints)
- **mcpredux** (`~/projects/ISU/mcpredux`, Python MCP server, prod port 27200). `server.py:32-48`: on any
  non-2xx it raises `ToolError(r.text)` — the raw body goes to the model, it never parses `error`. A 200
  whose body is an error string passes through as success. Its `tests/test_errors.py` and `TODO.md §4`
  pin a code `input_shape_mismatch` and a field `expected_example` that **Redux has never emitted**
  (fake backend only). Redux really answers `reduction_input_parse_error` there.
- **Redux_GUI** (`~/projects/ISU/Redux_GUI`, Next.js). `components/redux/index.js`: `fetchJson`/
  `fetchPostJson` return `undefined` on non-OK without reading the body. Two places do read bodies:
  `describeVerifyError` (`error === "certificate_parse_error"`, `expected`, `detail`, fallback
  `message ?? detail ?? title ?? error`) and, since PR #406, `describeSolveError` (`message`, `hint`,
  shown in `SolveRowReact.js`). The proxy `pages/api/redux/[...path].js` passes status and body through.
  Playwright e2e: `tests/e2e/fixtures.js:34-65` pins known failures at **exactly 400** for
  problemInstance/solve/gadgets/reduce and fails any other ≥400; `app.spec.js:171-195` pins 400 +
  `certificate_parse_error` + the text "Expected:".
- **Redux_Frontend** (not cloned locally) posts to the same endpoints; assume it reads nothing beyond
  status.
- **Redux_VR** throws on non-OK and never reads the body.

### 1.3 Related issues and PRs
| Ref | What | State |
|---|---|---|
| #996 | this work ("enhance error codes … for AI agentic workflows") | open, Ryan |
| #933 | one error contract (message, hint, expected_example, options, global handler, one helper, sweep test, docs) | open, Trosper3 — this plan implements it |
| #577 | parse failures crashed as 500 on 39/47 problems; fixed via `ParseGuard` | closed |
| #625 / #934 | `solver_unavailable` 503, first code in the #933 shape | merged |
| #935 / #943 | `time_limit` 422, second code in the shape | merged |
| #941 | Weighted Max-SAT; added `exact_search_limit_exceeded` 400 (no message/hint) | merged |
| #992 | 429 `server_busy` + `Retry-After` (`Interfaces/ComputeJobs.cs`, `ComputeJobLimiter.ServerBusyBody()`) | open, Trosper3 |
| #994 | 422 `memory_limit`, 500 `worker_failed`; rewrites `solve()` (+92/−31 in ProblemProvider.cs) | open, stacked on #992 |
| mcpredux `TODO.md §4`, `tests/test_errors.py` | the never-emitted `input_shape_mismatch` contract | to rewrite |
| Redux_GUI #406 | shows `message` + `hint` in the Solve row | merged |

**You own both sides of the sequencing problem.** Land the catalog/builder first (step 1 below), then
switch your own `ServerBusyBody`, `MemoryLimitBody` and `WorkerFailedBody` in #992/#994 to it before
they merge. Steps 4–5 (controller-wide migration) should wait until #994 is in, because #994 rewrites
`solve()` and the two would conflict badly.

### 1.4 Working conventions (Ryan's)
- One commit-sized step per PR, then stop for review. Do not build several steps ahead.
- Keep formatting-only commits separate from logic commits.
- Low cyclomatic complexity, DRY, minimal comments/docstrings; no meta-comments about the change.
- `Directory.Build.props` sets `TreatWarningsAsErrors`; `Directory.Build.targets` runs
  `dotnet format --verify-no-changes` on Release/CI builds. Unformatted code fails the build.
- Tests: xunit v3, `WebApplicationFactory<Program>` via `redux-tests/Endpoints/EndpointFixture.cs`
  (`AppFactory`); theories built from the `ProblemProvider.*` registries; config overrides via a subclass
  that calls `AddInMemoryCollection` (see `TimeLimit_Endpoint_Tests.cs:14-22`). Coverage floor 80%
  (`rbs.toml`).
- Build/test in the devcontainer (`.devcontainer/devcontainer.json`, `mcr.microsoft.com/dotnet/sdk:10.0`):
  ```bash
  cd ~/projects/ISU/Redux
  devcontainer up --workspace-folder . && devcontainer exec --workspace-folder . bash
  dotnet build Redux.slnx -c Release          # format + warnings-as-errors
  dotnet test Redux.slnx --filter FullyQualifiedName~ApiError
  rbs unit-test                                # what CI runs (coverage 80%)
  rbs build && rbs integration-test            # container + integration/ suite
  ```
  CI is `.github/workflows/rbs.yml` → the hub's reusable `ci.yml`; read results from the `rbs-report`
  artifact (`gh run download <id>`). Redux's gate is still `soft: true` until #879 merges, so a red gate
  does not block the PR — check it anyway.

---

## 2. Inventory: what the code does today

### 2.1 Every `error` code the API emits (15)
PP = `AdditionalControllers/ProblemProvider.cs`, CL = `Interfaces/ComputeLimits.cs`,
SUE = `Interfaces/SolverUnavailableException.cs`, PE = `Interfaces/ParseExceptions.cs`.

| `error` | HTTP | Body fields (order) | Emitted by |
|---|---|---|---|
| `instance_parse_error` | 400 | `error, problem, expected, received, detail` | verify PP:256-258; problemInstance PP:404-410 (own catches); solve/visualize/visualizeReduction/reduce/mapSolution/mapSolutionBack/gadgets via `ParseError` PP:305-313 |
| `certificate_parse_error` | 400 | same | verify PP:259-261 |
| `reduction_input_parse_error` | 400 | `error, reduction, problem, expected, received, detail` | `ReductionParseErrorBody` PP:694-703; covers source instance, forward certificate and target certificate with no field saying which |
| `no_verifier` | 400 | `error, received, detail` | verify PP:262-263 (`NoVerifierException`, `Interfaces/DummyClasses/DummyVerifier.cs:32`) |
| `exact_search_limit_exceeded` | 400 | `error, variableCount, maxVariables, detail` | PP:294-302; caught only on verify/solve/visualize; no `problem` though PE:56 carries it |
| `unknown_solver` | 400 | `error, received` | solve PP:342, visualize PP:526 |
| `unknown_interface` | 400 | `error, received` | info PP:379 |
| `unknown_visualization` | 400 | `error, received` | visualize PP:514 |
| `unknown_format` | 400 | `error, received` | visualize PP:522 |
| `solver_mismatch` | 400 | `error, received, visualization` | visualize PP:528 |
| `unknown_reduction` | 400 | `error, received` | PP:577, 618, 645, 678, 716 |
| `no_reductions_provided` | 400 | `error` | PP:601 — unreachable (`Split("-")` always yields one element) |
| `no_backward_map` | 400 | `error, reduction` | mapSolutionBack PP:682 |
| `time_limit` | 422 | `error, message, detail, limitSeconds, <solver\|verifier\|reduction, problem, options>, hint` | `ComputeLimits.TimeLimitBody` CL:67-80 (a Dictionary; writes `"problem": null`); `SolveTimeLimitError` PP:60-75; `ReductionTimeLimitError` PP:78-80 |
| `solver_unavailable` | 503 | `error, message, solver, problem, detail, hint, options` | `SolverUnavailableException.Body()` SUE:46-62 via `SolverUnavailableFilter` SUE:98-104, registered `Program.cs:17` |

**Bodies without a code:** `ContributerProfile.cs:64,90,121,148` → 400 `{error: ex.Message}` (free text
in `error`); `:77,103,135` → 404 `{message}`. `Nav_Solvers.cs:84` and `Nav_Verifiers.cs:49` → **200**
with the JSON string `"entered a solver/verifier that does not exist"`. Framework
`ValidationProblemDetails {type,title,status,errors,traceId}` on a missing/unbindable parameter
(*inferred*; `[ApiController]` + `<Nullable>enable`). 415 on a non-JSON body (*inferred*). Empty-body
404/405 on an unmatched route or wrong verb.

### 2.2 Raw 500s still reachable
- Unknown verifier on `/verify`: `Verifiers[name.ToLower()]` PP:172 inside the backstop lambda PP:247,
  no `TryGetValue`.
- Unknown problem on `/problemInstance`: `Problems[name.ToLower()]` PP:164.
- Constructors outside `try`: PP:381 (info), PP:523 and PP:530 (visualize).
- `SipserReduceToSAT3()` default ctor runs `this(new CLIQUE())` on a non-Sipser default instance and
  throws (`Problems/NPComplete/NPC_CLIQUE/ReduceTo/NPC_SAT3/SipserReduceToSAT3.cs:71,78`), so
  `info?interface=SipserReduceToSAT3` and any `Activator.CreateInstance` on it 500 (root of the
  `/gadgets` 500 noted in `REDUX_VR.md:243`).
- `/ProblemGenerator/*` has no parameter validation (`ProblemInstanceGenerators.cs:115-167`; e.g.
  `Sat3?n=0&c=1` indexes an empty list).
- `/ProblemTemplate*` → `FileNotFoundException` when a template is missing.
- `Nav_Batch.cs:44-73`: `Lazy<string>` caches a thrown exception forever (default
  `ExecutionAndPublication`), so one bad type breaks every later `Batch/all*` call.
- `PartitionDefaultVisualization.cs:29-37` indexes `groups[0]` on the `"{}"` that
  `PartitionBruteForce.cs:72` returns when there is no partition.
- UNSTRUCTUREDSEARCH with a non-power-of-2 size → `ArithmeticException` in
  `UnstructuredGroverSolver.cs:34` before any service call.
- `Reductions/path` (Nav_Reductions.cs:553) runs a solver in-process with no guard.

### 2.3 Field semantics that disagree
- `received` = a *name* in `unknown_*`, `solver_mismatch`, `no_verifier`; the *payload text* in parse errors.
- `reduction` = friendly `reductionName` in `reduction_input_parse_error` (PP:697); the raw query string in
  `no_backward_map` (PP:683) and `time_limit` (PP:80); in `unknown_reduction` the name is in `received`.
- `solver` = class name in `time_limit`/`solver_unavailable`; raw input (as `received`) in `unknown_solver`.
- `problem` = `"CLIQUE"` when `CLIQUE_Class.cs:76` throws, `"Clique"` when `ParseGuard` wraps
  (`CLIQUE_Class.cs:12`); same for VERTEXCOVER. `LookupInstanceFormat` PP:315-330 resolves either, O(N).
- `detail` = exception message everywhere except `time_limit` (two fixed constants CL:86, CL:89).
- `options` sort: `time_limit` uses `Ordinal` over `ProblemTypeOf(Solvers)` (PP:64); `solver_unavailable`
  uses `OrdinalIgnoreCase` over `SolverNavigationData` (`InterfaceNavigationData.cs:49`). `hint`:
  `time_limit` names all other solvers, `solver_unavailable` only non-quantum ones.
- `error` is free text in ContributorProfile; `message` is the only field in its 404s.

### 2.4 Status inconsistencies
- `time_limit` 422 vs `exact_search_limit_exceeded` 400, both limits.
- unknown solver 400 vs unknown verifier/problem 500 vs Navigation unknowns 200 (string, `[]`, `{}`, `found:false`).
- `DummyVerifier` → 400 `no_verifier`; `DummyVisualization` → 200 `[]`.
- 11/20 reductions' `mapSolutions` is a stub returning `""` → `/mapSolution` 200 `""`
  (`KarpSATToSAT3.cs:218`, `KarpReduceSAT.cs:171`, `GraphColoringToCliqueCover.cs:100`,
  `KarpGraphColorToExactCover.cs:155`, `KarpVertexCoverToSetCover.cs:115`, `KarpVertexCoverToNodeSet.cs:102`,
  `reduceToEXACTCOVER.cs:97`, `WEIGHTEDCUTReduction.cs:114`, `FengReduction.cs:100`,
  `KarpDirectedHamiltonianToUndirectedHamiltonian.cs:111`, `KarpExactCoverToSubsetSum.cs:110`); but a
  missing backward map is 400 `no_backward_map`.
- A quantum service that *hangs* answers 422 `time_limit` (backstop at 10 s + 2 s, CL:31/PP:53) before the
  30 s HttpClient timeout (`Program.cs:14`) can produce 503.
- Catch styles: verify uses direct typed catches; problemInstance has its own
  `TargetInvocationException` branch (redundant — `ParseGuard.CreateProblem` always unwraps, PE:82-85);
  everything else uses `IsParseError`/`ParseError`.

### 2.5 Global handling (`Program.cs`)
Absent: `UseExceptionHandler`, `UseDeveloperExceptionPage`, `AddProblemDetails`, `UseStatusCodePages`,
`IExceptionHandler`, `ConfigureApiBehaviorOptions`, any `IsDevelopment` branch, any `ILogger` (repo-wide:
none; two live `Console.WriteLine`s, `BERNSTEINVAZIRANI_Class.cs:76` and `HeuristicSolver.cs:68`). No
`TraceIdentifier` anywhere. Only filter: `SolverUnavailableFilter` (MVC `IExceptionFilter`, action
execution only). Development (also what `WebApplicationFactory` runs): unhandled → HTML/plain-text
developer page; TestServer rethrows instead (`ParseError_Endpoint_Tests.cs:10-12`). Production (Dockerfile
sets no `ASPNETCORE_ENVIRONMENT`): 500 with empty body, and the hand-set
`Access-Control-Allow-Origin` (`Program.cs:55-58`, set before `next()`) is wiped by the response reset, so
a browser reports a CORS failure instead of a 500 (*inferred*).

### 2.6 Leaf layer (below the controllers) — becomes follow-up issues, not this work
- `ProblemParseException` is thrown directly by 15/51 problems, `CertificateParseException` by 10/52
  verifiers, `ReductionInputException` by 4/20 reductions; everyone else relies on `ParseGuard`
  (`CreateProblem` called from `SolverInterface.cs:186,194,214`, `VerifierInterface.cs:27`,
  `VisualizationInterface.cs:51,60,69`, `GraphSubsetFrames.cs:322`, PP:164,168,197; `VerifyCertificate`
  from `VerifierInterface.cs:28`). Reductions have no guard in `Interfaces`; the controller re-parses the
  source to classify (PP:188-203).
- **20 verifiers catch and return `false`** on any exception (ConvexHull `:37`, BinPacking `:72`, SCC `:38`,
  TopologicalSort `:34`, LosslessDataCompression `:49`, Knapsack `:45`, DominatingSet `:87`, SPSP `:63`,
  WeightedCut `:58`, CliqueCover `:43,60`, SteinerTree `:96`, GraphColoring `:90`, SetCover `:51`, SSSP `:45`,
  Partition `:41,53`, Cut `:47`, Sudoku `:109-112`, GenericVerifier01INTP `:44,46`, NQueens `:33`,
  MinCut `:26,42`), so a malformed certificate is 200 `"False"`, contrary to `adding-a-verifier.md:133-179`.
- **Solvers return sentinels as answers:** `"{}"` (~60, by design #605); `""` (BinPacking, HittingSet,
  Knapsack, ExactCoverRecursive, DummySolver); `"()"` (IntegerProgramming, JobSeq); prose
  `"No solution exists"` (`SATBruteForceSolver.cs:69`), `"No Solution"` (`Sat3BacktrackingSolver.cs:37`),
  `"no solution"` (`UnstructuredSearchSolver.cs:35`), `"No Solution Exists…"` (`NFASolver.cs:120`,
  `DFASolver.cs:64,86,98`); invalid-instance prose `"Breaks DJ Promise"` (`DeutschJozsaClassicalSolver.cs:55`),
  `"invalid func…"` (`SimonSolver.cs:46,54`). Verifiers disagree on them: SAT/SAT3 verifiers throw
  `CertificateParseException` on `"No Solution"`, VC/Clique reject `"{}"`.
- `KarpReduceGRAPHCOLORING.cs:312` and `KarpIntProgStandard.cs:122` throw `ReductionInputException` for
  "assignment does not satisfy the formula" — a semantic failure labelled as a parse failure.
- **8 quantum visualizations bypass `QuantumServerAPI.Ask`** and swallow failures into an empty or static
  circuit (200): `SATGroverVisualization.cs:53`, `DeutschD3Visualization.cs:50`,
  `DeutschDefaultVisualization.cs:54`, `DeutschJozsaD3Visualization.cs:50`,
  `DeutschJozsaDefaultVisualization.cs:53`, `BernsteinVaziraniD3Visualization.cs:51`,
  `BernsteinVaziraniDefaultVisualization.cs:53`, `ShorsDefaultVisualization.cs:54`.
- `QuantumServerAPI.Ask` (`Tools/QuantumServerAPI.cs:94-104`) collapses not-configured / unreachable /
  non-2xx / timeout / bad JSON / missing answer into one exception distinguished only by `detail` text; the
  timeout message hard-codes "30 seconds".
- `ParseGuard.VerifyCertificate` wraps the *whole* `verify()` (parsing and checking), so a genuine bug
  (NRE, IndexOutOfRange) in checking logic becomes a 400 (acknowledged at PE:101-107).
- Cause chains are mostly dropped: most direct throws pass `ex.Message` without the inner exception (only
  `SAT_Class.cs:101`, `SAT3_Class.cs:84`, `WEIGHTEDMAXSAT_Class.cs:64` keep it).
- `ReductionBack.Accepts` (`Interfaces/ReductionBack.cs:21-27`) swallows every exception, including
  `NoVerifierException`.
- `ProblemTemplate/Templates/Tests/PROBLEM_Tests.txt:42,55` test the constructor and typed `verify`
  directly, bypassing the guard — so generated tests still require the leaf class to throw.

### 2.7 Tests that pin today's behaviour (what you may not change silently)
- `time_limit`: exact `message` "The {job} ran past the {s}-second time limit, so it was stopped."; `hint`
  starts "Try a smaller instance"; `limitSeconds` numeric; context keys `solver`/`verifier`/`reduction`;
  `options` without the solver (`TimeLimit_Endpoint_Tests.cs:58-110`).
- `solver_unavailable`: `error`, `solver`, `problem`; exact message template; exact Shor hint/options;
  `options` equal to the navigation list minus the solver, in order (`SolverUnavailable_Endpoint_Tests.cs:74-234`).
- parse errors: `ParseError_Endpoint_Tests.cs:78-86` is the only full-shape check (`error, problem,
  expected, received, detail`, `received=="garbage"`); `MapSolutionBack_Tests.cs:333-347` pins `received`,
  `expected == target certificateFormat`, `problem == target name`.
- `unknown_format.received == "xml"`; `solver_mismatch` `error/received/visualization`
  (`Visualize_Endpoint_Tests.cs:181-235`); `no_backward_map.reduction == "KarpVertexCoverToNodeSet"`
  (class name) (`MapSolutionBack_Tests.cs:369-376`); `no_verifier` 400 (`DefaultFor_Tests.cs:336-343`).
- `exact_search_limit_exceeded`: `variableCount==21`, `maxVariables==20`, `detail` contains both
  (`WeightedMaxSat_Endpoint_Tests.cs:104-114`); status asserted as 400 — **this changes** (see §3).
- Navigation 200 strings: `Navigation_Endpoint_Tests.cs:156-165, 242-250` — **this changes** (step 8).
- Nothing pins: `reduction_input_parse_error.reduction`'s value, `options` order on `time_limit`, key
  order, ProblemDetails bodies, ContributorProfile bodies.
- The sweep `ParseError_Endpoint_Tests.cs:88-194` (every endpoint × 4 garbage strings) asserts only
  "not 500". Upgrade it (step 5).

### 2.8 Docs that describe errors today
`Documentation/guides/troubleshooting.md` §5 (`:72-81`; says the response quotes `instanceFormat`, the
field is actually `expected`; lists only 4 codes), `how-the-code-works.md` §8 (`:138-150`),
`adding-a-solver.md:127,160-178`, `adding-a-verifier.md:65,133-179,305-314`, `adding-a-problem.md`,
`adding-a-reduction.md:146`, `adding-a-visualization.md:207`, `ProblemTemplate/Templates/README.md:31,59,85,104`,
`Tools/QUANTUM_API_README.md:162-185` (full `solver_unavailable` example citing #933). `Readme.md`,
`CONTRIBUTING.md` and `production.md` say nothing about errors. Stale notes elsewhere:
`~/projects/ISU/CLAUDE.md:183-188` still says quantum failures are 200 + error string (now 503).

---

## 3. The design

### 3.1 Decisions already taken (with Ryan, 2026-11-04) — don't reopen
1. **Envelope:** keep the flat #933 shape (`error` + fields), `application/json`. **Not** RFC 9457
   `application/problem+json`: every consumer and test keys on top-level `error`, `type`/`title` URIs add
   nothing an agent can't get from `error` + the docs page, and #992/#994 already build this shape. Add
   `status`, `category`, `retryable`, `traceId`, `field`, `expected_example`. Make the framework's own
   400/404/405/415 bodies conform so there is exactly one shape.
2. **Compatibility:** additive first; then one coordinated step per wrong status (200-strings, raw 500s,
   the 400/422 split), with the Redux test updates in the same PR and follow-up issues for Redux_GUI and
   mcpredux. Not "strictly additive", not "v2 with renames".
3. **Sequencing:** foundation (steps 1–3) lands now; you adopt the builder in #992/#994 before they merge;
   controller-wide migration (steps 4–5) follows #994.
4. **Scope:** API layer. Leaf-layer problems (§2.6) become separate issues with this taxonomy as spec,
   sized for student contributors.

### 3.2 Taxonomy
Codes stay flat `snake_case` strings in `error`. `category` and `retryable` come from the catalog entry,
never typed at a call site.

| `category` | Status | Meaning (one line, goes in the docs) | Codes |
|---|---|---|---|
| `input` | 400 | Something the caller sent is wrong; fix it and resend. Never retry unchanged. | `instance_parse_error`, `certificate_parse_error`, `reduction_input_parse_error`, `unknown_format`, `solver_mismatch`; **new** `invalid_request` (framework model-state), `invalid_parameter` (generators), `invalid_instance` (parsed but violates a rule: non-power-of-2 size, broken DJ promise), `invalid_certificate` (parsed but unusable) |
| `not_found` | 400 for names in the query; 404 for routes | The name or route does not exist; `options` lists what does. | `unknown_solver`, `unknown_interface`, `unknown_visualization`, `unknown_reduction`; **new** `unknown_verifier`, `unknown_problem`, `route_not_found` (404), `method_not_allowed` (405), `unsupported_media_type` (415) |
| `not_implemented` | 400 (pinned) / 501 | Redux has a placeholder here, not an implementation. | `no_verifier`, `no_backward_map` (stay 400); **new** `not_implemented` 501, mapped from `NotImplementedException` (what the templates throw for unfinished methods) |
| `limit` | 422 | The input parsed, but the job was refused or stopped by a configured limit. Try a smaller instance or another solver. | `time_limit`, `memory_limit` (#994), **`exact_search_limit_exceeded` moves 400→422** — the one deliberate status flip; it is the same situation as `time_limit` and 422 is the textbook status. One-line revert in the catalog if it bites. |
| `dependency` | 503 | A service outside Redux failed; another solver, or the same one later, can still answer. | `solver_unavailable` |
| `busy` | 429 | Every compute slot is taken; `Retry-After` says when to retry. | `server_busy` (#992) |
| `internal` | 500 | A Redux defect; report `traceId`. | **new** `internal_error`, `worker_failed` (#994) |

`retryable = true` only for `busy`, `dependency`, `internal`. `unknown_*` stay **400**: the name is a query
argument, not a resource path, and the GUI e2e fixture pins 400. Delete `no_reductions_provided`.

### 3.3 Canonical body (fixed key order; null values omitted)
| Field | When | Semantics |
|---|---|---|
| `error` | always | catalog code |
| `status` | always (new) | the HTTP status, repeated |
| `category` | always (new) | from §3.2 |
| `retryable` | always (new) | boolean |
| `message` | always | one plain sentence a person or a model can read. The two pinned templates (`time_limit`, `solver_unavailable`) are kept verbatim |
| `detail` | optional | technical reason: exception type + message, never a stack trace |
| `problem` | when known | the problem's friendly `problemName`, always resolved through the catalog (fixes `"CLIQUE"` vs `"Clique"`) |
| `solver` / `verifier` / `visualization` / `reduction` | when known | **class name** (the key the endpoints take), everywhere. The only value change this causes: `reduction_input_parse_error.reduction` friendly→class, which nothing pins |
| `field` | with `received` (new) | which request input was rejected: `solver`, `verifier`, `visualization`, `reduction`, `problem`, `interface`, `format`, `instance`, `certificate`, `solution`. Makes `received` mean "the exact text you sent in `field`" everywhere, and tells `reduction_input_parse_error` callers which of its three inputs failed |
| `received` | with `field` | the rejected text, unchanged |
| `expected` | input errors | `instanceFormat` / `certificateFormat` from the catalog (as today) |
| `expected_example` | input errors (new) | `defaultInstance` for instances; the default verifier's `certificate` property for certificates (omit when it is `DummyVerifier`); the first of `options` for names. This is the field mcpredux already expects |
| `hint` | every 4xx | what to do next. Catalog `DefaultHint` when constant; built per site when it needs context (`time_limit`, `solver_unavailable`: unchanged text) |
| `options` | names / alternatives | valid choices, from the navigation catalogs (`SolverNavigationData`, `VerifierNavigationData`, `VisualizationNavigationData`, `ProblemNavigationData`, `ProblemProvider.Reductions.Keys`) in navigation order — one source replaces the two sorts. Full list for unknown names (~150 solvers ≈ 4 KB is fine; a did-you-mean filter can come later) |
| code-specific | | `limitSeconds`, `variableCount`, `maxVariables`, `maxConcurrentJobs`, `retryAfterSeconds`, `errors` — names and values unchanged |
| `traceId` | always (new) | `HttpContext.TraceIdentifier` |

Example (unknown solver, after step 4):
```json
{
  "error": "unknown_solver", "status": 400, "category": "not_found", "retryable": false,
  "message": "No solver is named 'Sat3Backtrack'.",
  "field": "solver", "received": "Sat3Backtrack",
  "expected_example": "Sat3BacktrackingSolver",
  "hint": "Use one of the solver class names in options; Navigation/Batch/allSolvers lists them per problem.",
  "options": ["Sat3BacktrackingSolver", "SATGroverSolver", "..."],
  "traceId": "0HN7..."
}
```

### 3.4 Architecture
**New `Interfaces/ApiErrors.cs`** (namespace `API.Interfaces`, beside `ParseExceptions.cs`/`ComputeLimits.cs`):
- `enum ErrorCategory { Input, NotFound, NotImplemented, Limit, Dependency, Busy, Internal }`.
- `sealed record ErrorCode(string Code, int Status, ErrorCategory Category, bool Retryable, string Summary, string? DefaultHint = null)`.
- `static class ErrorCodes`: one `public static readonly ErrorCode` per code in §3.2 (include `ServerBusy`,
  `MemoryLimit`, `WorkerFailed` now so #992/#994 can adopt them) and `All`, built once by reflecting the
  static fields. A code not in `All` cannot be emitted; the sweep test and the docs test iterate it.
- `sealed class ApiError` — **the single builder**: `ApiError.Of(ErrorCode)`, then chainable
  `.Message(string)`, `.Detail(string?)`, `.Hint(string?)`, `.Received(string value, string field)`,
  `.Expected(string? format, string? example)`, `.Options(IEnumerable<string>)`, `.With(string key, object? value)`;
  `.ToBody(string? traceId)` → `Dictionary<string, object?>` in the fixed order with nulls dropped, falling
  back to `Summary`/`DefaultHint`; `.ToResult(HttpContext)` → `ObjectResult` with the code's status. Each
  method is one statement; it is a dictionary with an opinion about order.
- `static class ProblemCatalog`: `Resolve(string nameOrClass)` → `(className, problemName, instanceFormat,
  certificateFormat, defaultInstance, certificateExample)?`, cached per problem, built from
  `ProblemProvider.Problems` + `ProblemDefaults`. Absorbs `LookupInstanceFormat` (PP:315-330),
  `ParseGuard.ProblemName` (PE:94-100) and `SolverUnavailableException.OtherSolvers` (SUE:67-88), so
  `time_limit` and `solver_unavailable` share one "other solvers" source.
- `static class ApiErrorMapping`: `ApiError? From(Exception ex)` — unwrap `TargetInvocationException` /
  single-inner `AggregateException` (generalising `SolverUnavailableException.Find`), then one `switch`.
  **No existing exception is renamed**; they fold in:
  `ReduxApiException` → its error · `InvalidInstanceException` → `invalid_instance` ·
  `ProblemParseException` → `instance_parse_error` · `InvalidCertificateException` → `invalid_certificate` ·
  `CertificateParseException` → `certificate_parse_error` · `ReductionInputException` →
  `reduction_input_parse_error` (+`field` = `"instance"` if `ExpectedFormat == reductionFrom.instanceFormat`
  else `"solution"`) · `ExactSearchLimitExceededException` → `exact_search_limit_exceeded` (+`problem`) ·
  `NoVerifierException` → `no_verifier` · `SolverUnavailableException` → `solver_unavailable` (message/hint/
  options logic moved verbatim from `Body()`) · `NotImplementedException` → `not_implemented` · else `null`.
- `sealed class ReduxApiException(ApiError error) : Exception` for throw sites below the controller that
  want a specific code (generators now; leaf fixes later).

**New `Interfaces/ApiExceptionHandler.cs`:**
- `sealed class ApiExceptionHandler : IExceptionHandler` (ASP.NET Core 8+): `TryHandleAsync` →
  `ApiErrorMapping.From(ex) ?? ApiError.Of(ErrorCodes.InternalError).Detail($"{ex.GetType().Name}: {ex.Message}")`,
  write `ToBody(context.TraceIdentifier)` with the code's status, return `true`. Replaces
  `SolverUnavailableFilter` now and every per-endpoint `catch` in step 5. Logging comes from
  `UseExceptionHandler` itself (it logs with the request scope, which includes the trace id). .NET 9+
  suppresses that log when a handler answered with a <500 status — **verify** the .NET 10
  `ExceptionHandlerOptions.SuppressDiagnosticsCallback` default when wiring it, so mapped 4xx stay quiet and
  `internal_error` keeps its stack. Do not add a second `ILogger` call (double logging).
- `static class ReduxErrorHandling` with `AddReduxErrorHandling(this IServiceCollection)` and
  `UseReduxErrorHandling(this WebApplication)` so `Program.cs` gains ~3 lines:
  `AddExceptionHandler<ApiExceptionHandler>()`; `ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx => ApiError.Of(ErrorCodes.InvalidRequest).With("errors", …).ToResult(ctx.HttpContext))`;
  `app.UseExceptionHandler()` **first** in the pipeline; `app.UseStatusCodePages(...)` writing
  `route_not_found` / `method_not_allowed` / `unsupported_media_type` for body-less 404/405/415.
- **CORS fix:** change `Program.cs:55-58` to `context.Response.OnStarting(() => { headers[...] = "*"; return Task.CompletedTask; })`
  so the header survives the exception middleware's response reset. (`UseCors` further down is also
  configured; keep both as they are today otherwise.)

**`AdditionalControllers/ProblemProvider.cs`:**
- Holes: `verify` → `Verifiers.TryGetValue` → `unknown_verifier`; if the type is `DummyVerifier` answer
  `no_verifier` before running anything (exception path stays as fallback). `problemInstance` →
  `Problems.TryGetValue` → `unknown_problem` with `options`. The constructors outside `try` (PP:381, 523,
  530) become structured `internal_error` automatically once the handler exists; fix `SipserReduceToSAT3()`
  itself in step 6.
- All direct bodies (`unknown_*`, `solver_mismatch`, `no_backward_map`, `exact_search`, the
  `TimeLimitError` family and `ComputeLimits.TimeLimitBody`) go through the builder and gain
  `options`/`hint`/`field`. `SolveTimeLimitError`'s "other solvers" switches to `ProblemCatalog`.
- Then delete `ParseError`, `ParseErrorBody`, `ReductionParseErrorBody`, `ExactSearchLimitError`,
  `IsParseError`, `IsExactSearchLimitError`, `Unwrap`, `LookupInstanceFormat` and every
  `catch (Exception ex) when IsParseError(ex)` block (~90 lines) — the handler maps them. **Keep**
  `Reduction(name, instance)`'s re-parse classification (PP:188-203) and `MapSolutions`/`MapSolutionsBack`
  (PP:208-224): they decide *which* exception to throw, which a handler cannot.
- #992/#994: `ComputeJobLimiter.ServerBusyBody`, `ComputeLimits.MemoryLimitBody`,
  `SolveWorkerPool.WorkerFailedBody` → `ApiError.Of(ErrorCodes.X).Message(...).Detail(...).With(...).Hint(...).ToBody(traceId)`,
  keeping their keys (`maxConcurrentJobs`, `retryAfterSeconds`, `limitMB`, …).

**Other controllers:**
- `Interfaces/Tools/ProblemInstanceGenerators.cs:115-167`: validate `n >= 0`, `0 <= density <= 100`,
  `k >= -1`, `c >= 0` → `throw new ReduxApiException(ApiError.Of(ErrorCodes.InvalidParameter).Received(value, "density").Expected("0 <= density <= 100", "50"))`.
- `AdditionalControllers/Navigation/ContributerProfile.cs:64-148`: delete the
  `catch (Exception ex) { return BadRequest(new { error = ex.Message }); }` blocks; the handler answers
  `internal_error` with a trace id. A missing `contributorInfo.json` is a deployment defect, so 500 is honest.
- `Nav_Batch.cs:44-73`: construct the `Lazy<string>`s with `LazyThreadSafetyMode.PublicationOnly` so an
  exception is not cached.
- `Nav_Solvers.cs:84`, `Nav_Verifiers.cs:49`: return `IActionResult`; unknown problem → 400 `unknown_problem`
  with `options` (step 8, compat-breaking).

**`invalid_instance` / `invalid_certificate`:** add `InvalidInstanceException : ProblemParseException`
and `InvalidCertificateException : CertificateParseException` to `Interfaces/ParseExceptions.cs`;
`ParseGuard.CreateProblem` already rethrows a `ProblemParseException` untouched (PE:83-85), so subclasses
pass through, and `From` matches the subclass first. One exemplar: move UNSTRUCTUREDSEARCH's power-of-two
check from `UnstructuredGroverSolver.cs:27-30` into the problem constructor.

### 3.5 Compatibility policy
**Additive, safe immediately:** every new field; omitting null `problem` in `time_limit`; new codes on
paths that were 500 or empty-bodied (`unknown_verifier`, `unknown_problem`, `internal_error`,
`invalid_parameter`, `route_not_found`, `method_not_allowed`, `unsupported_media_type`, `not_implemented`);
key order; `options` ordering unification.

**Coordinated changes — list each in its PR description and in the GUI/mcpredux follow-up issues:**
1. `reduction_input_parse_error.reduction` friendly → class name (unpinned; GUI reads `error/expected/detail/message/hint` only).
2. Framework 400 `{type,title,status,errors,traceId}` → `{error:"invalid_request", …, errors, traceId}`.
   GUI's fallback chain now finds `message`; mcpredux passes text.
3. `exact_search_limit_exceeded` 400 → 422. Update `WeightedMaxSat_Endpoint_Tests.cs:104-114`. The GUI
   e2e fixture's pinned 400s are parse failures on default instances, so they don't hit this — confirm by
   running the suite (§5).
4. Navigation unknown problem 200 `"entered a solver that does not exist"` → 400 `unknown_problem`. Update
   `Navigation_Endpoint_Tests.cs:156-165, 242-250`. Check `Redux_GUI/components/redux/index.js` callers
   first; the GUI batches via `Batch/all*` since 2026-07-20, so the per-problem routes are probably unused.
   mcpredux stops seeing an error as a success.
5. ContributorProfile 400 free-text → 500 `internal_error`.

**Unchanged on purpose:** the 15 existing codes; `time_limit` and `solver_unavailable` message/hint/options
text and order; `received`/`expected`/`problem` values on parse errors; `unknown_format.received`;
`solver_mismatch` fields; `no_backward_map.reduction`; `no_verifier`/`no_backward_map` at 400.

---

## 4. Delivery: one PR per step, stop for review after each

| # | Step | Touches | Done when (tests) | vs #992/#994 |
|---|---|---|---|---|
| 1 | **Catalog + builder** (no behaviour change) | new `Interfaces/ApiErrors.cs` | new `redux-tests/Endpoints/ApiError_Contract_Tests.cs`: codes unique and `^[a-z_]+$`; status ∈ {400,404,405,415,422,429,500,501,503}; (status, category) pairs match §3.2; every 4xx has `DefaultHint` or is in an explicit per-site set; `ToBody("t")` for every code yields `error,status,category,retryable,message,traceId` in order with `status == code.Status`; `From(...)` for each existing exception type (incl. `TargetInvocationException`-wrapped) gives the right code and fields; `From(new InvalidOperationException())` is null | none. Land first; then switch your three bodies in #992/#994 to `ApiError.Of(...)` |
| 2 | **Global handler + CORS fix** | new `Interfaces/ApiExceptionHandler.cs`; `Program.cs`; `SolverUnavailableException.Body()` → `ApiErrorMapping.From(this)!.ToBody(...)`; delete `SolverUnavailableFilter` | unit test on `TryHandleAsync` with `DefaultHttpContext` + `InvalidOperationException` → 500 `internal_error` with `traceId`; all `SolverUnavailable_Endpoint_Tests` unchanged and green; `Access-Control-Allow-Origin: *` present on a 400 and on a 500 | `Program.cs` +3 / +10 lines in your PRs; trivial rebase |
| 3 | **Framework shapes** | `AddReduxErrorHandling` gains `InvalidModelStateResponseFactory`; `UseStatusCodePages` | `GET /CLIQUEGeneric/solve` → 404 `route_not_found`; `GET /ProblemProvider/solve` → 405; POST verify with no body → 400 `invalid_request` with `errors`; wrong content-type → 415 | none |
| 4 | **ProblemProvider bodies + holes** — **after #992 merges, ideally #994** | PP direct bodies via the builder; `unknown_verifier`, `no_verifier` by type, `unknown_problem`; delete `no_reductions_provided`; exact-search → 422 + `problem`; `ComputeLimits.TimeLimitBody` (and your three bodies if merged) → builder | new theory: every endpoint × unknown name → 400, `error` ∈ `ErrorCodes.All`, `options` non-empty, `received`+`field` set; `WeightedMaxSat_Endpoint_Tests` status updated; every existing pin in §2.7 still green | heavy overlap with #994's `solve()` rewrite — this is why it waits |
| 5 | **Remove per-endpoint catches** | PP: delete `ParseError*`, `ExactSearchLimitError`, `IsParseError`, `IsExactSearchLimitError`, `Unwrap`, `LookupInstanceFormat`, all `catch … when` blocks | `ParseError_Endpoint_Tests`: `AssertNot500` → `AssertStructuredError` (status 4xx, body parses, `error` ∈ `ErrorCodes.All`, `status` matches, `message`/`hint`/`traceId` present); 20 catch-and-return-false verifiers still answer 200 `"False"` (that's the leaf issue, not a failure here) | after #994 |
| 6 | **Other controllers** | generators; ContributorProfile catch removal; `Nav_Batch` `PublicationOnly`; `SipserReduceToSAT3()` default ctor | generator theory (`n=-1`, `density=101`, `c=0` → 400 `invalid_parameter` with `expected`); `info?interface=SipserReduceToSAT3` → 200 | none |
| 7 | **`invalid_instance` / `invalid_certificate`** | `ParseExceptions.cs` subclasses; `From` order; UNSTRUCTUREDSEARCH ctor | solve/visualize with a 3-element UNSTRUCTUREDSEARCH instance → 400 `invalid_instance` with `expected`/`expected_example` (was 500) | none |
| 8 | **Navigation unknown → 400** (compat-breaking; after the GUI check in §3.5 item 4) | `Nav_Solvers.cs`, `Nav_Verifiers.cs` | the two pinned tests updated; new test: `options` lists problems | none |
| 9 | **Docs + lockstep test + Swagger** | new `Documentation/guides/api-errors.md`; `troubleshooting.md` §5; `ProblemTemplate/Templates/README.md:59,104`; `adding-a-verifier.md:133-179`; `how-the-code-works.md` §8; `Tools/QUANTUM_API_README.md:162-185`; `Documentation/guides/README.md` table and `Documentation/index.md`; a Swashbuckle `OperationFilter` (model: `ReductionsResponseExampleFilter`) attaching one `ApiError` schema + example to every 4xx/5xx `ProducesResponseType`, and declaring 503/429 on compute endpoints | docs test: every code in `ErrorCodes.All` has a `### <code>` heading in `api-errors.md` and vice versa (same pattern as `VisualizationType_Tests` vs `visualization-types.json`) | each of your PRs adds one troubleshooting row; trivial |
| 10 | **Follow-up issues** (no code) | see §6 | — | — |

Steps 1–3 can land this week regardless of #992/#994. Steps 4–5 follow #994's merge.

---

## 5. How to verify each step

This host has no .NET SDK; use the devcontainer (§1.4). For every PR:
1. `dotnet build Redux.slnx -c Release` — catches format and warnings-as-errors.
2. `dotnet test Redux.slnx` (full) or `--filter FullyQualifiedName~ApiError` while iterating; `rbs unit-test`
   is what CI runs (80% floor).
3. Probe the running app (devcontainer `appPort` 27000 once #879 merges, or `rbs build` → `docker run -p 27000:27000 local/redux:ci`):
   ```bash
   curl -si -X POST 'localhost:27000/ProblemProvider/solve?solver=Nope' -H 'content-type: application/json' -d '"x"'
   #  → 400, body has error/status/category/retryable/message/field/received/options/hint/traceId
   curl -si 'localhost:27000/CLIQUEGeneric/solve'                      # → 404 route_not_found
   curl -si -X POST 'localhost:27000/ProblemProvider/verify?verifier=Nope' -H 'content-type: application/json' -d '{"ProblemInstance":"x","Certificate":"y"}'
   #  → 400 unknown_verifier (was 500)
   curl -si -X POST 'localhost:27000/ProblemProvider/solve?solver=Sat3BacktrackingSolver' -d 'not json'
   #  → 415 unsupported_media_type
   ```
4. Push; read the `rbs-report` artifact of the run (`gh run list --json databaseId,headBranch -q '.[] | select(.headBranch=="<branch>")'`, then `gh run download <id>`).
5. **Before step 8** (and after step 4): run Redux_GUI's Playwright suite against the new image
   (`cd ~/projects/ISU/Redux_GUI && rbs integration-test`, which stands Redux up from `ghcr.io/reduxisu/redux:latest`;
   to test an unmerged Redux, retag your `local/redux:ci` as that name locally) and confirm
   `tests/e2e/fixtures.js` pins still hold.
6. Swagger (`/swagger`) after step 9: every 4xx/5xx response shows the `ApiError` schema and an example.

---

## 6. Follow-up issues to open (step 10) — use §3.2 as the spec in each

**Redux** (one issue per pattern, good first issues for students):
- (a) 20 verifiers catch-and-return-false (list in §2.6): throw `CertificateParseException` (or let the
  parse exception escape) for unreadable text; return `false` only for parsed-but-wrong.
- (b) Solver sentinel answers: one convention for "no solution" (`"{}"` per #605) and
  `InvalidInstanceException` for invalid instances (`"Breaks DJ Promise"`, `"invalid func…"`,
  `"character not in DFA alphabet"`); make SAT/SAT3 verifiers accept the convention.
- (c) `KarpReduceGRAPHCOLORING.cs:312`, `KarpIntProgStandard.cs:122`: `ReductionInputException` → `InvalidCertificateException`.
- (d) 8 quantum visualizations through `QuantumServerAPI.Ask` so a service failure is 503, not an empty picture.
- (e) Quantum `HttpClient` timeout from `ComputeLimits.TimeLimit` (so a hung service yields 503, not 422);
  and give `Ask` sub-detail (not configured / unreachable / HTTP status / timeout / bad JSON / no answer)
  via `detail`, keeping one code.
- (f) `PartitionDefaultVisualization.cs:29-37` on `"{}"`.
- (g) `ParseGuard.VerifyCertificate` net too wide: NRE/IndexOutOfRange in checking logic should stay 500 —
  needs verifiers to separate parse from check first (depends on (a)).
- (h) 11 stub `mapSolutions` returning `""`: throw `NotImplementedException` → 501 `not_implemented`
  (free once step 2 exists).
- (i) Pass inner exceptions when throwing parse exceptions (cause chains lost in most leaf throws).

**mcpredux:** rewrite `tests/test_errors.py` against real codes (`instance_parse_error`,
`certificate_parse_error`, `reduction_input_parse_error`; `input_shape_mismatch` never existed); have
`server.py` parse the JSON and surface `message`, `hint`, `expected`/`expected_example`, `options`
distinctly, and treat `retryable`/`category` (retry on `busy`/`dependency`; never on `input`); after step 8
navigation errors are no longer 200 strings. Update `TODO.md §4`.

**Redux_GUI:** read `hint`/`options` in the verify and visualize rows the way #406 did for solve
(`VisualizeRowReact.js:162` passes no `onError`); handle 400 `unknown_problem` from navigation if any
caller remains; fix the proxy's own `error` field (it is free text in `pages/api/redux/[...path].js:8,24,31,59`,
clashing with `error`-as-code).

**ISU docs:** `~/projects/ISU/CLAUDE.md:183-188` (quantum failures are 503 now, not 200 + string) and the
`/visualize` 500-on-UNSAT notes in `REDUX_VR.md:134,247` (stale for SAT/SAT3: `SatDefaultVisualization.cs:28-30`
skips items without `:`).

---

## 7. Briefing your agent

Give it this file plus `~/projects/ISU/CLAUDE.md`, and one step at a time. A brief that works:

> Read `~/projects/ISU/ERROR_CODES_OVERHAUL.md` fully, then `~/projects/ISU/CLAUDE.md`. We are doing **step N**
> of §4 only — do not start step N+1. Follow §3.4 for file names and shapes and §3.3 for field semantics
> exactly; do not rename or restatus anything listed as "unchanged on purpose" in §3.5. Reuse the existing
> helpers named in §3.4 (`ParseGuard`, `SolverNavigationData`, `ComputeLimits.TimeLimitBody`,
> `ReductionsResponseExampleFilter` as the Swagger pattern) rather than writing parallel ones. Keep
> functions single-path and comments minimal. Write the tests listed in the "Done when" column, run
> `dotnet build Redux.slnx -c Release` and `dotnet test Redux.slnx` in the devcontainer, and show me the
> output. Don't commit.

Things the agent will get wrong without being told:
- `Directory.Build.targets` runs `dotnet format --verify-no-changes` in Release: run `dotnet format` before
  building, or the build fails on whitespace.
- `ParseGuard.CreateProblem` **already** unwraps `TargetInvocationException` and rethrows
  `ProblemParseException` as-is (PE:82-85); don't add another unwrap layer in the leaf, do it once in
  `ApiErrorMapping.From`.
- TestServer rethrows unhandled exceptions today (`ParseError_Endpoint_Tests.cs:10-12`); once
  `UseExceptionHandler` is first in the pipeline it answers a real 500 JSON instead. The helpers' catch
  branches become dead but harmless; the sweep then fails with a readable `detail` + `traceId`.
- `WebApplicationFactory` runs as Development; `Program.cs` has no environment branch, so test behaviour
  equals production behaviour. Keep it that way (no `IsDevelopment()` forks in the handler).
- `[ApiController]` short-circuits model validation before the action runs: the
  `string.IsNullOrEmpty(@interface)` branch at PP:379 is dead for null, and `Verify` (`Tools/ApiParameters.cs`)
  has `= ""` defaults with no `[Required]`, so `{}` passes validation and only a missing/unparseable body hits
  `invalid_request`.
- `options` for solvers must come out in the navigation order `SolverUnavailable_Endpoint_Tests` pins, not
  sorted.
- The `Dictionary<string, object?>` body is what gives fixed key order; an anonymous object would
  reorder per call site.
- Judgement calls already made (flag them in the PR, don't re-decide): `internal_error.detail` carries
  `ExceptionType: message` (no stack) in every environment — open academic API, and the detail is what
  lets an agent self-correct; `unknown_*` and `no_verifier`/`no_backward_map` stay 400; only
  `exact_search_limit_exceeded` changes status.
