# Implementing the new visualizations

> **Status.** The backend lands in small pull requests, tracked in #655. The first two are merged: #733 (the visualize endpoint uses the chosen solver) and #734 (typed solver steps and the replay test). At the time of writing, #885 (time limit), #746 (the first Graph slice), #748 (gadget kinds) and #749 (backward map) are in review. The rest of the picture types, the Reduction View and the bug fixes follow one PR at a time. Trackers #658 (picture types), #660 (reductions) and #835 (bug fixes) list what is still open. The frontend work is tracked in ReduxISU/Redux_GUI#351 and ReduxISU/Redux_Frontend#198. This guide describes the finished design as built on the integration branch. A name it uses may not be on `CSharpAPI` yet; check the trackers.

This guide tells an agent (or a person) how the new visualizations work across the three repositories, and how to build the parts that are still missing:

- **Redux** (this repo, C# API): problems, solvers, reductions, and the JSON the frontends draw.
- **Redux_GUI** (Next.js + MUI): the current site.
- **Redux_Frontend** (Next.js): the alternative frontend. The two frontends are parallel options, not old-versus-new. Scope and file work on each separately.

The mockups were the specification. The backend now sends real frames, and the mockup renderers can draw them (see the frames viewer in §8).

| What | Where |
| --- | --- |
| How to add a visualization to a problem, and steps to a solver or reduction (contributor guide, with a DFA walkthrough) | [`ADDING-A-VISUALIZATION.md`](ADDING-A-VISUALIZATION.md) |
| How to write a custom visualization that uses none of the picture types, and what each frontend does with it (tagged "Unclassified", drawn where it can be) | [`ADDING-A-VISUALIZATION.md`](ADDING-A-VISUALIZATION.md#8-a-custom-visualization-outside-the-picture-types) §8 |
| What every picture type looks like, its problems, payload and states, with screenshots | [`PICTURE-TYPES.md`](PICTURE-TYPES.md) |
| Design mockup (every picture type, one tab each) | [`mockups/design.html`](mockups/design.html), published at https://claude.ai/artifact/KZoqj8AqhHSBJYWMVLSzkx |
| Redux_GUI mockup (the real page with every problem wired in) | [`mockups/redux-gui.html`](mockups/redux-gui.html), published at https://claude.ai/artifact/WGGHMS9UGDB8is4Bf1e4Mf |
| Frames viewer (draws real backend frames with the mockup renderers; a local debug tool) | [`viewer/README.md`](viewer/README.md) |
| Plain-language summary for people | Claude Doc "Fixing Redux's Problem Visualizations", https://claude.ai/code/artifact/eeccd33d-0a1e-4a05-836e-3ef728cc14be |
| The original proposal this builds on | `Visualization_Proposal.pdf` (shared separately; covers the Graph base, state vocabulary and TikZ retirement) |
| Issues | Backend tracker #655, with #658 (picture types), #660 (reductions), #835 (bugs); frontend trackers ReduxISU/Redux_GUI#351 and ReduxISU/Redux_Frontend#198 |

Open the two mockup HTML files directly in a browser. They work offline.

**The `mockups/` folder is temporary.** Once the new system has shipped in the backend and both frontends, #730 deletes it, and these guides point at the real code and real screenshots instead.

---

## 1. Ground rules

1. **Only build what's approved.** Each picture type has a status in the Claude Doc's "Picture types" tables (Mockup built → Approved). Don't implement a type until it's Approved. Each mockup tab ends with a "For review" or "Decisions" list; resolve those first.
2. **One picture type per pull request**, per repo. Backend first, then each frontend.
3. **Problems hand over data, never drawings.** A visualization describes its problem in a spec record (graph, rule, K). The shared frames builder decides every state, and each frontend maps states to colors. No new per-problem highlight loops.
4. **Steps come from the solver, not the visualization.** The visualization draws whatever steps the selected solver recorded. The JavaScript solvers in the mockups are reference implementations and test oracles. Don't ship them in a frontend.
5. **Names match what's implemented.** If a problem's checker disagrees with its name or description, fix the text (example: Redux's Hitting Set is Exact Hitting Set, #698).
6. **Rendering approach:** React owns the SVG elements; D3 is used only for math (`d3-force`, `d3-zoom`, `d3-shape`). No `d3.select` on the page, and no global `#id` lookups. Layouts are deterministic: the same instance always draws the same way.
7. **Repo conventions still apply:** read each repo's `CLAUDE.md` / `AGENTS.md`, read the rbs report on every PR (it can be green while failing), and don't add `Co-Authored-By` or `Claude-Session` lines to commits or PRs in Redux or Redux_GUI.
8. **Check the JSON, not just the build.** Redux serializes with `System.Text.Json` and `IncludeFields`, which leaks public fields and skips default interface members. For every new payload, call the endpoint and compare the actual JSON to the contract. The endpoint tests in `redux-tests/Endpoints/*Frames_Endpoint_Tests.cs` do this for every picture type.
9. **Old frontends keep working.** The list format of `visualize` and every `/info` field stay as they were. The new pictures are served only under `format=frames` and by `reductionView`. Snapshot tests guard this.

---

## 2. Architecture in one picture

```
 Problem (C#)               Solver (C#)                       Reduction (C#)
 instance                   Solve(problem, rec)               target + gadgets
        │                   records SolverStep<TPartial>      + build steps (BuildDelta)
        ▼                         │                           + backward map (MapBack)
 Visualization (C#)               │                                 │
 Describe(problem) → spec         │                                 │
        │                         ▼                                 ▼
 ┌──────▼───────────────────────────────────────┐  ┌────────────────────────────────────┐
 │ POST /ProblemProvider/visualize              │  │ POST /ProblemProvider/reductionView │
 │   ?visualization=…&format=frames[&solver=…]  │  │   ?reduction=…[&solver=…]           │
 │ → { type, payload, frames[] }                │  │ → { type: "Reduction", source,      │
 │   one payload shape per picture type         │  │     target, gadgets, frames[] }     │
 └──────────────────────┬───────────────────────┘  └─────────────────┬──────────────────┘
                        ▼                                            ▼
        Frontend renderer for that picture type (React SVG + D3 math)
        payload + current frame + hover  →  elements with state classes
```

**Shared state vocabulary.** Every frame names states with these words (`PictureState` in `Interfaces/Steps/Frames.cs`), and each frontend maps them to styles in one table. A frame lists only elements that are not `Background`.

| State | Meaning |
| --- | --- |
| `Background` | Nothing has happened to it yet (left out of frames) |
| `ElementHighlight` | What the solver is looking at or trying right now, including a whole candidate a brute force is testing |
| `Solution` | Part of the confirmed answer only: the Done with `ok`, or an Accept that ends the run |
| `Rejected` | Failed: falsified clause, rejecting state, uncovered or doubled element, a wrong final answer |
| `Untraveled` | Never reached during this run, or not needed |
| `Covered` | A constraint that is met (element covered, set hit, clause satisfied) |
| `Blocked` | Can't be chosen without breaking an exactly-once rule (or independence) |
| `False` | An assigned-false literal or variable. An unassigned one stays `Background` |
| `Committed` | Kept so far but not confirmed: queens or digits placed, items packed, tree edges chosen, a best so far (#886). Drawn a lighter green with a dashed outline |

**Green is for answers.** A brute force that judges whole candidates draws each candidate `ElementHighlight`, never `Solution`. A solver that builds its answer piece by piece declares `IIncrementalSolver`; its committed pieces are `Committed` (a lighter green with a dashed outline) and only the piece under test is highlighted. In every picture, `Solution` is kept for the confirmed answer (#886), and a run that ends without an answer draws its picks `Rejected`.

Badges such as ×2 (covered twice), order numbers in a tour, missing-edge lines and group colors are drawn annotations, not states.

---

## 3. Backend in Redux

The pieces below exist on the integration branch. Read the code before changing it; this section says where things are.

### 3.1 The endpoints

All are on `AdditionalControllers/ProblemProvider.cs`. The body is always the instance string (JSON-encoded).

| Endpoint | Parameters | Returns |
| --- | --- | --- |
| `POST visualize` | `visualization`, optional `solver` (must solve the same problem; else 400 `solver_mismatch`), optional `format` = `list` (default) or `frames` | `list`: the old flat list `[initial, ...steps, solved]`. `frames`: `{ type, payload, frames }`. A visualization with a frames hook (`IFramesVisualization`) returns its own picture; any other gets its legacy items wrapped. |
| `POST visualizeRun` | `solver` (class name), optional `visualizations=A,B` (class names; default: every visualization of the solver's problem) | One solver run, every picture (#895): `{ solver, solverName, answer, ok, timedOut, pictures: [{ visualization, type, payload, frames }], unavailable: [{ visualization, reason, detail }] }`, the default visualization first. `ok` is the problem's verifier on `answer` (null when the run was stopped or the verifier can't read it). A visualization it can't draw is in `unavailable` with `no_frames` (no frames hook), `shape_mismatch` (it can't draw this solver's steps, nor the answer alone) or `solver_mismatch`. Errors as for `visualize`. |
| `GET info?interface=…` | | For a visualization, also `pictureType`: the picture it serves under `format=frames`, or null for a legacy one (#841) |
| `POST reductionView` | `reduction`, optional `solver` (a TARGET solver) | The Reduction View (§3.5). 400 `no_build_steps`, `no_backward_map`, `no_source_picture` or `no_target_picture` when a reduction can't be shown |
| `POST buildSteps` | `reduction` | `{ steps }`: the construction steps (§3.5) |
| `POST mapSolutionBack` | `reduction`, `solution` (the TARGET's certificate), optional `steps=true` | The source's answer, or `{ answer, steps }` |
| `POST gadgets` | `reduction` | The gadget list, with `gadgetKind`, `sourceIds`, `targetIds`, `note` beside the legacy `color` / `reductionFromIds` / `reductionToIds` |

`visualize` solves once and reuses the run for the steps and the solved picture (`ISolver.Run`; `SolveOnce_Tests` checks it). `visualizeRun` goes further: it solves once and builds every picture of the problem from that one run, so a solver that makes random choices (PPZ, Schöning, WalkSAT) shows the same run in each picture.

**The frames response.** `{ type, payload, frames }`. The payload alone is the base picture, shown before Run: `frames[0]` is the solver's first step, not a base frame. Every frame carries `step` (#895), always written: the 0-based index of the solver step it was drawn from, counted in the run's full step list. It is null for a frame drawn from no step: the solved frame a builder adds from the answer, and the blank stopped frame it adds when a stopped run's own Done step can't be drawn (a drawn stopped Done keeps its index). Pictures built from one run line up by `step`, not by frame position, since a picture may add a closing frame. A picture that can't draw the solver's steps (another `StepShape`) draws the answer alone, one Done frame with `step: null`; the Search Tree can't, so `visualizeRun` lists it as `shape_mismatch`. The generic wrapping of a legacy visualization (no frames hook) has no `step`.

**Time limit.** A solve that records steps for a picture has a budget, `Visualization:SolveTimeLimitSeconds` in `appsettings.json` (default 10; zero or less means none). A typed solver past the budget is stopped at its next recorded step. The run ends with a Done step, `ok: false`, whose caption starts "Stopped after …", and the picture draws a blank stopped frame. Legacy solvers can't be stopped this way (#523).

**Step cap.** `StepRecorder` keeps the first 150 decisions (`StepRecorder.DefaultCap`), then counts the rest. Past the cap it still keeps the last Accept, placed just before Done, so a brute force that finds its answer late still shows it. Done is always kept, and its caption ends "(n not shown)" when steps were dropped.

### 3.2 Typed solver steps

`Interfaces/Steps/` holds the step types:

- `SolverStep<TPartial>(Partial, Event, Focus, Caption)`, plus `Ok` (on Done only) and `Tree` (search-tree patches, optional). JSON: `partial`, `event`, `focus`, `caption`, `ok`, `tree`.
- `StepEvent { Try, Accept, Reject, Backtrack, Done }`.
- `StepRecorder<TPartial>`: `Try`, `Accept`, `Reject`, `Backtrack` take lambdas for the partial and the caption, so nothing is built when recording is off. `Done(answer, ok, caption)` closes the run. `StepRecorder<T>.Off` records nothing.
- The answer shapes in `Shapes.cs`, one per kind of answer, not per picture: `NodeSet`, `EdgeSet`, `Groups`, `Tour`, `Assignment`, `ChosenSets`, `ActiveStates`, `Distances`, `Components`, `NetworkFlow`, `Placement`, `TableCells`, `BoardCells`, `JobOrder`, `PumpPlan`, `ChosenTriples`, `HullBuild`, `HuffmanForest`, `QuantumProgress`, and `BuildDelta` for reductions.

A solver implements `ISolver<TProblem, TPartial>` and writes one method, `string Solve(TProblem problem, StepRecorder<TPartial> rec)`. The plain `solve` runs it with the recorder off. A solver that builds its answer piece by piece also implements the marker `IIncrementalSolver` (§2). `SolverReplay_Tests` finds every typed solver by reflection and checks that its last step is Done and that its partial matches the answer `solve` returns.

### 3.3 Picture types and the manifest

- `Interfaces/VisualizationType.cs` is checked by CI against `Documentation/visualization-types.json`. Redux_GUI keeps a vendored copy (`components/Visualization/svgs/visualizationTypes.json`) cross-checked daily. Add new values in all three places in the same change set.
- The new types are `Graph`, `GroupedGraph`, `BipartiteGraph`, `Automaton`, `LayeredGraph`, `FlowNetwork`, `Packing`, `Table`, `Board`, `Schedule`, `TripartiteMatching`, `Geometry`, `CodeTree`, `QuantumCircuit`, `SearchTree` and `Reduction`. Two more are proposed and not built: `Formula` and `NestedSet` (§5). They appear only as the `type` of a frames response (and as `pictureType` in `/info`). Each visualization keeps its legacy `visualizationType` in `/info`.
- Every payload carries a `kind` discriminator (see #524). It is not always the type name: an automaton's `kind` is `dfa` or `nfa`.

### 3.4 Shared frames builders

Each picture type has one interface in `Interfaces/` that builds the frames. A visualization implements it and returns a spec record from `Describe`; the frames hook comes for free. For example `IGraphSubsetVisualization<CLIQUE>` with `GraphSubsetSpec(nodes, edges, K, SubsetRule.Clique)`. [PICTURE-TYPES.md](PICTURE-TYPES.md) lists each interface, its spec and its payload; [ADDING-A-VISUALIZATION.md](ADDING-A-VISUALIZATION.md) shows how to write one. A visualization that uses none of these interfaces is possible too: see [ADDING-A-VISUALIZATION.md §8](ADDING-A-VISUALIZATION.md#8-a-custom-visualization-outside-the-picture-types).

Graph input rules that every builder follows:

- **Edge ids are text:** `a-b` for an undirected edge (ends in ordinal order), `a>b` for a directed one. Separator characters inside a name are escaped with a backslash, so ids stay unique. The payload lists both ends (`a`, `b`), so a renderer never splits an id.
- **Parallel edges** become one drawn edge per pair, with every weight in `label` (`"2,5"`). Cut problems sum the weights; path, tour and tree problems use the lightest. Exact duplicates are dropped, and in an undirected graph a reversed identical copy is a duplicate too. Minimum S-T Cut adds parallel capacities. DFA and NFA merge transitions between two states into one arrow labelled `a,b`.
- **Loops:** undirected graph problems refuse an edge from a node to itself with a clear 400 (#836, #882). The directed feedback problems, SSSP and SPSP accept loops.

### 3.5 Reductions and the Reduction View

- **Gadgets** (`Interfaces/JSON_Objects/Gadget.cs`): each has a `gadgetKind` from `GadgetKind` (`element`, `group`, `edgeRule`, `palette`, `orGadget`, `bound`), the stable `sourceIds` and `targetIds` that each side's own picture uses, and an optional `note`. The legacy `color`, `reductionFromIds` and `reductionToIds` stay for Redux_GUI.
- **Construction steps:** a reduction implements `IRecordedReduction<T, U>`. Its build code calls a `StepRecorder<BuildDelta>` while it really builds the target. Each step is a delta: the nodes, edges and skipped pairs it added, the gadgets it built, and `SourceFocus`. Every step is recorded; past 150 the rest fold into one combined step, so replaying always gives the whole target.
- **Backward map:** a reduction implements `IReversibleReduction<T, U, TPartial>` and writes `MapBack(targetAnswer, rec)`, which returns the source's answer and records one step per target element read. The interface adds the Done step and checks the result with the source's verifier.
- **The Reduction View** (`Interfaces/Steps/ReductionView.cs`) needs both, plus a frames picture on each side. It plays three phases in one frame list: `build`, `solve` (the target's solver) and `map`. Each frame carries `phase`, `event`, `caption`, `step`, `sourceFocus`, `targetFocus` and the two panes' states. `step` counts within the frame's own phase (construction steps, the solver run, the backward map's steps), null on a frame the view adds. Only the last frame is a Done with `ok` (#840).
- **Which solver:** `IReduction.ReductionViewSolver()` names the solver the view runs when the caller names none (3SAT → Graph Coloring and 3SAT → 3-Dimensional Matching name their backtracking solvers, #814). Otherwise it is the target's default solver. `?solver=` overrides both.
- 20 reductions have a Reduction View today; [PICTURE-TYPES.md](PICTURE-TYPES.md#reduction) lists them.

### 3.6 Bugs found along the way

They are filed as separate issues, tracked in #835 (continued from #661): for example Convex Hull returning non-hulls on repeated x values, Cut accepting edges that don't split the graph, and 3-Dimensional Matching accepting incomplete answers. They are not part of the picture work. Check #835 for what is still open.

---

## 4. Frontend work (both sites)

The two frontends share the same contract but not code. Build each separately. A framework-free module of layouts and state rules could be shared later if the team wants that.

### 4.1 How to port one mockup module

Each mockup module (`mockups/*.js`) has the same four parts. Treat them differently:

| Part in the mockup | What to do with it |
| --- | --- |
| `parse()` | Don't port. The backend parses and sends the payload. Keep it only for tests. |
| Solvers (`solve…`, `trace…`, `SOLVE`) | Don't port. The backend records the steps. Use them only as a test oracle. |
| Layout (`layout()`, ring, layers, columns, BFS ranks) | Port as a pure function `(payload) → positions`. Keep it deterministic. Some payloads already carry layers or rows (Automaton, Layered Graph, Flow Network). |
| Paint (`paint()`, `evaluate()`, CSS classes) | Port as a React component: `(positions, frame, hover) → <svg>` with state class names. Read states from the frame; don't recompute them. Move colors into the site theme. |

The viewer's adapters (`viewer/adapters/*.js`) show, per picture type, how the real backend JSON maps onto each mockup renderer. They are the quickest way to see what a renderer must read.

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
- **Contrast, in both themes, for every picture and every screenshot:**
  - Text needs at least 4.5:1 against what's behind it (3:1 at 24 px, or 18.66 px bold).
  - Edges, outlines, wires, bars, markers and control borders need at least 3:1.
  - No light grey on a white background, and no dark grey on a dark one.
  - Fade things by changing the color, never the opacity:
    - **Dimmed** (not part of this step) uses the `edge-dim` token, which still meets 3:1.
    - **Faint** (not involved in what you're hovering) turns grey with `filter: grayscale(1)` and gets a dashed outline.
  - Text on a solid state fill uses the matching `on-` token: `on-sol` is white in light mode and near-black in dark mode, because the dark-mode greens and reds are bright.
  - Group colors keep their bright fill, but outlines and text use the darker `g0s`–`g5s` and `g0i`–`g5i` variants in light mode.
  - The token values are in the `:root` blocks of `mockups/design.src.html`.
  - `mockups/contrast/` and `viewer/contrast.mjs` check all of this automatically (§8).
- **Accessibility:** focusable nodes with `aria-label`s, `role="img"` with a label on each diagram, visible focus rings, and `prefers-reduced-motion` respected.

### 4.3 Redux_GUI specifics

- **Where things are:**
  - `components/pageblocks/VisualizeRowReact.js`: the Visualize section and its controls (Refresh, step navigation, the Show reduction / Highlight gadgets / Highlight solution switches).
  - `components/widgets/VisualizationLogic.js`: chooses a renderer by visualization type.
  - `components/Visualization/svgs/Visualizations.js`: the type registry.
  - `components/Visualization/ReducedVisualization.js` and `components/redux/index.js`: reduction view plumbing.
- **Replace `StandardGraphSvgReact.js`; don't extend it.** It renders with imperative D3 and uses page-wide `d3.selectAll("#id…")` for gadget hover, which breaks when two graphs are mounted. It also runs `d3.scaleLinear` over string edge weights such as `"a,b"`.
- **The Solve section's selected solver** already reaches VisualizeRow (`chosenSolver`). Pass it as `solver=` to the visualize request.
- **Pick the renderer from `pictureType`** in `/info`, and request `format=frames` for those visualizations.
- **Highlight gadgets** is currently gated on a hard-coded `#highlightGadgets` checkbox id. Drive it from state instead.
- **Retire TikZ last:** once DFA, NFA and Clique's LaTeX view are on the new renderers, remove `LaTeXGraphSvgReact.js` and `node-tikzjax`. Its `svgo` dependency carries the advisories listed in the proposal.
- **Match the mockup's GUI page** (`mockups/redux-gui.html`): visualization picker with type tags in Title Case ("Grouped Graph"), second views ("Factor Graph", "Trace Table", "Search Tree", and the proposed "Formula" and "Nested Set") as extra picker entries, and menu groups by class.
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

## 5. Picture types: backend interface and mockup source

Each row lists the response `type`, the backend interface, the mockup module and what is special. [PICTURE-TYPES.md](PICTURE-TYPES.md) has the payloads and states.

| Picture type (`type`) | Problems | Backend interface | Mockup module | Notes |
| --- | --- | --- | --- | --- |
| Graph (`Graph`) | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set; Minimum Spanning Tree, Steiner Tree; Cut, Weighted Cut, Max Cut, Minimum Cut; Graph Coloring, Clique Cover; Hamiltonian Cycle, Directed Hamiltonian Cycle, Traveling Salesperson, Minimum Traveling Salesperson | `IGraphSubsetVisualization`, `IGraphEdgesVisualization`, `IGraphCutVisualization`, `IGraphGroupsVisualization`, `IGraphTourVisualization` | `graphbase.js` | Five shapes: subset, edges, cut, groups, tour. Cut and groups stay separate shapes. Directed Hamiltonian Cycle is a directed tour. |
| Grouped Graph (`GroupedGraph`) | SAT, 3SAT | `IGroupedGraphVisualization` | inline in `design.src.html` and `redux-gui.src.html` | Clusters on a ring, complement edges, `False` literals. |
| Bipartite Graph (`BipartiteGraph`) | Set Cover, Exact Cover, Hitting Set (exactly once); SAT and 3SAT Factor Graph | `IBipartiteVisualization`, `IFactorGraphVisualization` | `bipartite.js` | Two columns, `pickSide`, hits counts. A new node-link payload; the old set payload stays in the list format. |
| Formula (`Formula`) | SAT, 3SAT, as an additional selectable view (`SatFormulaVisualization`, `Sat3FormulaVisualization`) | `IFormulaVisualization`, `IFormulaExprVisualization` | `formula.js` | Clause boxes joined by ∧, literals by ∨, an assignment strip. The Grouped Graph's spec, ids and frames, plus a `variables` map. Also draws both sides of SAT → 3SAT in the Reduction View. Not approved yet. *Generalization (approved Oct 21, built in the backend, #929):* the payload drives the drawing with no problem-specific frontend code: `layout` `rows` (with `cnf`/`dnf` as presets), `equation` (`tree` reserved); groups hold a literal list or an expression tree with an id on every piece; operators by name from one table (unknown names drawn `name(a, b)`); typed variables (bool or int); frame `note` per group and `exprs` per group. Mocked for DNF (illustration); built as opt-in second views of Prime Factorization, 0-1 ILP and Subset Sum (`IFormulaExprVisualization`). Every Formula view is listed only with `includePictures=true`, so Redux_GUI does not see it. No text floats in the picture (no "FORMULA · CNF" heading; the legend is outside). See [PICTURE-TYPES.md](PICTURE-TYPES.md#formula-generalized). |
| Nested Set (`NestedSet`) | Set Cover, Exact Cover, Hitting Set, as an additional selectable view (`SetCoverNestedSetVisualization`, `ExactCoverNestedSetVisualization`, `HittingSetNestedSetVisualization`) | *proposed:* `INestedSetVisualization` | `nestedset.js` | Dots on a circle, each set a padded hull; falls back to a set list past 12 elements, 8 sets or 4 overlaps per element. The Bipartite spec, ids and frames, without edges. Not approved yet. |
| Automaton (`Automaton`) | DFA, NFA | `IAutomatonVisualization` | `automaton.js` | Layers from the start state, merged arrows, Garbage state for a DFA's missing moves, NFA runs for the one-path picker. |
| Layered Graph (`LayeredGraph`) | Topological Sort, Feedback Arc Set, Feedback Node Set, Strongly Connected Components, SSSP, SPSP | `ILayeredVisualization` | `layered.js` | Ranked left to right, back edges marked, distances under nodes. |
| Flow Network (`FlowNetwork`) | Minimum S-T Cut | `IFlowVisualization` | `flow.js` | Source left, sink right, flow/capacity, one augmenting path per step, S/T sides and cut at the end. |
| Packing (`Packing`) | Partition, Subset Sum, Knapsack, Bin Packing | `IPackingVisualization` | `packing.js` | Items to scale, containers with target or limit lines. |
| Board (`Board`) | Sudoku, N-Queens | `IBoardVisualization` | `board.js` | Clues, placed values, conflict links. Green only for the confirmed answer. |
| Table (`Table`) | Edit Distance (grid), 0-1 Integer Programming (matrix); trace tables for DFA, NFA, SSSP, SPSP | `ITableVisualization` | `table.js` (HTML, not SVG) | The visualization fills the cells; the base adds focus and ids. |
| Schedule (`Schedule`) | Job Sequencing, Pump Scheduling (both) | `IJobScheduleVisualization`, `IPumpScheduleVisualization` | `schedule.js` | One type, told apart by `payload.rule`. |
| Quantum Circuit (`QuantumCircuit`) | Deutsch, Deutsch-Jozsa, Bernstein-Vazirani, Simon, Unstructured Search, Prime Factorization | `IQuantumCircuitVisualization` | `quantum.js` | One gate-list payload. Measured qubits high bit first. Shor's circuit is drawn, not simulated. |
| Tripartite Matching (`TripartiteMatching`) | 3-Dimensional Matching | `ITripartiteVisualization` | `misc.js` | Triples as lines through three columns. |
| Geometry (`Geometry`) | Convex Hull | `IGeometryVisualization` | `misc.js` | Hull pieces, merge chain, dropped points. Green only for the confirmed hull. |
| Code Tree (`CodeTree`) | Lossless Data Compression | `ICodeTreeVisualization` | `misc.js` | Huffman merges bottom-up, codes and bits. |
| Search Tree (`SearchTree`) | Second view for 3SAT, Graph Coloring, TSP, N-Queens, 3-Dimensional Matching | `ISearchTreeVisualization` | `searchtree.js` | Built from `SolverStep.Tree` patches. Folds past 12 leaves. |
| Reduction (`Reduction`) | 20 reductions | `ReductionView` (endpoint `reductionView`) | `reduction.js` | Three phases, gadget map, cross-pane hover. |
| Tape and stack machines | Turing machine, LBA, PDA | *planned* (#662) | `machine.js` | The problems aren't in Redux yet. |
| Boolean Circuit, Grammar | Circuit Value, Circuit-SAT, CFG membership | *planned* (#662) | `circuitgrammar.js` | The problems aren't in Redux yet. |
| Dominoes, Two-Graph Mapping | Post Correspondence, Graph Isomorphism, Subgraph Isomorphism | *planned* (#662) | `pairview.js` | The problems aren't in Redux yet. |
| Game Tree | QBF, Generalized Geography | *planned* (#662) | `searchtree.js` | `SearchNodeState.Skipped` exists for it; no solver uses it yet. |

Problems marked planned need a new problem class first (PR 7, #662), with instance formats as proposed on their mockup tabs. Redux's `ComplexityClass` has no value for undecidable or PSPACE-complete problems yet.

---

## 6. What is still planned

- **Merging the rest:** the picture types, the Reduction View and the fixes reach `CSharpAPI` one small PR at a time (#655, #658, #660, #835).
- **Certificate pictures:** draw a certificate the user typed, and show where it fails (#853). There is no endpoint for this yet.
- **New problems** for the planned picture types above (#662).
- **Removing the old list format** of `visualize` once both frontends read frames (#732).
- **Frontend renderers** in Redux_GUI and Redux_Frontend, then retiring TikZ (§4.3).
- **Deleting `mockups/`** once everything has shipped (#730).

---

## 7. Briefing a subagent

Subagents can't see the task list or this conversation, so put everything in the brief. Template:

```
Repo and branch: <Redux | Redux_GUI | Redux_Frontend>, new branch from <base>.
Goal: implement the <picture type> for <problems>, per Documentation/Visualizations/README.md §<n>.
Spec: PICTURE-TYPES.md "<type>" for the payload and states; open
      Documentation/Visualizations/mockups/design.html, tab "<tab>", for the look;
      mockups/<module>.js and viewer/adapters/<type>.js for layout and how the JSON maps.
Files to change: <exact paths>.
Must: React-owned SVG with D3 for math only; deterministic layout; state names from §2; responsive
      rules from §4.2; light and dark mode; contrast rules; aria labels.
Must not: port the JS solvers into the frontend; touch other picture types; add commit footers.
Done when: <tests> pass, the replay test passes, actual endpoint JSON matches the contract,
      and screenshots at 390 px, 1080 px and 1280 px look like the mockup tab.
Report: files changed, test output, screenshots, open questions.
```

---

## 8. Working with the mockup sources and the viewer

`mockups/` contains everything needed to change and rebuild the two mockup pages:

- `design.src.html` and `redux-gui.src.html`: page shells with `/*@@NAME@@*/` placeholders.
- One renderer module per picture type, listed in §5. Some have small "viewer hooks" so they can draw backend frames.
- `build.js`: inlines the modules into the shells → `design.html`, `redux-gui.html`. Run `node build.js` in this folder. It fails if a placeholder is missing, and syntax-checks the result.
- `test-*.js`: Node test harnesses. Each loads its module, runs every example with every solver, and cross-checks solvers against brute force on random instances. The `test-*-draw.js` ones draw a renderer into a tiny DOM (`dom-shim.js`) and check what it drew. Run `node test-<name>.js`; each prints a pass line at the end.
- `contrast/`: color contrast audits.
  - `audit-design.js` clicks through every tab, problem, example, view, step and toggle of `design.html`.
  - `audit-gui.js` does the same for every problem and visualization in `redux-gui.html`, including hover tracing.
  - Both run in light and dark mode, apply the rules in §4.2, and exit 1 with one line per failing element kind.
  - Run them from `mockups/`. They need Playwright: install it, or set `PLAYWRIGHT` to the full path of an installed copy (Redux_GUI has one in its `node_modules`).
  - A few marks are exempt and listed in `audit-lib.js`: label backing plates, guide grids, panel separators and gaps cut in the surface color. So are disabled controls.

`viewer/` is a local page that calls a running backend with `format=frames` (and `reductionView`) and draws the real frames with the mockup renderers. It covers every picture type and the Reduction View. Its README explains how to run it, its Playwright smoke test and its contrast audit.

The published copies on claude.ai are updated by publishing the rebuilt HTML to the same artifact URLs.

The screenshots in `images/` (used by [PICTURE-TYPES.md](PICTURE-TYPES.md)) are taken from `design.html` at 1,280 px in the light theme. Some predate later mockup changes; PICTURE-TYPES.md notes where the picture now differs. Retake the affected ones when a mockup changes, after the contrast audits pass, and replace them with real-site screenshots as each frontend implements a type.
