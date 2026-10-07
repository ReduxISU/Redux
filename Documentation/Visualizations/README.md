# Implementing the new visualizations

This guide tells an agent (or a person) how to turn the visualization mockups into real code across the three repositories:

- **Redux** (this repo, C# API): problems, solvers, reductions, and the JSON the frontends draw.
- **Redux_GUI** (Next.js + MUI): the current site.
- **Redux_Frontend** (Next.js): the alternative frontend. The two frontends are parallel options, not old-versus-new. Scope and file work on each separately.

The mockups are the specification. Everything here was designed, built and tested in them first.

| What | Where |
| --- | --- |
| Design mockup (every picture type, one tab each) | [`mockups/design.html`](mockups/design.html), published at https://claude.ai/artifact/KZoqj8AqhHSBJYWMVLSzkx |
| Redux_GUI mockup (the real page with every problem wired in) | [`mockups/redux-gui.html`](mockups/redux-gui.html), published at https://claude.ai/artifact/WGGHMS9UGDB8is4Bf1e4Mf |
| Plain-language summary for people | Claude Doc "Fixing Redux's Problem Visualizations", https://claude.ai/code/artifact/eeccd33d-0a1e-4a05-836e-3ef728cc14be |
| The original proposal this builds on | `Visualization_Proposal.pdf` (shared separately; covers the Graph base, state vocabulary and TikZ retirement) |
| Issues | Backend tracker #655 (PRs 1–7, with dependencies); frontend trackers ReduxISU/Redux_GUI#351 and ReduxISU/Redux_Frontend#198 |

Open the two HTML files directly in a browser. They work offline.

---

## 1. Ground rules

1. **Only build what's approved.** Each picture type has a status in the Claude Doc's "Picture types" tables (Mockup built → Approved). Don't implement a type until it's Approved. Each mockup tab ends with a "For review" or "Decisions" list; resolve those first.
2. **One picture type per pull request**, per repo. Backend first, then each frontend.
3. **Problems hand over data, never drawings.** A problem says what its graph or instance is and what shape its answer has. The shared renderer decides every color. No new per-problem highlight loops.
4. **Steps come from the solver, not the visualization.** The visualization draws whatever frames the selected solver recorded. The JavaScript solvers in the mockups are reference implementations and test oracles. Port the *recording* into the real C# solvers; don't ship the JS solvers in a frontend.
5. **Names match what's implemented.** If a problem's checker disagrees with its name or description, fix the text (example: Redux's Hitting Set is Exact Hitting Set).
6. **Rendering approach:** React owns the SVG elements; D3 is used only for math (`d3-force`, `d3-zoom`, `d3-shape`). No `d3.select` on the page, and no global `#id` lookups. Layouts are deterministic: the same instance always draws the same way.
7. **Repo conventions still apply:** read each repo's `CLAUDE.md` / `AGENTS.md`, read the rbs report on every PR (it can be green while failing), and don't add `Co-Authored-By` or `Claude-Session` lines to commits or PRs in Redux or Redux_GUI.
8. **Check the JSON, not just the build.** Redux serializes with `System.Text.Json` and `IncludeFields`, which leaks public fields and skips default interface members. For every new payload, call the endpoint and compare the actual JSON to the contract.

---

## 2. Architecture in one picture

```
 Problem (C#)                Solver (C#)                    Reduction (C#)
 instance + answer shape     solve() + recorded steps       target instance + gadget map
        │                         │                          + construction / map-back steps
        ▼                         ▼                                 │
 ┌──────────────────────────────────────────────────────────────────▼──┐
 │ POST /ProblemProvider/visualize?visualization=…&solver=…            │
 │ → { type, payload, frames[] }   one JSON shape per picture type      │
 └───────────────────────────────┬──────────────────────────────────────┘
                                 ▼
        Frontend renderer for that picture type (React SVG + D3 math)
        payload + current frame + hover  →  elements with state classes
```

**Shared state vocabulary.** Every frame names states with these words, and each frontend maps them to styles in one table:

| State | Meaning |
| --- | --- |
| `Background` | Nothing has happened to it yet |
| `ElementHighlight` | What the solver is looking at or trying right now |
| `Solution` | Part of the answer, or a run that ended accepted |
| `Rejected` | Failed: falsified clause, rejecting state, uncovered or doubled element |
| `Untraveled` | Never reached during this run |
| `Covered` | A constraint that is met (element covered, set hit, clause satisfied) |
| `Blocked` | Can't be chosen without breaking an exactly-once rule |
| `False` | Assigned and false (still under review) |

Badges such as ×2 (covered twice), order numbers in a tour, missing-edge lines and group colors are drawn annotations, not states.

---

## 3. Backend work in Redux

Do these in order. Steps 3.1 to 3.3 are shared plumbing; every picture type needs them.

### 3.1 Let the visualize endpoint use the chosen solver

- `AdditionalControllers/ProblemProvider.cs`, the `visualize` action, calls `vis.solver.GetSteps(...)` and `vis.solver.solve(...)`. The solver is hard-wired per visualization, so the user's choice never reaches the picture.
- Add an optional `solver` query parameter. When given, resolve it the same way the solve endpoint does and use it; otherwise fall back to `vis.solver`.
- Solve once and reuse the result. Today `GetSteps` and `solve` each run the solver.
- Keep the response backward compatible until both frontends move: today it's a flat list `[initial, ...steps, solved]` read by position. Add the new shape under a version flag or a new route, so old renderers keep working.

### 3.2 Typed solver steps

- `Interfaces/SolverInterface.cs`: `GetSteps` returns `List<Object>`, and only 6 of 80 solvers implement it (DFA, NFA, SSSP, SPSP and both Pump Scheduling solvers).
- Add one step type per answer shape, not per visualization:

  ```csharp
  record SolverStep<TCert>(
      TCert Partial,          // the answer so far, in the problem's certificate format
      StepEvent Event,        // Try, Accept, Reject, Backtrack, Done
      string[] Focus,         // optional: what the solver is looking at
      string Caption);        // one plain sentence, shown under the picture
  ```

  Problems with the same answer shape share one step type: a set of nodes, a set of edges, a split into groups, a tour, an assignment, a set of chosen sets, an active-state set.
- Each solver adds steps through a small recorder, active only when steps are requested.
- **Replay test for every solver:** replaying its steps must end at the same answer `solve()` returns. Put it in `redux-tests`.
- Record decisions (choose, reject, back out), not loop iterations. Large runs are capped or sampled; the mockups cap at 120 to 150 shown tries and say how many were hidden.

### 3.3 Picture types and the manifest

- `Interfaces/VisualizationType.cs` is checked by CI against `Documentation/visualization-types.json`. Redux_GUI keeps a vendored copy (`components/Visualization/svgs/visualizationTypes.json`) cross-checked daily. Add new values in all three places in the same change set.
- Every payload carries a `kind`/`type` discriminator (see #524). Graph payloads also need a graph-level subtype. Today `directed`/`weighted` are set per link on `API_Link`, so nothing says "this is an automaton" or "this is bipartite".

### 3.4 Shared bases instead of per-problem drawing code

For each approved picture type, write one base class that turns *instance + answer shape + rule* into the payload. Problems only declare those three things. The mockup module for that type (section 5) contains the exact rules, for example `evaluate()` in `graphbase.js`.

### 3.5 Reductions

- Reductions already send gadget data (`Interfaces/JSON_Objects/Gadget.cs`, `GadgetInterface.cs`, `POST ProblemProvider/gadgets`), but the gadget's kind travels in `Gadget.color`.
- Add a `kind` taken from one shared list (element, group, edgeRule, palette, orGadget, bound) and stable source/target ids.
- Add construction steps and a **backward** answer map (B's answer back to A). `mapSolutions` only maps forward today.
- See `reduction.js` for the exact frames for Sipser 3SAT → Clique, Sipser Clique → Vertex Cover and Karp 3SAT → Graph Coloring.

### 3.6 Fix the bugs found along the way (separate issues, not part of the picture work)

They are filed as sub-issues of the backend "PR 6" issue under tracker #655. Highest impact:

- Convex Hull returned a non-hull on 22 of 200 point sets with repeated x values.
- Cut and Weighted Cut accept any K edges, even ones that don't split the graph.
- The Pump Scheduling Cost Minimization example certificate fails its own checker.
- 3-Dimensional Matching's checker accepts incomplete answers.
- Several problems are filed in `Problems/NPComplete` but are P, EQP or BQP (Edit Distance, Convex Hull, Lossless Data Compression, the quantum problems).

---

## 4. Frontend work (both sites)

The two frontends share the same contract but not code. Build each separately. A framework-free module of layouts and state rules could be shared later if the team wants that.

### 4.1 How to port one mockup module

Each mockup module (`mockups/*.js`) has the same four parts. Treat them differently:

| Part in the mockup | What to do with it |
| --- | --- |
| `parse()` | Don't port. The backend parses and sends the payload. Keep it only for tests. |
| Solvers (`solve…`, `trace…`, `SOLVE`) | Don't port to the frontend. Use them as the reference when adding step recording to the C# solver, and as a test oracle. |
| Layout (`layout()`, ring, layers, columns, BFS ranks) | Port as a pure function `(payload) → positions`. Keep it deterministic. |
| Paint (`paint()`, `evaluate()`, CSS classes) | Port as a React component: `(positions, frame, hover) → <svg>` with state class names. Move colors into the site theme. |

### 4.2 Layout and responsive rules (from the mockups; apply in both sites)

- **Wide screens (1,200 px and up):** diagram on the left, side panel (steps, answer, checks, key) on the right.
- **Phones and tablets (up to 1,199 px):** stack them. Order in the stacked panel:
  1. stepper and caption;
  2. answer and checks;
  3. the key, folded closed behind a "Key" toggle.

  Desktop starts with the key open.
- **Phones (up to 700 px):** each diagram gets `min-width: 560px` inside its own sideways-scrolling box, with a "Swipe the diagram sideways…" hint. Only the diagram scrolls; controls and captions don't. The Reduction view needs about 960 px.
- **Short screens (height 560 px or less):** remove any diagram max-height, or labels shrink to unreadable sizes.
- **Touch (`pointer: coarse`):** controls at least 44 px tall. Inputs and selects use 16 px text, so iOS doesn't zoom on focus.
- **Wording:** on `hover: none` devices, say "Tap" instead of "Hover". Every hover interaction must also work on tap.
- **Themes:** light and dark both work, every color comes from theme tokens, and SVG text always sets its fill.
- **Accessibility:** focusable nodes with `aria-label`s, `role="img"` with a label on each diagram, visible focus rings, and `prefers-reduced-motion` respected.

### 4.3 Redux_GUI specifics

- **Where things are:**
  - `components/pageblocks/VisualizeRowReact.js`: the Visualize section and its controls (Refresh, step navigation, the Show reduction / Highlight gadgets / Highlight solution switches).
  - `components/widgets/VisualizationLogic.js`: chooses a renderer by visualization type.
  - `components/Visualization/svgs/Visualizations.js`: the type registry.
  - `components/Visualization/ReducedVisualization.js` and `components/redux/index.js`: reduction view plumbing.
- **Replace `StandardGraphSvgReact.js`; don't extend it.** It renders with imperative D3 and uses page-wide `d3.selectAll("#id…")` for gadget hover, which breaks when two graphs are mounted. It also runs `d3.scaleLinear` over string edge weights such as `"a,b"`.
- **The Solve section's selected solver** already reaches VisualizeRow (`chosenSolver`). Pass it to the visualize request (3.1).
- **Highlight gadgets** is currently gated on a hard-coded `#highlightGadgets` checkbox id. Drive it from state instead.
- **Retire TikZ last:** once DFA, NFA and Clique's LaTeX view are on the new renderers, remove `LaTeXGraphSvgReact.js` and `node-tikzjax`. Its `svgo` dependency carries the advisories listed in the proposal.
- **Match the mockup's GUI page** (`mockups/redux-gui.html`): visualization picker with type tags in Title Case ("Grouped Graph"), second views ("Factor Graph", "Trace Table", "Search Tree") as extra picker entries, and menu groups by class.
- The tour (`components/tour/steps.js`) points at `data-tour-id="viz-controls"`. Keep that id.

### 4.4 Redux_Frontend specifics

- **Where things are:** `components/detail/visualizations/` holds:
  - `GraphRenderer.js` (React-owned SVG with a one-shot `d3-force`; the closest match to the target approach);
  - `graphGeometry.js`, `BooleanSatisfiabilityRenderer.js`, `StepTableRenderer.js`, `QuantumCircuitRenderer.js`, `PumpScheduleRenderer.js`, `RecursiveSetRenderer.js`;
  - `VisualizationCanvas.js` and `ZoomPanSurface.js`.
- **Update the docs agents read** when a type lands: `ai_documentation/VISUALIZATION_TYPE_CONTRACTS.md` (§3.1 graph, §3.3 SAT, §4.5 gadgets, which the Frontend currently leaves out) and `ai_documentation/VISUALIZATION_TAXONOMY_WISHLIST.md`.
- **Fix the SAT connectives:** `BooleanSatisfiabilityRenderer.js` joins literals with ∧ and clauses with ∨ (its constants `CONJUNCTION` / `DISJUNCTION`), citing a note ("T40") about what Redux_GUI draws. That note misread the GUI. `StandardSATSvgReact.js` draws ∨ inside a clause and ∧ between clauses, which is correct for Redux's CNF instances (`(x1 | !x2 | x3) & …`). The Frontend's picture is reversed and should be swapped.
- **Gadgets:** Redux_Frontend currently drops gadget data. The Reduction view needs it.

---

## 5. Picture types: mockup source and what's special

Each row lists the mockup module, the design-mockup tab, and what to carry over. Every tab also shows its full input contract and its review questions.

| Picture type | Problems | Mockup module | What to carry over |
| --- | --- | --- | --- |
| Graph (base) | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set, Graph Coloring, Clique Cover, Max Cut, Cut, Weighted Cut, Minimum Cut, MST, Steiner Tree, Hamiltonian Cycle, TSP | `graphbase.js` (`evaluate`, `layout`, `SOLVERS`) | Answer shapes subset / edges / partition / tour plus a rule. Deterministic Fruchterman–Reingold or circle layout. Missing-edge phantoms. Six colorblind-safe group colors. |
| Grouped Graph (clause graph) | SAT, 3SAT | inline in `design.src.html` (`build`, `trace`) and `redux-gui.src.html` | Clusters on a ring, literals inside, complement edges curved through the center, witness ring per clause. |
| Bipartite Graph | Set Cover, Exact Cover, Exact Hitting Set, SAT factor view | `bipartite.js` (`create`, `createSat`) | Two columns, the "● PICKED" side, sets sorted by barycenter to cut crossings, ×N badges. **Changes the wire format** for the set problems (nested set → node-link), so it needs a coordinated frontend change. |
| Automaton | DFA, NFA | `automaton.js` | BFS-layered layout, merged edge labels, start arrow, double ring, input tape, Garbage state for missing DFA moves, all branches plus one-path view for NFA. |
| Layered Graph | Topological Sort, Feedback Arc Set, Feedback Node Set, Directed Hamiltonian Cycle, Strongly Connected Components, SSSP, SPSP | `layered.js` | Sugiyama-lite: break cycles by DFS, rank by longest path, barycenter ordering, back edges curved under the rows. Dijkstra distances under nodes. |
| Flow Network | Minimum S-T Cut | `flow.js` | Source left, sink right, flow/capacity labels, one augmenting path per step, backward residual arcs, S/T sides and cut edges at the end. |
| Packing | Partition, Subset Sum, Knapsack, Bin Packing | `packing.js` | Items as bars to scale, containers with target or limit lines, Partition balance. The mockup also includes Subset Product (log scale); that problem exists only on a demo branch, not in Redux. |
| Board | Sudoku, N-Queens | `board.js` | Clues vs placed digits, clash links, attacked squares, pencil marks toggle. |
| Table / Trace Table | Edit Distance, 0-1 Integer Programming, plus a second view for DFA, NFA, SSSP, SPSP | `table.js` (HTML host, not SVG) | DP grid with neighbor arrows and backtrace. Constraint rows with totals. Trace tables reveal rows step by step. Extends `API_TableJSON`. |
| Quantum Circuit | Bernstein-Vazirani, Deutsch, Deutsch-Jozsa, Simon, Unstructured Search, Prime Factorization | `quantum.js` | One gate-list payload replacing the D3-or-QASM flag. In-browser statevector up to 15 qubits. Bars colored by amplitude sign to show phase kickback. |
| Schedule | Job Sequencing, Pump Scheduling (both) | `schedule.js` | Time axis to scale, deadlines, 24-hour pump rows with tank and cost charts. Real DP forward-pass frames (the C# solver must keep its table to provide them). |
| Geometry, Tripartite Matching, Code Tree | Convex Hull, 3-Dimensional Matching, Lossless Data Compression | `misc.js` | Points on real axes with divide-and-conquer merge steps. Triples as polylines through three columns. Huffman tree plus encoded bits. |
| Reduction view | 3SAT → Clique, Clique → Vertex Cover, 3SAT → Graph Coloring | `reduction.js` | Side-by-side A ↦ B, gadget map with kinds, three phases (Build, Solve, Map back), cross-pane hover. Already wired into the GUI mockup's real Reduce picker and switches. |
| Search Tree / Game Tree | Second view for 3SAT, Knapsack, TSP, Graph Coloring, N-Queens; QBF and Generalized Geography | `searchtree.js` | Tidy tree, cut vs backed-out branches, best-first queue order badges, "+k more" folding at about 12 leaves, alternating turn bands for game trees. Step = existing step plus a `parentId`. |
| Tape and stack machines | Turing machine, LBA, PDA (not in Redux yet) | `machine.js` | Automaton diagram plus tape with head, or stacks per branch. Honest step-limit ending. |
| Boolean Circuit, Grammar | Circuit Value, Circuit-SAT, CFG membership (not in Redux yet) | `circuitgrammar.js` | Gate glyphs, 1 = thick solid and 0 = thin dashed. CYK triangle then parse tree. |
| Dominoes, Two-Graph Mapping | Post Correspondence Problem, Graph Isomorphism, Subgraph Isomorphism (not in Redux yet) | `pairview.js` | Aligned top and bottom strings with tile spans, honest "undecidable" ending. Paired node colors with degree pruning. |

Problems marked "not in Redux yet" need a new problem class first, with instance formats as proposed on their tabs. Redux's `ComplexityClass` has no value for undecidable or PSPACE-complete problems yet.

---

## 6. Suggested order

1. **Plumbing:** 3.1 solver parameter, 3.2 step types and the replay test, 3.3 type/manifest field. Small, unblocks everything.
2. **Graph base:** backend base plus both frontend renderers. Biggest win by count (15 problems).
3. **Automaton:** then retire TikZ (section 4.3).
4. **Grouped Graph and Bipartite Graph:** the Bipartite type includes the set-problem wire change.
5. **Layered Graph, Flow Network, Table / Trace Table.**
6. **Packing, Board, Schedule, Quantum Circuit, Geometry and the rest.**
7. **Reduction view, then Search Tree.**
8. **New problems** (machines, circuits, grammars, PCP, isomorphism, QBF, Geography), if the team wants them in Redux.

---

## 7. Briefing a subagent

Subagents can't see the task list or this conversation, so put everything in the brief. Template:

```
Repo and branch: <Redux | Redux_GUI | Redux_Frontend>, new branch from <base>.
Goal: implement the <picture type> for <problems>, per Documentation/Visualizations/README.md §<n>.
Spec: open Documentation/Visualizations/mockups/design.html, tab "<tab>"; its Input contract is the
      payload; mockups/<module>.js is the reference for layout and state rules.
Files to change: <exact paths>.
Must: React-owned SVG with D3 for math only; deterministic layout; state names from §2; responsive
      rules from §4.2; light and dark mode; aria labels.
Must not: port the JS solvers into the frontend; touch other picture types; add commit footers.
Done when: <tests> pass, the replay test passes, actual endpoint JSON matches the contract,
      and screenshots at 390 px, 1080 px and 1280 px look like the mockup tab.
Report: files changed, test output, screenshots, open questions.
```

---

## 8. Working with the mockup sources

`mockups/` contains everything needed to change and rebuild the two mockup pages:

- `design.src.html` and `redux-gui.src.html`: page shells with `/*@@NAME@@*/` placeholders.
- One renderer module per picture type, listed in section 5.
- `build.js`: inlines the modules into the shells → `design.html`, `redux-gui.html`. Run `node build.js` in this folder. It fails if a placeholder is missing, and syntax-checks the result.
- `test-*.js`: Node test harnesses. Each loads its module, runs every example with every solver, and cross-checks solvers against brute force on random instances. Run `node test-<name>.js`; each prints a pass line at the end.

The published copies on claude.ai are updated by publishing the rebuilt HTML to the same artifact URLs.
