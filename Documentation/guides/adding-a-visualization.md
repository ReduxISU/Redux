# Adding a visualization

This guide walks you through adding a new **visualization** to Redux, from an empty file to a green pull request (PR). A visualization is the one place where work can span **two repositories**: the Redux API (this repo) produces the data, and the Redux_GUI website draws it.

New to the repo? Read the [guides index](README.md) first. When you reach the "build and test" step, the details live in [building-and-testing.md](building-and-testing.md).

Some words you will see:

- **Visualization**: a picture of a problem instance (and, optionally, its solution). In the API it is a C# class. In the GUI it is a React component that draws SVG shapes.
- **Payload**: the JSON data the API sends to the GUI so the GUI can draw. The API builds it from small C# classes called "JSON objects" (for example `API_GraphJSON`).
- **Renderer**: the GUI code that turns one kind of payload into a picture. One renderer can draw many problems.
- **Visualization type**: a short name (`GraphD3`, `SetD3`, ...) that says which renderer should draw a visualization. It is the **contract** between the two repos.
- **Manifest**: a plain list of those type names, kept as a JSON file so both repos can check themselves against it.

---

## 1. What a visualization is

Everyday analogy: a visualization is like a recipe card handed from a kitchen (the API) to a waiter (the GUI). The kitchen does the cooking: it works out which nodes exist, which one is colored as part of the solution, and so on. The card says which plate to use (the **visualization type**). The waiter never cooks. It only plates the food the way that plate is meant to be served.

In Redux:

- A visualization is one C# class. It takes a problem instance (a string such as `(({a,b,c},{{a,b},{b,c}}),2)`) and returns a **payload**.
- It can produce three kinds of payload. Everything the GUI shows comes from these:
  1. `visualize`: the plain picture of the instance.
  2. `SolvedVisualization`: the same picture with the solution highlighted. Optional.
  3. `StepsVisualization`: one picture per solver step, for animations. Optional.
- It declares a `visualizationType`. The GUI looks that name up in its list of renderers and uses the matching one.
- You do not write a controller or register anything on the API side. Redux finds every visualization class automatically (see section 3).

### Which case are you in?

| Case | Example | What you touch |
| --- | --- | --- |
| **(a) A new problem that reuses an existing visualization type** (the common case) | A new graph problem drawn with the existing graph renderer | **Redux only.** Section 4. |
| **(b) A brand-new visualization type** (rare) | A map or chessboard view that no current renderer can draw | **Both repos**, in a set order. Section 5. |

If you are not sure, you are almost certainly in case (a). Look at the list of existing types in section 2. If one of them can draw your payload, use it.

## 2. The existing visualization types

The list of types lives in [VisualizationType.cs](../../Interfaces/VisualizationType.cs). Each one maps to a payload shape (a C# JSON object in [Interfaces/JSON_Objects/](../../Interfaces/JSON_Objects/)) and a GUI renderer.

| `visualizationType` | Payload class to return | Drawn by (in Redux_GUI) | Used by, for example |
| --- | --- | --- | --- |
| `GraphD3` | [`API_GraphJSON`](../../Interfaces/JSON_Objects/Graphs/API_GraphJSON.cs) | [StandardGraphSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardGraphSvgReact.js) | Independent Set, Max Cut, Vertex Cover |
| `GraphLaTeX` | `API_GraphJSON` | [LaTeXGraphSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/LaTeXGraphSvgReact.js) | DFA |
| `Graph` | not a list item: a `payload` plus `frames` of named states ([`GraphSubsetFrames`](../../Interfaces/graphs/GraphSubsetFrames.cs), [`GraphEdgeFrames`](../../Interfaces/graphs/GraphEdgeFrames.cs), [`GraphGroupFrames`](../../Interfaces/graphs/GraphGroupFrames.cs), [`GraphTourFrames`](../../Interfaces/graphs/GraphTourFrames.cs)) | none yet | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set; Minimum Spanning Tree, Steiner Tree; Cut, Weighted Cut, Max Cut, Minimum Cut; Graph Coloring, Clique Cover; Hamiltonian Cycle, Directed Hamiltonian Cycle, Traveling Salesperson, Minimum Traveling Salesperson. Reported only as the `type` of `visualize?format=frames`; these visualizations still report `GraphD3` in /info and in the default list format. See "The Graph picture" below |
| `GroupedGraph` | not a list item: a `payload` plus `frames` of named states ([`GroupedGraphFrames`](../../Interfaces/graphs/GroupedGraphFrames.cs)) | none yet | SAT, 3SAT (the clause graph). Reported only as the `type` of `visualize?format=frames`; these visualizations still report `BooleanSatisfiability` in /info and in the default list format. See "The Grouped Graph picture" below |
| `BipartiteGraph` | not a list item: a `payload` plus `frames` of named states ([`BipartiteFrames`](../../Interfaces/graphs/BipartiteFrames.cs)) | none yet | Set Cover, Exact Cover, Hitting Set (exact). Reported only as the `type` of `visualize?format=frames`; these visualizations still report `SetD3` in /info and in the default list format. See "The Bipartite Graph picture" below |
| `Automaton` | not a list item: a `payload` plus `frames` of named states ([`AutomatonFrames`](../../Interfaces/graphs/AutomatonFrames.cs)) | none yet | DFA, NFA. Reported only as the `type` of `visualize?format=frames`; these visualizations still report `GraphLaTeX` in /info and in the default list format. See "The Automaton picture" below |
| `LayeredGraph` | not a list item: a `payload` plus `frames` of named states ([`LayeredFrames`](../../Interfaces/graphs/LayeredFrames.cs)) | none yet | Topological Sort, Feedback Arc Set, Feedback Node Set, Strongly Connected Components, SSSP, SPSP. Reported only as the `type` of `visualize?format=frames`; these visualizations still report `GraphD3` (or `DynamicTable` for the tables, which are unchanged) in /info and in the default list format. See "The Layered Graph picture" below |
| `FlowNetwork` | not a list item: a `payload` plus `frames` of named states ([`FlowFrames`](../../Interfaces/graphs/FlowFrames.cs)) | none yet | Minimum S-T Cut. Reported only as the `type` of `visualize?format=frames`; its visualization still reports `GraphD3` in /info and in the default list format. See "The Flow Network picture" below |
| `Packing` | not a list item: a `payload` plus `frames` of named states ([`PackingFrames`](../../Interfaces/Packing/PackingFrames.cs)) | none yet | Partition, Subset Sum, Knapsack, Bin Packing. Reported only as the `type` of `visualize?format=frames`; these visualizations still report `SetD3` in /info and in the default list format. See "The Packing picture" below |
| `Table` | not a list item: a `payload` plus `frames` of cell values and named states ([`TableFrames`](../../Interfaces/Table/TableFrames.cs)) | none yet | Edit Distance, 0-1 Integer Linear Programming; the DFA and NFA run tables, the SSSP and SPSP tables. Reported only as the `type` of `visualize?format=frames`; these visualizations still report `DynamicTable` in /info and in the default list format. See "The Table picture" below |
| `Board` | not a list item: a `payload` plus `frames` of named states ([`BoardFrames`](../../Interfaces/Board/BoardFrames.cs)) | none yet | Sudoku, N-Queens. Reported only as the `type` of `visualize?format=frames`; both visualizations report `DynamicTable` in /info, and their default list format is a table (Sudoku's grid; each N-Queens row's queen). See "The Board picture" below |
| `Schedule` | not a list item: a `payload` plus `frames` of named states ([`ScheduleFrames`](../../Interfaces/Schedule/ScheduleFrames.cs)) | none yet | Job Sequencing, Pump Scheduling Cost Minimization and Emergency Resilience. Reported only as the `type` of `visualize?format=frames`; the pump visualizations still report `PumpSchedule` and Job Sequencing's reports `DynamicTable` in /info and in the default list format. See "The Schedule picture" below |
| `TripartiteMatching` | not a list item: a `payload` plus `frames` of named states ([`TripartiteFrames`](../../Interfaces/Tripartite/TripartiteFrames.cs)) | none yet | 3-Dimensional Matching. Reported only as the `type` of `visualize?format=frames`; its `DM3Visualization` reports `DynamicTable` in /info and in the default list format. See "The Tripartite Matching picture" below |
| `Geometry` | not a list item: a `payload` plus `frames` of named states ([`GeometryFrames`](../../Interfaces/Geometry/GeometryFrames.cs)) | none yet | Convex Hull. Reported only as the `type` of `visualize?format=frames`; `ConvexHullVisualization` reports `DynamicTable` in /info, and its default list format is a table of the points. See "The Geometry picture" below |
| `CodeTree` | not a list item: a `payload` plus `frames` of named states ([`CodeTreeFrames`](../../Interfaces/CodeTree/CodeTreeFrames.cs)) | none yet | Lossless Data Compression. Reported only as the `type` of `visualize?format=frames`; `LosslessDataCompressionVisualization` reports `DynamicTable` in /info, and its default list format is the code table. See "The Code Tree picture" below |
| `QuantumCircuit` | not a list item: a `payload` plus `frames` of named states ([`QuantumCircuitFrames`](../../Interfaces/Quantum/QuantumCircuitFrames.cs)) | none yet | Deutsch, Deutsch-Jozsa, Bernstein-Vazirani, Simon's Problem, Unstructured Search, Prime Factorization. Reported only as the `type` of `visualize?format=frames`; the quantum visualizations still report `QuantumCircuitD3` or `QuantumCircuitQjs` in /info and in the default list format. See "The Quantum Circuit picture" below |
| `SearchTree` | not a list item: a `payload` plus `frames` of named states ([`SearchTreeFrames`](../../Interfaces/SearchTree/SearchTreeFrames.cs)) | none yet | A second view of a solver that branches and backs up (see "The Search Tree picture" below). Reported only as the `type` of `visualize?format=frames`; each search-tree visualization reports its problem's legacy type in /info and serves that picture's list format |
| `SetD3` | [`API_SET`](../../Interfaces/JSON_Objects/API_SET.cs) | [StandardSetSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardSetSvgReact.js) | Set Cover, Exact Cover, Partition |
| `BooleanSatisfiability` | [`API_SAT`](../../Interfaces/JSON_Objects/API_SAT.cs) | [StandardSATSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardSATSvgReact.js) | SAT, 3SAT |
| `QuantumCircuitD3` | [`API_QUANTUMCIRCUIT`](../../Interfaces/JSON_Objects/API_QUANTUMCIRCUIT.cs) | [StandardCircuitSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardCircuitSvgReact.js) | Deutsch (D3 view) |
| `QuantumCircuitQjs` | `API_QUANTUMCIRCUIT` | [QuantumCircuitVis.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/QuantumCircuitVis.js) | Deutsch, Shor's |
| `PumpSchedule` | a pump-frame class defined next to the visualization | [PumpSchedulingSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/PumpSchedulingSvgReact.js) | Pump Scheduling |
| `DynamicTable` | [`API_TableJSON`](../../Interfaces/JSON_Objects/Tables/API_TableJSON.cs) | [DynamicTableSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/DynamicTableSvgReact.js) | DFA table view, shortest paths |
| `Unimplemented` | `API_empty` | nothing, on purpose | Only `DummyVisualization`, the stand-in for a problem with no picture yet |

/info also gives every visualization a `pictureType`, right after `visualizationType`: the picture type it serves under `visualize?format=frames` (the `type` of that response, such as `Graph`), or `null` when it serves only the older formats. It comes from the frames hook (`IFramesVisualization.PictureType()`, which reads it off a frames response for the problem's default instance), so a new picture type needs nothing extra for it. Clients read it instead of running each visualization to find out.

Two things that trip people up:

- Every payload class carries a `kind` field (`"graph"`, `"sat"`, `"set"`, `"table"`, `"empty"`, ...). It tells a reader what the JSON is without guessing. You do not set it. The class does.
- `Unimplemented` means "no renderer exists". The GUI shows an honest "not renderable" message instead of a broken picture. A few old classes use it, and a test (section 6) stops **new** classes from joining them.

### The Graph picture (`format=frames`)

A graph problem whose answer is a set of nodes, a set of edges, a split of the nodes in two, a split into groups, or a tour in visiting order can also serve the shared `Graph` picture. This is additive: you keep `visualize` and `SolvedVisualization` exactly as they are (old frontends read those), and add one interface plus a description of the problem. The solver records typed steps (see `StepRecorder`), and the base derives every picture from the step's partial answer, so a greedy and a brute-force solver animate on the same picture.

The payload (sent once) is `{ kind: "graph", subtype: "undirected" | "directed", layout: "force" | "circle", nodes, edges, shape, rule, k? }`. Edge ids are `"a-b"` with `a < b` (ordinal), except in a directed graph (`"a>b"`, see the `tour` row). An id is a label, never something to split: every payload edge gives its two ends as `a` and `b`. A `-`, `>` or `\` inside a node name is written with a `\` before it, so ids stay unique (nodes `n-1`, `n-2` give `n\-1-n\-2`; nodes `n`, `1-n-2` give `1\-n\-2-n`) and ordinary names are unchanged. Build ids with `GraphSubsetFrames.EdgeId`, never by hand. Each frame is `{ event, caption, focus, ok?, nodes, edges, phantoms, groups?, order? }` where `nodes` and `edges` map an id to `{ state }`; an id that is absent is `Background`. When several states apply to one element the order is `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`. How a step's partial is drawn depends on the kind of solver, which the solver declares. A solver that judges whole candidates (a brute force) records each candidate as the partial of a `Try` or `Reject` step; such a candidate is never `Solution`: what it picks is `ElementHighlight`, and `Solution` is for what the solver has taken (`Accept`, `Backtrack`) and for the `Done` answer. A solver that builds its answer piece by piece (a greedy, a backtracking search, a branch and bound) implements the marker interface `IIncrementalSolver` (in [SolverInterface.cs](../../Interfaces/SolverInterface.cs)): its partial is always what it has committed (it may also hold the piece under test), and its `focus` names only the piece under test. Its partial is then `Solution` in every step, and only the focus is `ElementHighlight` (`Try`) or `Rejected` (`Reject`), so a Kruskal step that skips an edge shows the tree so far in green and the skipped edge in red. Never infer the kind from a solver's name: declare it.

| `shape` | Problems (`rule`) | Interface to implement | `StepShape` | How the answer reads |
| --- | --- | --- | --- | --- |
| `subset` | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set | `IGraphSubsetVisualization<T>` (`GraphSubsetSpec`) | `NodeSet` | Chosen nodes `Solution`; edges the answer handles `Covered`, edges breaking the rule `Rejected`; Clique draws a `phantoms` entry for each unjoined chosen pair. |
| `edges` | Minimum Spanning Tree (`spanningTree`), Steiner Tree (`steinerTree`) | `IGraphEdgesVisualization<T>` (`GraphEdgeSpec`) | `EdgeSet` (edge ids, `"a-b"`) | Chosen edges `Solution`; a node an edge of the answer touches is `Covered` (the nodes joined so far), both `ElementHighlight` in a `Try` or `Reject` step of a whole candidate; the edge being tried is `ElementHighlight`; in a `Reject` step the thing that failed (an edge that would close a cycle, a terminal that is not reached) is `Rejected`. Weights are `weight` on the payload's edges. Steiner terminals are `terminal: true` on the payload's nodes (not a state: the frontend draws a double outline). |
| `cut` | Cut (`cut`), Weighted Cut (`weightedCut`), Max Cut (`maxCut`), Minimum Cut (`minCut`) | `IGraphCutVisualization<T>` (`GraphEdgeSpec`) | `Groups` (node id to `0` for the S side, `1` for the T side) | The frame carries the split as `groups` (node id to 0 or 1: an annotation, the frontend colors the sides, not a state) and every edge that crosses it is `Solution` (`ElementHighlight` while a whole split is only being tried). A rejected candidate split is told apart by its `Reject` event and its caption, not by a red element; in an incremental run the focus of a `Reject` is `Rejected`. |
| `groups` | Graph Coloring (`coloring`), Clique Cover (`cliqueCover`) | `IGraphGroupsVisualization<T>` (`GraphGroupSpec`, K required) | `Groups` (node id to a 0-based group index) | Every frame carries `groups` (node id to group index; a node with no group yet is left out), the annotation the frontend colors the nodes with. Nodes are never `Solution` (the group colors are the answer; the mockups show no green nodes). Coloring: an edge whose two ends share a group is `Rejected` (a clash). Clique Cover: an edge inside a group is `Solution` (the clique is drawn solid; `ElementHighlight` while a whole grouping is only being tried; an incremental solver's grouping so far stays `Solution`) and a missing edge between two nodes of one group is a `Rejected` phantom. Either rule: a node whose group is K or more is `Rejected`. The node being colored is `ElementHighlight`; in a `Reject` step the thing that failed (the clashing pair, the unjoined pair, a node with no color) is `Rejected`. `k` on the payload is the color or group count. A solver that gives up ends on a `Done` step with `ok: false` and no groups. |
| `tour` | Hamiltonian Cycle (`hamiltonianCycle`), Directed Hamiltonian Cycle (`hamiltonianCycle`, `subtype: "directed"`), Traveling Salesperson (`tsp`), Minimum Traveling Salesperson (`minTsp`) | `IGraphTourVisualization<T>` (`GraphTourSpec`; `Directed`, and `K` for TSP) | `Tour` (node ids in visiting order) | Every frame carries `order`, the path so far (a closed tour repeats its first node at the end, like the certificate), the annotation a frontend draws order badges from. Each step of the path that is an edge is `Solution` (`ElementHighlight`, with the nodes on the path, in a `Try` or `Reject` step of a whole tour; an incremental solver's path so far keeps these states, and only the next node, the hop to it or the closing hop it names in `focus` is marked); a step that is not an edge (a dead end that was tried, or a closing edge the graph lacks) is a `Rejected` phantom, drawn as a dashed line. Nodes on the path are `Covered`, and `Solution` on the `Done` frame when `ok` is true (the solved mockups are green). The node or edge being tried is `ElementHighlight`; in a `Reject` step the thing that failed is `Rejected`. TSP puts `weight` on the payload's edges and `k` on the payload, and its captions carry the running path cost and the best so far. Minimum TSP puts `weight` on the edges and no `k`; its brute force records each new best tour as an `Accept` (drawn as `Solution`, so the recorder's cap keeps the last one, the cheapest) and every other ordering as a `Reject` saying why. The layout is `"circle"`. **Directed graphs**: `subtype: "directed"` means draw an arrowhead at each edge's `b`; an edge's `a` is its tail. Directed edge ids are `"a>b"` (tail first, never reordered; names escaped as for `"a-b"`, with `GraphTourFrames.EdgeId`) because a directed graph can hold both a-to-b and b-to-a, which a sorted `"a-b"` would merge into one id, and a frame's state map is keyed by id. A solver that finds no tour ends on a `Done` step with `ok: false` and an empty `order` (as does a run stopped by the time limit). |

To add a problem to a shape:

1. Implement the interface on the default visualization, **explicitly**, so nothing new appears in `/info`, and declare the step shape: `Type? IVisualization.StepShape => typeof(EdgeSet);` (or `NodeSet`, `Groups` for the cut and groups shapes, `Tour` for the tour shape).
2. Return the spec from `Describe`: nodes, edges (with `GraphEdgeInput(a, b, weight)` where the problem has weights), the rule, and `K` or `Terminals` if it has them. Loops are dropped and the lightest of parallel edges is drawn. The tour shape takes `GraphTourEdge(from, to, weight)` and `Directed` instead, and draws the first of repeated edges.
3. If the solver's answer string is not the usual form, override `ParseAnswer`. The edge shapes read `{{a,b},...}` and `{({a,b},w),...}` by default, the cut shape recovers a split from a list of cut edges (Max Cut's answer is the S side itself, so it overrides), and the groups shape reads the classes of `{{a,d},{b},{c}}` (the n-th class is group n). The answer string is only used for the last frame when the steps do not end in a `Done` step: a solver that records no steps, or one that returns without calling `Done` (as the converted solvers do when `timerHasExpired`). A solver that records steps normally ends with a `Done` step carrying its own partial answer. A run stopped by the step time limit (`SolveRun.TimedOut`) is different: its answer string is never read, and it ends with a `Done` frame with `ok: false`, the stopped caption, and every element `Background` (the cut and groups shapes keep an empty `groups`, the tour shape an empty `order`).
4. Make the solver implement `ISolver<T, EdgeSet>` (or `Groups`) and record decisions with captions in the problem's words ("Edge 2-5 joins two trees, so add it (weight 3)."). Pass lambdas, so nothing is built when recording is off, and keep the answer identical. Cut solvers record the *split* and let the base draw the crossing edges. Groups solvers record the grouping so far (a brute force records each candidate, leaving out nodes it gave no group; a greedy records one node per step) and use `GraphGroupFrames.Why` and `Classes` for captions. Tour solvers record the path so far: a backtracking search uses `Try` (start), `Accept` (walk an edge), `Reject` (no such edge: put the node tried at the end of the partial so the missing edge is drawn) and `Backtrack`; a permutation solver records one step per complete permutation and lets the recorder's cap of 150 hide the rest; a best-first search says "queued" for a branch it has not followed yet. Use `GraphTourFrames.Arrows` for captions.
5. Prove nothing changed for old clients: snapshot the list-format output and `/info` before touching code, and snapshot what the solver answers on seeded instances (`Endpoints/Snapshots/`, `GraphFrames_Endpoint_Tests`, `GraphEdgeFrames_Endpoint_Tests`, `GraphGroupFrames_Endpoint_Tests`, `GraphTourFrames_Endpoint_Tests`, `EdgeSetSolvers_Tests`, `GroupSolvers_Tests`, `TourSolvers_Tests`).

### The Grouped Graph picture (`format=frames`)

A CNF problem (SAT, 3SAT) can serve the shared `GroupedGraph` picture, the clause graph: one node per literal, grouped into one cluster per clause with the clusters on a ring. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the problem.

The problem hands over only its clauses: `Describe` returns `GroupedGraphSpec.FromLiterals(problem.clauses)`, or a `GroupedGraphSpec(int[][] clauses, string[] variables)` (a literal is a 1-based variable index, negative when negated, as in `[[-1, 2, 3], [-1, -2, 4]]`). The base derives the clusters, the complement edges and the states, so SAT and 3SAT get identical pictures from the same code. Variables are numbered in natural order of name (`x2` before `x10`).

The payload (sent once) is `{ kind: "groupedGraph", layout: "ring", shape: "assignment", rule: "satisfiability", groups, nodes, variables, edges, clauseEdges }`. Each frame is `{ event, caption, focus, ok?, nodes, groups, assignment }`, where `nodes` and `groups` map an id to `{ state }` (an id that is absent is `Background`) and `assignment` maps a variable id to `true` or `false` (an unassigned variable is absent).

**Ids are a contract**: the Reduction View matches gadgets on them, so do not change them.

| What | Id | Example |
| --- | --- | --- |
| Clause `i` (0-based, formula order) | `c{i}` | `c0` |
| The `j`-th literal of clause `i` (one node per occurrence, so a literal written twice is two nodes) | `c{i}-{j}` | `c0-1` |
| Variable `v` (the name escaped: a `\` before every `\`, `~`, `-` and `:` in it) | `v:{name}` | `v:x2`, `v:a\-b` |
| An edge, written earlier occurrence first (clause index, then position, compared as numbers) | `{a}~{b}` | `c0-1~c2-0` |

The edge separator is `~` because `-` cannot be one: literal ids contain it. Clause and literal ids never hold a name, so they never contain `~`; variable names can hold any of the separators, so `GroupedGraphFrames.VariableId` escapes them (`GroupedGraphFrames.EscapeName`, the same rule as the Graph picture's) and two variables never share an id. An id is a label, never something to split: every payload edge gives its two ends as `a` and `b`, and every node gives its `variable` id; the variable's name as written is its `label` in `variables`.

`edges` are the complement pairs (a literal and the negation of the same variable in a *different* clause). `clauseEdges` join every two literal occurrences inside one clause; the frontend's Edges switch shows `edges` alone (the default) or both lists, and both together are exactly the graph of the 3SAT to Independent Set reduction. A node is `{ id, label ("x2" or "¬x2"), variable, positive, group }`, a group is `{ id, label ("C1"), members }`, so hovering a literal can trace its `variable` everywhere.

States: a true literal is `Solution` in a step that keeps the assignment (`Accept`, `Backtrack`, `Done`) and `ElementHighlight` in one that only tries a whole assignment (`Try`, `Reject`), as in the Graph picture; either way its clause is `Covered`. An incremental solver (the 3SAT backtracking, an `IIncrementalSolver`) records the variables decided so far, so their true literals stay `Solution` in every step and only the variable being tried is highlighted. A literal that is false under the assignment is `False` ("False: literal is false under the assignment"); a literal whose variable is unassigned stays `Background`, since nothing has been decided about it. A clause with every literal false is `Rejected` along with its literals; the variable being tried (a `v:` id in the step's `focus`) is `ElementHighlight` on every occurrence that is not false; a false occurrence stays `False`, so the tried value's false literals never look like its true ones. A clause in the focus is `ElementHighlight`, or `Rejected` in a `Reject` step. The order when several apply is `Rejected > ElementHighlight > Solution > Covered > Blocked > False > Background`, except that the focus highlight never covers `False` on a literal. Falsified clauses come from the assignment alone, never from the event.

A run that did not finish ends with a `Done` frame with `ok: false`, nothing assigned and every element `Background`: one stopped by the recording time limit, and one stopped by the solver's own timer (the SAT brute force answers `timeout` and records a `Done` saying it stopped without finishing). The answer `timeout` is never read as an assignment.

To add a problem:

1. Implement `IGroupedGraphVisualization<T>` on the default visualization, **explicitly**, so nothing new appears in `/info`, and declare `Type? IVisualization.StepShape => typeof(Assignment);`. Attach it to the problem as its `defaultVisualization` (SAT was once wired to `DummyVisualization`, which cannot visualize anything).
2. Make the solver record `Assignment` steps (variable name to `"True"`/`"False"`) with captions in the problem's words: a backtracking search uses `Try` (a value for the next variable), `Accept` (no clause is falsified), `Reject` (a clause has no true literal left) and `Backtrack`; a brute force records one step per candidate and lets the recorder's cap of 150 hide the rest. Focus is `GroupedGraphFrames.VariableId(name)` for the variable being tried and `ClauseId(i)` for a clause that failed. `GroupedGraphFrames.FirstFalsifiedClause`, `ClauseText` and `Assigned` build captions; call them inside the recorder lambdas so nothing is built when recording is off. A solver that is not generic (like the SAT brute force, which would otherwise join the navigation lists) can record typed steps by overriding `ISolver.Run` and returning `typeof(Assignment)` as the shape.
3. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer on seeded formulas (satisfiable and not) before touching code (`Endpoints/Snapshots/`, `GroupedGraphFrames_Endpoint_Tests`, `SatSolvers_Tests`, `GroupedGraphFrames_Tests`).

### The Bipartite Graph picture (`format=frames`)

A set problem whose answer picks sets (Set Cover, Exact Cover) or elements (Hitting Set, which is the exact version, #698) can serve the shared `BipartiteGraph` picture: elements in one column, sets in the other, one edge per membership. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the problem.

The problem hands over `BipartiteSpec(universe, sets, k, pick, rule)`: the element names, each set's elements, Set Cover's `K` (or null), the column the answer picks (`BipartitePick.Sets` or `Elements`) and the rule (`BipartiteRule.AtLeastOnce` or `ExactlyOnce`, how often every node of the other column must be hit).

The payload (sent once) is `{ kind: "bipartiteGraph", layout: "columns", pickSide: "sets" | "elements", rule: "atLeastOnce" | "exactlyOnce", elements, sets, edges, k? }`. An element is `{ id, label }`, a set is `{ id, label, members }` (its element ids, in instance order) and an edge is `{ id, a, b }` with `a` the element and `b` the set. Each frame is `{ event, caption, focus, nodes, edges, hits, ok? }`: `nodes` (both columns) and `edges` map an id to `{ state }`, an id that is absent is `Background`, and `hits` maps each node of the other column that the picks hit to how many picks hit it (an annotation for the ×2 badge, not a state).

| What | Id | Example |
| --- | --- | --- |
| Element `x` (a `\` before every `\`, `~` and `:` in the name) | `e:{x}` | `e:1`, `e:a\~b` |
| Set `i` (1-based, instance order; sets are named by position, so two sets with the same elements are two nodes) | `S{i}` | `S2` |
| The membership of element `x` in set `i` | `{element}~{set}` | `e:1~S2` |

Build ids with `BipartiteFrames.ElementId`, `SetId` and `EdgeId`, never by hand. An id is a label, never something to split: every edge gives its ends as `a` and `b`, and every set lists its `members`.

States: a picked node and its edges are `Solution` in a step that keeps the picks (`Accept`, `Backtrack`, `Done`, and every step of an `IIncrementalSolver`) and `ElementHighlight` in a `Try` or `Reject` of a whole candidate (a brute force). A node of the other column that the picks hit is `Covered`; under `exactlyOnce` one hit more than once is `Rejected` in every step. One that is not hit is `Rejected` only when a complete candidate is judged (`Done`, or a whole candidate's `Try` or `Reject`) and something is picked: while an answer is being built, and for the empty pick, nothing has failed yet. Under `exactlyOnce`, a pick that is not taken but would double up on a covered node is `Blocked` (not while a whole candidate is only being tried). The focus is `ElementHighlight`, or `Rejected` in a `Reject` of an incremental run; a pick in the focus marks its edges too. The order when several apply is `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`. A run stopped by the time limit ends with a `Done` frame with `ok: false`, every element `Background` and no `hits`; its answer string is never read.

To add a problem:

1. Implement `IBipartiteVisualization<T>` on the default visualization, **explicitly**, so nothing new appears in `/info`, and declare the step shape: `Type? IVisualization.StepShape => typeof(ChosenSets);` for an answer that picks sets, `typeof(NodeSet)` for one that picks elements (`BipartiteFrames.StepShape`).
2. Return the spec from `Describe`. A static `Spec(problem)` next to it lets the solvers build captions from the same description.
3. Make each solver implement `ISolver<T, ChosenSets>` (set ids, `S1`, `S2`, ...) or `ISolver<T, NodeSet>` (element names), and mark the ones that build an answer piece by piece with `IIncrementalSolver`. A brute force records each candidate as a `Reject` (or the winning `Accept`) with `BipartiteFrames.Why` in the caption ("Try {S1,S2}: 5 isn't covered."); a greedy records each pick as an `Accept` and a final `Reject` for a cover that is incomplete or over K; a backtracking search (Algorithm X) records `Accept` for a set it takes, `Reject` for an element no set can cover (focus: the element) or a budget it exceeds (focus: the set), and `Backtrack` for a set it undoes. Always end with `Done`, whose `ok` is the verifier's verdict on the answer (`{}` is the empty collection).
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer on seeded instances before touching code (`Endpoints/Snapshots/`, `BipartiteFrames_Endpoint_Tests`, `BipartiteSolvers_Tests`, `BipartiteFrames_Tests`).

**The SAT factor graph.** SAT and 3SAT have a second Bipartite picture, their factor graph, served by two extra visualizations, `SatFactorGraphVisualization` and `Sat3FactorGraphVisualization` (`IFactorGraphVisualization<T>`, [`FactorGraphFrames`](../../Interfaces/graphs/FactorGraphFrames.cs)). Their list format is exactly the default SAT and 3SAT pictures', so old frontends see the same picture under a second name. They read the same `GroupedGraphSpec` and the same `Assignment` steps as the clause graph, so every SAT solver animates on both. The payload is the Bipartite one with the variables as `elements`, the clauses as `sets`, `pickSide: "assignment"`, `rule: "satisfiability"`, and an edge per literal occurrence carrying `positive` (false for a negated literal, drawn dashed). Ids are the clause graph's: variables `v:{name}`, clauses `c{i}`, and each edge is its literal's id `c{i}-{j}`. Each frame adds `assignment` (variable id to `true`/`false`). A true literal's edge and a true variable are `Solution` (`ElementHighlight` while a whole assignment is only tried); a false one is `False`; a clause with a true literal is `Covered` (`hits` counts its true literals), one with every literal assigned and false is `Rejected` along with its edges, as the clause graph marks its literals (from the assignment alone, whatever the event); the variable being tried is `ElementHighlight` with its true edges. The order is `Rejected > ElementHighlight > Solution > Covered > Blocked > False > Background`.

### The Automaton picture (`format=frames`)

DFA and NFA serve the shared `Automaton` picture: states laid out left to right in the order the machine reaches them, merged transition arrows, a start arrow, double-ring accept states and an input tape. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the machine.

The problem hands over `AutomatonSpec(kind, states, alphabet, transitions, start, accepting, input)`: `AutomatonKind.Dfa` or `Nfa`, the state names, the symbols, each transition as `(from, symbol, to)` with `"ε"` for an ε-transition, the start state, the accept states and the input string (`"ε"` is the empty input).

The payload (sent once) is `{ kind: "dfa" | "nfa", layout: "layered", states, start, accepting, alphabet, transitions, input, garbage?, paths?, pathsTruncated? }`:

- A state is `{ id, label, layer }`. `layer` is how many moves the start state needs to reach it; a state it cannot reach goes one layer past the last.
- `start` and `accepting` hold state ids. `input` is the tape, one symbol per cell (`[]` for the empty input).
- A transition is one arrow `{ id, a, b, label, symbols, epsilon }` for every transition from `a` to `b`: `label` joins the symbols with commas (`"a,b"`), and `epsilon` is true when every symbol is ε (the arrow is dashed).
- `garbage` is there only for a DFA with a missing move: `{ id: "garbage", label: "Garbage", missing }`, where `missing` lists each `{ state, symbol }` with no move. A symbol of the input outside the alphabet is a missing move too: the `{ state, symbol }` the run hits is listed (only that one, since the run stops there), and it brings `garbage` along even when every in-alphabet move exists. The symbol is not added to `alphabet`. It is drawn apart, with no arrows into it. Its label gets a `'` while a state already has that name.
- `paths` is there only for an NFA: its runs for the path picker, accepting runs first, at most 40 (`pathsTruncated` says whether there are more). Each is `{ id, label, accepted, outcome, frames }`. The `label` reads like `Path 1: 1 ⇢ 2 → 3, accepts` (⇢ is an ε move), `outcome` is `accepts`, `rejects` or `dies`, and `frames` follow that run alone, in the same shape as the main frames. The runs are the ones `NFASolver.GetPathRuns` finds, in the same order, so Path *i* is Run *i* of the NFA table.

Each frame is `{ event, caption, focus, position, active, nodes, edges, ok? }`. `position` is how many symbols have been read (the tape head sits on the next one). `active` lists the ids of the states the machine is in. `nodes` and `edges` map an id to `{ state }`; an id that is absent is `Background`.

| What | Id | Example |
| --- | --- | --- |
| State `x` (a `\` before every `\`, `>` and `:` in the name) | `q:{x}` | `q:1`, `q:a\>b` |
| Every transition from `x` to `y` | `{id of x}>{id of y}` | `q:1>q:2` |
| The DFA's Garbage state | `garbage` | `garbage` |

Build ids with `AutomatonFrames.StateId`, `TransitionId` and `GarbageId`, never by hand. An id is a label, never something to split: every arrow gives its ends as `a` and `b`.

States: the active states are `ElementHighlight` while the machine runs, and so is the focus (the arrow just taken, the state just reached). In a `Reject` the focus is `Rejected`. `Solution` appears only in an `Accept` (an active accept state) and in the `Done` frame of an input the machine accepts (the active accept states). In a `Done` that rejects, every active state is `Rejected`, Garbage included. In every `Done`, a state the run never reached on this input is `Untraveled`. The order when several apply is `Rejected > ElementHighlight > Solution > Background`. A run stopped by the time limit ends with a `Done` frame with `ok: false`, nothing active and every element `Background`; its answer string is never read.

The solvers and their steps (`ISolver<T, ActiveStates>`; a step's partial is the states the machine is in and how many symbols it has read):

- `DFASolver` records a `Try` for the start state and one per symbol read (focus: the arrow taken). A missing move, a symbol outside the alphabet included, is a `Reject` whose partial has no states: the picture shows the machine in Garbage (focus: `garbage`), and the run stops there. Input used up in a state that does not accept is a `Reject` on that state.
- `NFASolver` follows one run at a time: a `Try` for each move (focus: its arrow), `Accept` when a run ends in an accept state, `Reject` at a dead end (a symbol outside the alphabet is one too: no state has a move on it), and `Backtrack` when it backs out of a move.
- `NFASubsetSolver` ("NFA Subset Simulation") runs every branch at once. It records a `Try` per symbol with the whole active set (focus: every arrow taken, ε moves included), then an `Accept` on the accept states it reached, or a `Reject`. This is the picture's "all branches" view. The path picker is the "one path" view.

Every run ends with `Done`, whose `ok` says whether the machine accepts.

To add an automaton problem:

1. Implement `IAutomatonVisualization<T>` on its visualization, **explicitly**, so nothing new appears in `/info`, and declare `Type? IVisualization.StepShape => typeof(ActiveStates);`.
2. Return the spec from `Describe`. A static `Spec(problem)` next to it lets tests build the same description.
3. If the list format already drew the typed steps before the picture existed, also return `true` from `IFramesVisualization.ListReadsTypedSteps()`, so the list keeps getting them. It is a method, not a property, so it never shows in `/info`.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/automaton_*.tsv`, `AutomatonFrames_Endpoint_Tests`, `AutomatonSolvers_Tests`, `AutomatonFrames_Tests`).

### The Layered Graph picture (`format=frames`)

Directed-graph problems serve the shared `LayeredGraph` picture: the graph ranked left to right so most edges point forward, with the edges that close a cycle drawn as curves under the rows. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the graph. Directed Hamiltonian Cycle is not one of them: it stays a directed tour in the Graph picture.

The problem hands over `LayeredSpec(nodes, edges, rule)` with `Directed`, `K`, `Source` and `Target` where they apply. `edges` are `LayeredEdgeInput(from, to, weight?)` as the instance lists them, parallel edges included. `rule` is one of `TopologicalSort`, `FeedbackArcSet`, `FeedbackNodeSet`, `StronglyConnectedComponents`, `SingleSourceShortestPath`, `SinglePairShortestPath`.

The payload (sent once) is `{ kind: "layeredGraph", layout: "ranked", shape, rule, directed, weighted, nodes, edges, layers, k?, source?, target? }`:

- `shape` is `order` (Topological Sort), `removedSet` (Feedback Arc Set and Node Set), `groups` (Strongly Connected Components) or `distances` (SSSP, SPSP).
- A node is `{ id, label, layer, row }`. `layers` lists each rank's node ids in row order, so `layers[layer][row]` is the node.
- An edge is `{ id, a, b, back, weight?, label? }`. `back` is true when it closes a cycle in the layout (draw it under the rows). `weight` is the weight that counts: the lightest of parallel edges. `label` is there only for parallel edges and lists every listed weight in the order listed (`"5,2"`): draw `label` if present, else `weight` (#769). In a directed graph `a→b` and `b→a` are two edges.
- `directed` is false only for a shortest-path instance written with `{u,v}` edges; then every edge goes both ways and has no arrowhead.

Layout: break cycles by depth-first search (from the source first), rank by the longest path over the other edges, then order each rank by the average row of its neighbors (two down-and-up sweeps), as `mockups/layered.js` does.

Each frame is `{ event, caption, focus, nodes, edges, <annotations>, ok? }`, where `nodes` and `edges` map an id to `{ state }` (absent means `Background`) and the annotations depend on the shape, present in every frame of that shape:

| Shape | Annotations |
| --- | --- |
| `order` | `order`: the placed node ids in order (the order badges) |
| `removedSet` | none |
| `groups` | `groups`: node id to component index (0-based, in the order found); `finish`: the first pass's finish order (finish number *n* is the *n*-th id) |
| `distances` | `dist`: every node id to its best known distance, `null` for ∞; `pred`: each reached node to the node it was reached from |

| What | Id | Example |
| --- | --- | --- |
| Node `x` (a `\` before every `\`, `-` and `>` in the name) | `{x}` | `1`, `a\-b` |
| Directed edge from `x` to `y` | `{id of x}>{id of y}` | `1>2` |
| Undirected edge (shortest paths written with `{u,v}`) | `{smaller}-{larger}` | `1-2` |

Build ids with `LayeredFrames.NodeId`, `ArcId` and `EdgeId`, never by hand. A node id never holds a bare separator and an edge id always does, so no two ids are equal. An id is a label, never something to split: every edge gives its ends as `a` and `b`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`, with `Untraveled` only where nothing else applies):

- **order**: placed nodes `Solution` (Kahn's committed order), nodes ready to place `Covered`; an edge between placed nodes `Solution` (forward), one out of a placed node `Covered`. A `Done` with no order shows the nodes that can never be placed and one cycle among them `Rejected`.
- **removedSet**: what a candidate removes is `ElementHighlight` while it is tried and `Solution` in an `Accept` or the answer (draw a removed edge dashed). Edges that go with a removed node are `Untraveled`. A cycle that survives is `Rejected`. The brute forces try removing nothing first (#789): that candidate is empty, so nothing is highlighted, and an acyclic graph is accepted at once with the answer `{}`.
- **groups**: `groups` colors the nodes, so a node in a component has no state; a node visited but not yet in a component is `Covered`; an edge inside a component is `Solution`.
- **distances**: settled nodes, and the edges they were settled through, `Solution`; reached but not settled `Covered`. In `Done`, nodes never reached are `Untraveled`; for a single pair only the path is `Solution` (the rest of what was settled is `Covered`) and an unreached target is `Rejected`.
- The focus is `ElementHighlight`, or `Rejected` in a `Reject` of an incremental solver. A run stopped by the time limit ends with a `Done` frame, `ok: false`, every element `Background`; its answer string is never read.

The solvers and their steps (partials hold node names; focus holds picture ids):

- `KahnsAlgorithm` (`Tour`, incremental): a `Try` with the nodes ready to place, then an `Accept` per node placed (focus: that node). A cycle ends with a `Reject` of the nodes that are never freed, then a `Done` that is not ok.
- `ArcSetBruteForce` (`EdgeSet` of edge ids) and `NodeSetBruteForce` (`NodeSet`), whole candidates: a `Reject` per removal that leaves a cycle (the caption names it), an `Accept` for the one that works.
- `KosarajuSolver` (`Components`, incremental): pass 1 is a `Try` per visit and per finish; pass 2 an `Accept` per node joining a component (focus: the node and the edge followed backward).
- `SSSPSolver` and `SPSPSolver` (`Distances`, incremental): a `Try` at the source, then an `Accept` per settled node (focus: the node and the edges it relaxed). SPSP stops when the target is settled.

To add a directed-graph problem:

1. Implement `ILayeredVisualization<T>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape` as `LayeredFrames.StepShape` of its rule.
2. Return the spec from `Describe`. A static `Spec(problem)` next to it lets solvers (for captions such as `LayeredFrames.SurvivingCycle`) and tests build the same description.
3. If the list format draws steps of its own, return `true` from `IFramesVisualization.ListReadsTypedSteps()` so it is still called, and have it make its legacy steps itself (as `SSSPVisualization.StepsVisualization` does). A table that makes its own steps declares the typed `StepShape` for the same reason.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/layered_*.tsv`, `LayeredFrames_Endpoint_Tests`, `LayeredSolvers_Tests`, `LayeredFrames_Tests`).

### The Flow Network picture (`format=frames`)

Minimum S-T Cut serves the shared `FlowNetwork` picture: the source on the left, the sink on the right, every edge labelled with its flow and capacity, one frame per augmenting path, and a last frame that splits the nodes into the S and T sides. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the network.

The problem hands over `FlowSpec(nodes, edges, source, sink)`. `edges` are `FlowEdgeInput(from, to, capacity)` as the instance lists them, parallel edges included.

The payload (sent once) is `{ kind: "flowNetwork", layout: "sourceToSink", rule: "minimumSTCut", source, sink, nodes, edges, layers }`:

- A node is `{ id, label, layer, row }`. `layers` lists each column's node ids in row order, so `layers[layer][row]` is the node. The source is alone in the first column and the sink alone in the last. Every other node sits in the column of its hop distance from the source; nodes the source cannot reach share the column before the sink. This is the layout of `mockups/flow.js`.
- An edge is `{ id, a, b, capacity, label? }`. Edges listed more than once from `a` to `b` are one arrow whose `capacity` is their sum, because the solver adds them. `label` is there only for such edges and lists every listed capacity in the order listed (`"2,5"`): draw `label` if present, else `capacity` (#769). An edge written twice with the same capacity is one edge, since the instance's edges are a set. `a→b` and `b→a` are two edges.

Each frame is `{ event, caption, focus, nodes, edges, flow, value, path, groups?, cut?, ok? }`. `nodes` and `edges` map an id to `{ state }` (absent means `Background`). The annotations:

| Field | In | Meaning |
| --- | --- | --- |
| `flow` | every frame | Every edge id to the units it carries. The residual room is derived from it: `capacity − flow` forward, `flow` backward (the backward arcs to draw) |
| `value` | every frame | The flow's value so far |
| `path` | every frame | The augmenting path this step used, as legs `{ edge, forward, units }`; `forward: false` means the path ran against the edge, taking back flow sent earlier. Empty when the step used no path |
| `groups` | the Done frame | Every node id to `0` (S side, what the source can still reach) or `1` (T side) |
| `cut` | the Done frame | The ids of the edges from S to T. Their capacities add up to `value` |

Ids: a node is its name with a `\` before every `\`, `-` and `>` (`1`, `a\-b`), and the edge from `x` to `y` is `{id of x}>{id of y}` (`1>2`). Build them with `FlowFrames.NodeId` and `ArcId`, never by hand. An id is a label, never something to split: every edge gives its ends as `a` and `b`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`, with `Untraveled` only where nothing else applies):

- While flow is pushed, a full edge (flow equals capacity, which is above 0) is `Covered`. How much an edge carries is the `flow` annotation, not a state.
- The focus is the path just used, nodes and edges, and is `ElementHighlight`.
- In the Done frame the cut edges are `Solution` and the edges from T back into S are `Untraveled`. `groups` colors the two sides, so nodes have no state of their own.
- A run stopped by the time limit ends with a `Done` frame, `ok: false`, every element `Background`, every edge's flow 0, an empty path and no `groups` or `cut`. Its answer string is never read.

The solver and its steps (`NetworkFlow`: units keyed by edge id, the legs, the value, and in Done the S side by node name):

- `MinSTCutSolver` (Edmonds-Karp, incremental): a `Try` with no flow, then an `Accept` per augmenting path ("Path 1: push 3 along 1 → 2 → 4. 2 → 4 is now full. Flow is now 3."; a backward leg is written `⇠` and named in the caption), then a `Done` with the S side.
- The solver's residual graph merges parallel edges and both directions of a pair, so it only knows each pair's net flow. The steps draw that net flow on the edge it runs along. That is always a valid flow, and the algorithm itself is unchanged.

To add a flow problem or another max-flow solver:

1. Implement `IFlowVisualization<T>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape => typeof(NetworkFlow)`.
2. Return the spec from `Describe`. A static `Spec(problem)` next to it lets tests build the same description.
3. Record a `NetworkFlow` per augmenting path, with its legs, and the S side in `Done`. Ford-Fulkerson with a depth-first search fits the same steps.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solver answers before touching code (`Endpoints/Snapshots/flow_*.tsv`, `FlowFrames_Endpoint_Tests`, `FlowSolvers_Tests`, `FlowFrames_Tests`).

### The Packing picture (`format=frames`)

Partition, Subset Sum, Knapsack and Bin Packing serve the shared `Packing` picture. Every one of them asks the same question in a different container: which items go where, so that a total hits its mark. The items sit in a pool, and the containers are drawn to scale with their lines. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the instance.

The problem hands over `PackingSpec(items, rule)`. `items` are `PackingItemInput(size, value, label)` in instance order (`value` only for Knapsack, `label` as the instance writes the item: `4`, `(10,60)`). The rule's numbers go in `Target` (Subset Sum's T), `Limit` (Knapsack's W, Bin Packing's C), `AtLeast` (Knapsack's V) and `Bins` (Bin Packing's K).

The payload (sent once) is `{ kind: "packing", rule, items, containers, total, maxBins? }`:

- `rule` is `"partition"`, `"subsetSum"`, `"knapsack"` or `"binPacking"`.
- An item is `{ id, label, size, value? }`, with `value` only for Knapsack.
- A container is `{ id, label, target?, limit?, atLeast? }`. The base derives them from the rule:

| Rule | Containers | Lines |
| --- | --- | --- |
| `partition` | `S1` and `S2` | `target`: half the total on each (it can end in `.5`, when no split can balance) |
| `subsetSum` | `Chosen` | `target`: T |
| `knapsack` | `Knapsack` | `limit`: W, on the sizes (the weights). `atLeast`: V, on the values |
| `binPacking` | `Bin 1` ... `Bin m`, with m = min(K, number of items) | `limit`: C on each. `maxBins` is K itself, since no packing fills more bins than it has items |

- `total` is the sum of every item's size.

Each frame is `{ event, caption, focus, items, containers, assign, totals, ok? }`. `items` and `containers` map an id to `{ state }` (absent means `Background`). The annotations:

| Field | Meaning |
| --- | --- |
| `assign` | One entry per item: the 0-based index of its container in `containers`, or `-1` when it is left out. The group colors of Partition's sides and Bin Packing's bins come from it |
| `totals` | One entry per container: `{ size, value? }`, the sum of its items' sizes and, for Knapsack, of their values |

Ids are positions, never numbers: item `i` (0-based, instance order) is `I{i+1}` and container `c` is `C{c+1}`. Two items of the same size are two items, and no id needs escaping. Build them with `PackingFrames.ItemId` and `ContainerId`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`, with `Untraveled` only where nothing else applies):

| State | Items | Containers |
| --- | --- | --- |
| `ElementHighlight` | In a container while a whole candidate is only tried (a `Try` or `Reject` of a brute force); the focus | The focus |
| `Solution` | In a container in an `Accept`, after a `Backtrack`, in every step of an incremental solver (what it has committed) and in `Done` | not used |
| `Untraveled` | Left out of a correct answer (`Done`, `ok: true`) | not used |
| `Covered` | not used | Its line is met: a target hit exactly, a knapsack within W and worth at least V, a bin that holds something and is within C |
| `Rejected` | The focus of an incremental solver's `Reject` (an item no bin has room for) | Over its limit, in every frame. A target missed, or a value short of V, only when a whole placement is judged (`Done`, or a `Try` or `Reject` that is not part of an answer still being built) and something is placed |

A run stopped by the time limit ends with a `Done` frame, `ok: false`, every element `Background`, nothing placed (`assign` all `-1`) and every total 0. Its answer string is never read.

The solvers and their steps (`Placement`: one container index per item, by position, `-1` for left out; trailing `-1`s are dropped):

| Solver | Incremental | Steps |
| --- | --- | --- |
| Partition Brute Force | no | A `Reject` per split that fails ("Try S1 = {7,12}: 19 against 131, off by 112."), the `Accept` of the one that balances, `Done` |
| Subset Sum Brute Force | no | A `Reject` per subset in mask order ("Try {1,7}: 1 + 7 = 8, not 28."), the `Accept` of the one that adds up to T, `Done` |
| Fast Approximation Algorithm | no | A `Try` per number with the best subset kept so far ("Add 12 to every sum kept so far that stays within 28. 8 sums kept. Best so far: 20."), `Done` |
| Knapsack Brute Force | no | A `Reject` per set in mask order, the empty set first ("Try {(10,60)}: weight 10, value 60, short of 220."), the `Accept` of the one that works, `Done` |
| Knapsack Dynamic Programming | yes | A `Try` per row of the table with its item in focus and nothing taken, then an `Accept` per item the trace-back takes, `Done`. The table itself belongs to the Table picture (#671) |
| Bin Packing Brute Force | yes | An `Accept` per item put in a bin ("Put 4 in bin 1: 4 of 10 used."), a `Reject` when an item has nowhere to go, a `Backtrack` per placement taken back, `Done` |
| Bin Packing First Fit Decreasing | yes | A `Try` with the sorted order, an `Accept` per item put in the first bin with room, a `Reject` for the item that would need a bin past K, `Done` |

Subset Sum, Knapsack and Bin Packing have their own visualizations for the picture (`SubsetSumVisualization`, `KnapsackVisualization`, `BinPackingVisualization`). Their list format draws the items as a set (`SetD3`). They are the problems' default visualizations.

To add a packing problem or another packing solver:

1. Implement `IPackingVisualization<T>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape => typeof(Placement)`.
2. Return the spec from `Describe`, and read an answer string as a placement in `ParseAnswer` (`PackingFrames.Groups` and `FromGroups` do the usual shapes; equal numbers fill items in order). A static `Spec(problem)` next to it lets tests build the same description.
3. Record a `Placement` per decision, by position: keep track of which item went where, not just the numbers. Declare `IIncrementalSolver` when the solver commits items one at a time.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/packing_*.tsv`, `PackingFrames_Endpoint_Tests`, `PackingSolvers_Tests`, `PackingFrames_Tests`).

### The Table picture (`format=frames`)

Edit Distance and 0-1 Integer Linear Programming serve the shared `Table` picture: rows and columns of text cells, drawn in HTML. It comes in three shapes that share one renderer:

| Shape | Problems | How it reads |
| --- | --- | --- |
| `grid` | Edit Distance | A dynamic-programming grid. The cell being filled is the focus, with arrows from the cells it compared. The trace-back's cells are the answer |
| `matrix` | 0-1 Integer Linear Programming | The assignment on top, then one row per constraint with its total against its bound |
| `trace` | the second views of DFA, NFA, SSSP and SPSP | One row per step, added as the run reaches it. Cells that changed this step are marked |

Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface.

The problem hands over a `TableSpec(shape, columns)`. A column is `TableColumnSpec(label, key?)`: the header as written, and an optional camelCase name a frontend can key on. When the rows are fixed (a grid's letters, a matrix's constraints), `RowLabels` gives their headers. A trace table leaves `RowLabels` null, and its rows are whatever its frames reach. `Title`, `RowAxis` and `ColumnAxis` are optional captions (the grid's `source x` and `target y`).

The payload (sent once) is `{ kind: "table", shape, columns, rows, title?, rowAxis?, columnAxis? }`:

- A column is `{ id, label, key? }`.
- A row is `{ id, label? }`. A trace table lists as many rows as its longest frame shows, without labels.

Each frame is `{ event, caption, focus, values, rows, cells, changed, arrows?, ok? }`:

| Field | Meaning |
| --- | --- |
| `values` | The text of every cell shown, row by row: `values[i][j]` is cell `r{i}c{j}`. A cell that is still empty is `null`. There is one entry per row shown, the first rows of the payload. Grids and matrices show every row in every frame. A trace table shows more rows as the run goes on |
| `rows`, `cells` | Map a row id or a cell id to `{ state }` (absent means `Background`). A cell's own state shows over its row's |
| `changed` | The cells whose value changed at this step, drawn underlined. An annotation, not a state |
| `arrows` | Grid only: `{ from, to, best }`, from each cell the fill read to the cell it filled. `best` marks the one whose value it took |

Ids are positions, never contents: row `i` is `r{i}`, column `j` is `c{j}` and their cell is `r{i}c{j}` (0-based). Repeated letters, labels that look like ids and labels with separators stay distinct, and no id needs escaping. Build them with `TableFrames.RowId`, `ColumnId` and `CellId`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`, with `Untraveled` only where nothing else applies). The visualization sets them in the problem's own terms, and the base adds the focus:

| State | Grid (Edit Distance) | Matrix (0-1 ILP) |
| --- | --- | --- |
| `ElementHighlight` | The cell being filled, and the trace-back's newest cell (the focus) | The assignment's cells while it is only tried (a `Try` or `Reject` of the brute force) |
| `Solution` | The trace-back's cells, in every step once it starts (the solver is incremental) and in `Done` | The assignment and every row in an `Accept` and in a correct `Done` |
| `Covered` | not used | A row that holds while the assignment is only tried |
| `Rejected` | not used | A row over its bound, in every frame |

`Solution` is only for an `Accept` and for the answer. While Edit Distance fills its table nothing is the answer yet, so filled cells have no state.

A run stopped by the time limit ends with a `Done` frame, `ok: false`. It shows the instance with nothing worked out (an empty grid, or the matrix with every `x` a `?` and no totals), every element `Background`, nothing changed and no arrows. Its answer string is never read.

The solvers and their steps:

| Solver | Shape | Incremental | Steps |
| --- | --- | --- | --- |
| Edit Distance Dynamic Programming | `TableCells` | yes | A `Try` that fills row 0 and column 0, a `Try` per cell ("Cell (1,1): 'h' ≠ 'r'. Replace 'h' with 'r': 1 + 0. Delete 'h': 1 + 1. Insert 'r': 1 + 1. The cheapest is replace, so 1."), then an `Accept` per cell of the trace-back ("(5,3) came from (4,3): delete 'e'."), `Done`. If the cells, the trace-back and the first and last steps would not fit in the 150-step cap, the table is filled one row per step instead, so the whole fill stays visible |
| 0-1 ILP Brute Force | `Assignment` (`x1` ... `xn` to `0` or `1`) | no | A `Reject` per assignment in the counter's order, naming the rows over their bounds ("Try (1 1 0 0): row 2 adds up to 2, over its bound 1."), the `Accept` of the first that holds, `Done` |

`TableCells` holds the text of every cell filled so far (by cell id), the cells the latest fill read (the one it took first), and the trace-back's cells in order. It suits any dynamic-programming table.

The two problems have their own visualizations for the picture (`EditDistanceVisualization`, `IntProgrammingVisualization`). Their list format is a `DynamicTable` before and after, built by `TableFrames.ToListTable`. They are the problems' default visualizations.

#### Trace tables: DFA and NFA

`DFATableVisualization` and `NFATableVisualization` are the run tables, separate from the DFA and NFA graphs. Under `format=frames` they draw the Table picture's `trace` shape, read off the same `ActiveStates` steps the Automaton picture draws ([`AutomatonTraceTable`](../../Interfaces/Table/AutomatonTraceTable.cs)). No solver changes for them.

- The columns are Step, Read, From, To and Accepting? (keys `step`, `read`, `from`, `to`, `accepting`). Read is `ε` for an ε move and `—` for the start row.
- Every move adds a row. The new row is the focus, and its To cell is marked changed.
- An `Accept` marks its row `Solution`, and a `Reject` marks its row `Rejected`. In `Done` the last row is `Solution` when the input is accepted and `Rejected` when not.
- A DFA's missing move, a symbol outside the alphabet included, is a row into `Garbage`. Every NFA branch dying is a row into `∅` (subset simulation), or a dead end marked `Rejected` (backtracking); a symbol outside the alphabet kills every branch the same way. Both rows are `Rejected`.
- NFA Backtracking (the table's own solver) shows the branch being explored: a `Try` adds a row and a `Backtrack` takes it away. Its `Done` shows the first branch that accepted, or the last one that died.
- NFA Subset Simulation (`&solver=nfasubsetsolver`) adds one row per symbol, with every active state in braces.
- Past the 150-step cap a step may not follow on from the one before. A row of `…` then stands for the steps not shown. A DFA keeps the rows before it, since they are the start of its one run. An NFA keeps only its start row, because a search's backtracking may have been hidden.
- A stopped run ends with an empty table, `ok: false`.

Their list format and /info are unchanged. `ListReadsTypedSteps()` returns true, so their list methods keep getting the steps they always did.

#### Trace tables: SSSP and SPSP

`SSSPTableVisualization` and `SPSPTableVisualization` are Dijkstra's tables, separate from the shortest-path graphs. Under `format=frames` they draw the Table picture's `trace` shape, read off the same `Distances` steps the Layered Graph picture draws ([`DistanceTraceTable`](../../Interfaces/Table/DistanceTraceTable.cs)). No solver changes for them.

- There is one row per node, in instance order, in every frame. The payload's `title` names the source (and the target, for SPSP).
- The columns are Node, Settled, Cost, Via and Path (keys `node`, `settled`, `cost`, `via`, `path`).
  - Settled is `#k` (the order the node was settled in), `not yet`, or `unreachable` in `Done`. It is `yes` when the node was settled past the step cap, where the order is not known.
  - Cost is `∞` until a route is found.
  - Via and Path are `—` until a route is found.
- The node a step settles (the source, at the start) is the focus. Its cost and via cells are marked changed when a step lowers them.
- Dijkstra is incremental, so a settled node's row stays `Solution` in every step.
- In `Done`:
  - SSSP: every reached node's row is `Solution`.
  - SPSP: the path's rows are `Solution`. A target that can't be reached is `Rejected`.
  - A node that can't be reached is `Untraveled`.
- A stopped run ends with the node names and nothing worked out, `ok: false`.

Their list format and /info are unchanged. `ListReadsTypedSteps()` returns true, so their list methods keep getting the steps they always did.

To add a table:

1. Implement `ITableVisualization<TProblem, TPartial>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape => typeof(TPartial)`.
2. Return the spec from `Describe`. In `Draw`, turn each step into a `TableDraft` and return one draft per step:
   - add its rows (`AddRow`);
   - mark rows and cells (`MarkRow`, `MarkCell`, `MarkChanged`);
   - name the focus (`FocusCell`, `FocusRow`);
   - for a grid, add arrows.

   `Blank` is the instance with nothing worked out. `Solved` (optional) draws an answer when the steps cannot be drawn. A static `Spec(problem)` next to it lets tests build the same description.
3. A table that is a second view of a problem that already has another frames picture (an automaton's run, Dijkstra's distances) is its own visualization. It draws the same solver's steps, so the solver is not changed, and the problem's other pictures and default stay as they are.
4. Prove nothing changed for old clients: snapshot `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/table_solver_answers.tsv`, `TableFrames_Endpoint_Tests`, `TableSolvers_Tests`, `TableFrames_Tests`).

### The Board picture (`format=frames`)

Sudoku and N-Queens serve the shared `Board` picture: a grid of squares, the clues the instance gives, the digits or pieces a solver puts down, and dashed links from a square that was ruled out to the square that rules it out. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the instance.

The problem hands over `BoardSpec(game, size)`, with `Box` (Sudoku's 3), `LabelsFrom` (where row and column labels start: 1 for Sudoku, whose captions say R1C1; 0 for N-Queens, whose certificate counts from 0), `Clues` (`BoardClue(row, column, value)` for every square the instance fills in) and `PieceKind` (`"queen"` when the solver puts down pieces rather than text).

The payload (sent once) is `{ kind: "board", game, size, box?, labelsFrom, clues }`:

- `game` is `"sudoku"` or `"queens"`. It tells a frontend how to draw the squares: boxes with thick lines, or a checkerboard.
- `box` is present only for a board with boxes.
- A clue is `{ id, r, c, value }`. A frontend draws clues bold; they never change.

Each frame is `{ event, caption, focus, values, pieces, cells, links, ok? }`:

| Field | Meaning |
| --- | --- |
| `values` | The text on every square that holds text, keyed by square id: the clues and the digits placed so far (Sudoku). Empty for N-Queens |
| `pieces` | `{ id, r, c, kind }` for every piece on the board (N-Queens' queens), in reading order. Empty for Sudoku |
| `cells` | Square id to `{ state }`, only for squares that are not `Background`. A piece takes its square's state |
| `links` | `{ from, to, state }`: from a square this step ruled out to the square that rules it out (a skipped square to the queen attacking it, a clue to the clue it repeats). Always `Rejected` so far |

Ids are positions, never contents: the square in row `i` and column `j` (0-based) is `r{i}c{j}`. Equal digits are different squares, and no id needs escaping. Build them with `BoardFrames.CellId`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`):

| State | Squares |
| --- | --- |
| `ElementHighlight` | The focus: the square being tried, the queen just placed. Also every placed square while a whole candidate is only tried (a `Try` or `Reject` of a solver that is not incremental; none of today's are) |
| `Solution` | What the solver has put down, only in the `Done` frame with `ok: true`, once the board is confirmed as the answer. Before that a placed digit or queen is plain (`Background`), in every step of an incremental solver and in an `Accept`, so Solution colour never shows while the solver is still searching. Never a clue |
| `Rejected` | The focus of an incremental solver's `Reject` (a square with no digit left, a row with no safe square and the queen above it that must move); a square a step ruled out (the start of a link); everything placed in a `Done` whose answer is wrong |
| `Covered`, `Blocked`, `Untraveled` | not used. Squares a queen attacks are not marked: a frontend's Hints switch can shade them from the queens' positions |

A run stopped by the time limit ends with a `Done` frame, `ok: false`, showing only the clues: no pieces, no links, every square `Background`. Its answer string is never read. Long searches keep their first 150 steps and then the `Done` frame with the finished board; its caption says how many steps were not shown.

The solvers and their steps (`BoardCells`: `values`, every square that holds something, clues included, as a digit or `"Q"`; `conflicts`, each `{ cell, with }` a square ruled out and the square that rules it out):

| Solver | Incremental | Steps |
| --- | --- | --- |
| Sudoku Backtracking | yes | A `Try` per digit put in a square ("R1C1 can still hold 6, 8 or 9. Try 6.", "6 in R1C1 led to a dead end. Try 8 instead."), a `Reject` when a square has no digit left ("R2C6 has no digit left: every digit from 1 to 9 is already in its row, column or box. Undo the last choice."), `Done`. Clues that clash get one `Reject` naming them, linked, and nothing is searched |
| N-Queens Backtracking | yes | A `Try` per queen placed, with the squares skipped in its row as conflicts ("Row 1: columns 0 and 1 are attacked (0 along its column by the queen at (0,0)). Place the queen at column 2."), a `Reject` naming the whole row and the queen above it when no square is safe ("Row 2: every column is attacked, so back up and move the queen in row 1."), `Done` |
| N-Queens Constructive | yes | A `Try` with the column list the formula writes, then a `Try` per row ("Row 0: column 1, straight from the list. No checking needed."), `Done`. Rows are `Try`, not `Accept`: the recorder builds every `Accept` past its cap, which for a large n would build the board n times |

`SudokuVisualization` got the picture, and its list format, once an empty placeholder (`Unimplemented`), is now the grid as a table (`DynamicTable`): the clues, and once solved every square the solver filled in marked Solution. N-Queens has a new `NQueensVisualization`, whose list format is a table of each row's queen (`DynamicTable`). It is N-Queens' default visualization.

To add a grid puzzle or another board solver:

1. Implement `IBoardVisualization<T>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape => typeof(BoardCells)`.
2. Return the spec from `Describe`, and read an answer string as a board in `ParseAnswer`. A static `Spec(problem)` next to it lets tests build the same description.
3. Record a `BoardCells` per decision with the whole board, clues included, and the squares the step ruled out as conflicts. Build it only inside the recorder's lambdas, and pass the focus as a lambda too, so solving without steps costs nothing. Declare `IIncrementalSolver` when the solver fills squares one at a time.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/board_*.tsv`, `BoardFrames_Endpoint_Tests`, `BoardSolvers_Tests`, `BoardFrames_Tests`).

### The Schedule picture (`format=frames`)

Job Sequencing and both Pump Scheduling problems serve the shared `Schedule` picture: time runs left to right, drawn to scale. Job Sequencing puts each job's run on one machine as a bar with its deadline as a tick; Pump Scheduling gives each pump a row of 24 hours, with the tank level and the cost of each hour charted underneath. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the instance.

The two kinds of instance hand over different things:

- Job Sequencing: `JobScheduleSpec(jobs, k)`, with a `ScheduleJob(time, deadline, penalty)` per job in instance order.
- Pump Scheduling: `PumpScheduleSpec(rule, capacity, level, minimum, demand, peakHours, onPeak, offPeak, pumps)`, with a `SchedulePump(name, flow, power, startupCost)` per pump in instance order, and `Budget` for Emergency Resilience (as the instance writes it; 0 means 1.5 times the cheapest schedule).

The payload (sent once) has `kind: "schedule"` and a `rule`:

- `jobSequencing`: `{ kind, rule, jobs: [{ id, label, time, deadline, penalty }], k, span }`. `label` numbers jobs from 0, as the certificate does (`Job 0`). `span` is the length of the time axis: every job back to back, or the latest deadline if that is later.
- `costMinimization` and `emergencyResilience`: `{ kind, rule, hours: [{ id, label, demand, peak }], pumps: [{ id, name, flow, power, startupCost }], tank: { capacity, level, minimum }, rates: { onPeak, offPeak }, budget? }`. There are always 24 hours; `budget` is present for Emergency Resilience only.

Ids are positions, never numbers or names: job `i` (0-based) is `J{i+1}`, pump `p` is `P{p+1}`, hour `h` is `t{h}`, and pump `p` in hour `h` is `P{p+1}t{h}`. Two equal jobs, or two pumps with one name, stay apart, and no id needs escaping. Build them with `ScheduleFrames.JobId`, `PumpId`, `HourId` and `CellId`.

A Job Sequencing frame is `{ event, caption, focus, order, runs, jobs, penalty, ok? }`:

| Field | Meaning |
| --- | --- |
| `order` | The job ids placed so far, in the order they run |
| `runs` | `{ job, start, end, late }` for each, back to back from time 0. `late` means it ends after its deadline (the deadline tick turns red) |
| `jobs` | Job id to `{ state }`, only for jobs that are not `Background` |
| `penalty` | The penalties of the late jobs, added up |

A Pump Scheduling frame is `{ event, caption, focus, reached, schedule, upTo, hours, cells, budget?, ok? }`:

| Field | Meaning |
| --- | --- |
| `reached` | The dynamic program's forward pass so far, one `{ states, low, high, best }` per hour: how many (tank level, pumps on) states it can still reach, the band of their tank levels, and the best score (the cheapest cost so far, or the most water stored so far in gallon-hours) |
| `schedule` | Once the solver has a schedule, 24 `{ hour, on, starts, inflow, level, energy, startup }`, simulated exactly as the verifiers do it: the pumps on, the pumps that start that hour (the startup mark), the water pumped in, the tank level at the end of the hour, and the hour's energy and startup cost. Empty before |
| `upTo` | The last hour drawn so far: -1 with no schedule, the hour being replayed, else 23. Draw the tank line and the cost bars up to it |
| `hours` | Hour id to `{ state }` |
| `cells` | Pump-in-hour id to `{ state }`: the pumps on, as the `schedule` lists them |
| `budget` | Emergency Resilience only: the budget the schedule must fit, once the solver has fixed it |

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`):

| State | Jobs | Hours and pump cells |
| --- | --- | --- |
| `ElementHighlight` | Every placed job while a whole order is only tried (a `Try` or `Reject` of a solver that is not incremental, as the brute force is) | The focus: the hour the forward pass is filling, or the hour being replayed |
| `Solution` | Every placed job in an `Accept` or a correct `Done` | Every pump on in the schedule (the pump solvers are incremental, so what they have committed is `Solution` in every step) |
| `Rejected` | A placed job that ends after its deadline, in every frame: its penalty counts | An hour no state survives (the focus of a forward-pass `Reject`); an hour drawn so far whose tank level ends below the minimum or above the capacity |
| `Covered`, `Blocked`, `Untraveled` | not used | not used |

A run stopped by the time limit ends with a blank `Done` frame, `ok: false`: nothing placed, no forward pass, no schedule. Its answer string is never read. Long searches keep their first 150 steps, then the order that works and the `Done` frame; its caption says how many steps were not shown.

The solvers and their steps:

| Solver | Shape | Incremental | Steps |
| --- | --- | --- | --- |
| Job Sequencing Brute Force | `JobOrder` (`order`) | no | A `Reject` per order that fails, with its late jobs as the focus ("Try (0,1,2,5,3,4): jobs 2, 3 and 4 finish late, penalty 10 > K = 4."), an `Accept` for the first that fits ("... penalty 4, within K = 4. It works."), `Done` |
| Pump Scheduling Cost Minimization Dynamic Programming | `PumpPlan` (`reached`, `on`, `budget?`) | yes | A `Try` per hour of the forward pass ("Forward pass, hour 0: 8 reachable (tank level, pumps on) states, levels 4,400 to 5,450 gal, cheapest so far $0.00."), or a `Reject` at an hour no state survives and then `Done` with no answer; an `Accept` that commits the traced-back schedule; a `Try` per hour replaying it ("Hour 8 (peak): PumpA and PumpB on. In 550, out 1,000 gal, so the tank drops to 4,150 gal. Hour cost $1.62."); `Done` |
| Pump Scheduling Emergency Resilience Longest Path | `PumpPlan` | yes | The same, after a first `Try` that fixes the budget when the instance's is 0 ("... That costs $37.24, so the budget is $55.86."); the replay also says how much of the budget has gone |

The pump solvers' table works in whole gallons, so with fractional flows or demands their schedule can leave the tank's limits (#738). The picture simulates exactly, so such a schedule shows the hours it runs dry as `Rejected` and its `Done` has `ok: false`, as the verifier says.

`PumpSchedulingCMVisualization` and `PumpSchedulingEMVisualization` got the picture; their list format is unchanged. Job Sequencing has a new `JobSeqVisualization`, whose list format is a table of the jobs and when they run (`DynamicTable`). It is Job Sequencing's default visualization.

To add a scheduling problem or solver:

1. Implement `IJobScheduleVisualization<T>` or `IPumpScheduleVisualization<T>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape => typeof(JobOrder)` (or `typeof(PumpPlan)`).
2. Return the spec from `Describe`, and read an answer string in `ParseAnswer` (`ScheduleFrames.ParseJobOrder` and `ParsePumpPlan` read today's certificates). A static `Spec(problem)` next to it lets the solver and the tests build the same description.
3. Record a `JobOrder` or `PumpPlan` per decision. Build it only inside the recorder's lambdas, so solving without steps costs nothing; `ScheduleFrames.ReplayCaption` and `ForwardCaption` write the pump captions. Declare `IIncrementalSolver` when the solver commits its answer piece by piece.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/schedule_*.tsv`, `ScheduleFrames_Endpoint_Tests`, `ScheduleSolvers_Tests`, `ScheduleFrames_Tests`).

### The Tripartite Matching picture (`format=frames`)

3-Dimensional Matching serves the `TripartiteMatching` picture: X, Y and Z as three columns, each triple of M a line through one element of each, the triples a solver chooses, and the elements they use once or more than once. The picture comes from a new `DM3Visualization`, which is also the problem's default visualization (its list format is a table of the triples, `DynamicTable`).

The problem hands over `TripartiteSpec(X, Y, Z, Triples)`: the names in each set in instance order, and each triple of M as `TripartiteTriple(x, y, z)`, the positions of its names in X, Y and Z.

The payload (sent once) is `{ kind: "tripartite", x, y, z, triples }`:

- `x`, `y` and `z` list `{ id, name }` for each element, in instance order.
- A triple is `{ id, x, y, z }`, where `x`, `y` and `z` are element ids.

Each frame is `{ event, caption, focus, chosen, repeats, elements, triples, ok? }`:

| Field | Meaning |
| --- | --- |
| `chosen` | The ids of the triples chosen, in M's order |
| `repeats` | Element id to how many chosen triples use it, for each element used more than once (the ×2 badge) |
| `elements` | Element id to `{ state }`, only for elements that are not `Background` |
| `triples` | Triple id to `{ state }`, only for triples that are not `Background` |

Ids are positions, never names: the i-th element (from 1) of X, Y and Z is `x{i}`, `y{i}` and `z{i}`, and the i-th triple of M is `T{i}`. A name in more than one set, a triple listed twice and names with separators in them all stay distinct, and no id needs escaping. Build them with `TripartiteFrames.XId`, `YId`, `ZId` and `TripleId`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`):

| State | Elements | Triples |
| --- | --- | --- |
| `ElementHighlight` | The focus | A chosen triple while a whole set is only tried (a `Try` or `Reject` of a solver that is not incremental); the focus |
| `Solution` | | A chosen triple in an `Accept` and a correct `Done`, and in every step of an incremental solver (what it has committed) |
| `Covered` | Used by exactly one chosen triple | |
| `Blocked` | | Not chosen, but shares an element with a chosen triple: choosing it too would use that element twice |
| `Rejected` | Used by more than one chosen triple | A chosen triple that shares an element with another; every chosen triple in a `Done` whose answer is wrong; the focus of an incremental solver's `Reject` |
| `Untraveled` | not used | not used |

A run stopped by the time limit ends with a blank `Done` frame, `ok: false`: nothing chosen, no states. Its answer string is never read. Long searches keep their first 150 steps, then the `Accept` that found the matching and the `Done` frame; its caption says how many steps were not shown.

The solver and its steps (`ChosenTriples`: `triples`, the ids of the triples chosen, as a set):

| Solver | Incremental | Steps |
| --- | --- | --- |
| 3-Dimensional Matching Brute Force | no | A `Reject` per set of \|X\| triples the verifier refuses, focused on the elements it uses more than once ("Try T1, T2 and T3: Paul and Chloe are used twice."), then an `Accept` ("Try T2, T4 and T6: every element of X, Y and Z is used exactly once, so the verifier accepts it.") and `Done` ("T2 (Paul, Austin, Jake), T4 (Sally, Madison, Frank) and T6 (Dave, Bob, Chloe) use every element of X, Y and Z exactly once. Found after 15 tries."). With fewer triples than X there is only a `Done` step. A name in more than one set is captioned with its set ("1 in X") |

`DM3Visualization`'s list format is a table with one row per triple and, once solved, whether it is in the matching (`DynamicTable`).

To add another matching solver (the mockup's backtracking one, for example):

1. Record a `ChosenTriples` per decision, built only inside the recorder's lambdas (`TripartiteFrames.Chosen(indices)`). Pass the focus as a lambda too: the elements that repeat, or the triple under test.
2. Declare `IIncrementalSolver` when it picks triples one at a time. Then the committed triples stay `Solution`, and only the focus is highlighted, or `Rejected` in a `Reject` ("T5 shares Chloe with a chosen triple, so skip it.").
3. Add it to `IncrementalSolvers_Tests.Classification` and prove nothing changed for old clients: snapshot `/info` and what it answers before touching code (`Endpoints/Snapshots/tripartite_solver_answers.tsv`, `TripartiteFrames_Endpoint_Tests`, `TripartiteSolvers_Tests`, `TripartiteFrames_Tests`).

### The Geometry picture (`format=frames`)

Convex Hull serves the `Geometry` picture: the points drawn to scale on real axes, the small hulls a divide-and-conquer solver builds and merges, the chain each merge walks, the points it drops and, at the end, the hull's corners in order. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the instance.

The problem hands over `GeometrySpec(points)`: its points in instance order, as `(x, y)` pairs.

The payload (sent once) is `{ kind: "geometry", points }`, where each point is `{ id, x, y }`.

Each frame is `{ event, caption, focus, pieces, chain, hull, points, ok? }`:

| Field | Meaning |
| --- | --- |
| `pieces` | The hulls on the table, each `{ points, role }` with its corners counterclockwise. `role` is `left` or `right` for the two being merged (drawn in two group colors) and `piece` for a finished hull waiting to be merged. A role is an annotation, not a state |
| `chain` | The corners a merge has walked so far, in order (the bottom edge left to right, then the top edge back), drawn as a line. Empty outside a merge |
| `hull` | The answer's corners in output order, for the order badges. Empty until the `Done` frame |
| `points` | Point id to `{ state }`, only for points that are not `Background` |

Ids are positions, never coordinates: the i-th point (from 1) is `P{i}`. Build them with `GeometryFrames.PointId`. A solver works with distinct points, so if a problem ever lets a point repeat, steps name it by its first copy and every copy is drawn the same (`GeometryFrames.FirstOf`). Today the instance parser already merges repeated points.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`):

| State | Points |
| --- | --- |
| `ElementHighlight` | The focus: the corner just pushed onto the chain, the corners of a piece just made or merged. Also every corner on the table while a whole candidate is only tried (a solver that is not incremental; today's is) |
| `Solution` | Corners of the hulls on the table and points on the chain: what the incremental solver has committed. In the `Done` frame, the hull's corners |
| `Covered` | In the `Done` frame, every point that is not a corner: inside the hull or on an edge |
| `Rejected` | The focus of a `Reject`: the corner popped off the chain. A point dropped so far (inside a merged hull, or in the middle of an edge). In a `Done` frame whose answer is wrong, its corners |
| `Blocked`, `Untraveled` | not used |

A run stopped by the time limit ends with a blank `Done` frame, `ok: false`: no hulls, no states. Its answer string is never read. Long runs keep their first 150 steps, then the `Accept` with the whole hull and the `Done` frame; its caption says how many steps were not shown.

The solver and its steps (`HullBuild`: `pieces`, `left`, `right`, `chain` and `dropped`, as above):

| Solver | Incremental | Steps |
| --- | --- | --- |
| Convex Hull Divide and Conquer | yes | A `Try` for the sort ("Sort the 10 points by x, then y: P3, P4, ... Then split the list in half, again and again, until each piece has at most 3 points."), a `Try` per smallest piece ("Smallest piece: P1, P9 and P5. Its hull is the triangle they make."), and per merge: a `Try` to start it, a `Try` per corner pushed ("Add P9 to the bottom edge."), a `Reject` per corner popped ("The turn at P4 (from P3 to P1) is clockwise, so pop P4 from the bottom edge."; "P1, P2 and P3 are on one line, so pop P2 ..."), and a `Try` with the merged hull ("The merged hull is P3, P5, P9 and P4. P1 is now inside it (or on an edge), so it is dropped."). Then an `Accept` with the whole hull and `Done` with the verifier's verdict |

`ConvexHullVisualization` got the picture, and it is still Convex Hull's default. Its list format, once empty (`Unimplemented`), is now a table of the points (`DynamicTable`): each point's x and y and, once solved, its place among the hull's corners in counterclockwise order, the corners marked Solution. The solver asks the verifier for the `Done` step only while it records steps: the verifier checks every point against every edge, which would make a plain solve of a large hull quadratic.

To add another hull solver (the mockup's monotone chain, for example):

1. Record a `HullBuild` per decision, built only inside the recorder's lambdas, with the focus as a lambda too. A one-pass solver leaves `pieces`, `left` and `right` empty and builds only the `chain`.
2. Declare `IIncrementalSolver`: the committed corners stay `Solution`, and only the point under test is highlighted, or `Rejected` when it is popped.
3. Add it to `IncrementalSolvers_Tests.Classification` and prove nothing changed for old clients: snapshot the list format, `/info` and what it answers before touching code (`Endpoints/Snapshots/geometry_*.tsv`, `GeometryFrames_Endpoint_Tests`, `GeometrySolvers_Tests`, `GeometryFrames_Tests`).

### The Code Tree picture (`format=frames`)

Lossless Data Compression serves the `CodeTree` picture: a Huffman tree growing bottom-up from the text's characters, merge by merge, with its 0 and 1 edges, each character's code under it and the encoded bits underneath. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the instance.

The problem hands over `CodeTreeSpec(text)`: the text as the solver reads it. The base counts its characters (UTF-16 code units, as the solver and the certificate do).

The payload (sent once) is `{ kind: "codeTree", text, symbols }`. Each symbol, in character order, is `{ id, code, label, count }`:

- `code` is the character's UTF-16 code, as the certificate writes it (97 for `a`). It is a number because half of a surrogate pair (an emoji is two) cannot travel as a JSON string on its own.
- `label` is safe to draw: `␣` for a space, `↵` for a line break, `⇥` for a tab, `U+XXXX` for any other control character or lone surrogate, else the character itself.

Each frame is `{ event, caption, focus, merged, roots, codes, nodes, edges, bits?, ok? }`:

| Field | Meaning |
| --- | --- |
| `merged` | The merged nodes built so far, in order: `{ id, weight, left, right }`, the left child under bit 0 and the right under bit 1 |
| `roots` | The trees not merged yet, in the order the solver picks them: lightest first, then the tree holding the earliest character, then the oldest |
| `codes` | Character id to its code, once the tree is read; empty until then |
| `bits` | The length of the encoded text, once every character has a code; absent before |
| `nodes` | Leaf or merged node id to `{ state }`, only for nodes that are not `Background` |
| `edges` | Edge id to `{ state }`, only for edges that are not `Background` |

Ids are positions, never characters: the i-th character in character order (from 1) is the leaf `c{i}`, the k-th merge makes `n{k}`, and its edges are `n{k}.0` and `n{k}.1`. No id needs escaping, whatever the text holds. Build them with `CodeTreeFrames.LeafId`, `NodeId` and `EdgeId`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`):

| State | Nodes and edges |
| --- | --- |
| `ElementHighlight` | The focus: the two trees being merged and their new parent, and the parent's two edges |
| `Solution` | Every merged node, its children and its edges: what the incremental solver has committed. Once the codes are known, every leaf that has one (a one-character text has no merge) |
| `Rejected` | Everything drawn in a `Done` frame whose answer is wrong; the focus of an incremental solver's `Reject` (none today) |
| `Covered`, `Blocked`, `Untraveled` | not used. Leaves no merge has used yet are `Background` |

A run stopped by the time limit ends with a blank `Done` frame, `ok: false`: no tree, no codes, no `bits`. Its answer string is never read. A text with more than 150 different characters keeps its first 150 steps, then the `Accept` with the codes and the `Done` frame; its caption says how many steps were not shown.

The solver and its steps (`HuffmanForest`: `merges`, each `{ node, left, right, weight }`, and `codes`, by leaf id):

| Solver | Incremental | Steps |
| --- | --- | --- |
| Huffman's Algorithm | yes | A `Try` for the count ("Count each character: 70 characters, 19 different. ..."), a `Try` per merge with the two trees and their parent as the focus ("Merge f (5) and e (9), the two lightest trees left, into a node of weight 14."; "Merge p (2) and the tree of weight 2 (r and x), ... (A tie: the tree holding the earliest character goes first.)"), an `Accept` once the codes are read off the tree ("Read each code from the top: going left adds 0, going right adds 1. ..."), and `Done` ("Encoded: 279 bits, versus 560 bits at 8 bits per character, 50% smaller. ...") with the verifier's verdict. An empty text is a single `Done` step. Merges are `Try`, not `Accept`: the recorder builds every `Accept` past its cap |

`LosslessDataCompressionVisualization` got the picture, and it is still the problem's default. Its list format, once empty (`Unimplemented`), is now the code table (`DynamicTable`): each distinct character with how often it appears and, once solved, its code and the bits it takes, marked Solution. The solver asks the verifier for the `Done` step only while it records steps.

To add another code-building solver:

1. Record a `HuffmanForest` per merge, built only inside the recorder's lambdas, with the merged trees and their parent as a lambda focus. Name leaves and nodes with `CodeTreeFrames.LeafId` and `NodeId`.
2. Declare `IIncrementalSolver` if it never undoes a merge: what is merged stays `Solution`, and only the focus is highlighted.
3. Add it to `IncrementalSolvers_Tests.Classification` and prove nothing changed for old clients: snapshot the list format, `/info` and what it answers before touching code (`Endpoints/Snapshots/codetree_*.tsv`, `CodeTreeFrames_Endpoint_Tests`, `CodeTreeSolvers_Tests`, `CodeTreeFrames_Tests`).

### The Quantum Circuit picture (`format=frames`)

The six quantum-algorithm problems serve the `QuantumCircuit` picture: one wire per qubit, the gates in the order they run with the oracle drawn as a box, and bars underneath for how likely each outcome of the measured qubits is after the column just applied, colored by the sign of its amplitude. Like the Graph picture it is additive: `visualize` and `SolvedVisualization` stay as they are, and you add one interface plus a description of the instance.

The problem hands over a `QuantumCircuitSpec`, built by [`QuantumCircuits`](../../Interfaces/Quantum/QuantumCircuits.cs) from the instance alone: `Deutsch`, `DeutschJozsa`, `BernsteinVazirani` (phase kickback), `Simon`, `UnstructuredSearch` (Grover's rounds, ⌊π/4 · √(N/M)⌋ of them) and `PrimeFactorization` (Shor's period finding for the first part of the number that needs it, with the smallest base a that works). It is the textbook circuit, ported from the mockup: nothing asks a quantum server for it, and a server compiles its own. An instance with no circuit (a table that is not a power of two long, outputs that are not bits, a number the classical checks factor, a circuit over 24 qubits) has no qubits and no columns, and a `note` that says why.

The payload (sent once) is `{ kind: "quantumCircuit", problem, qubits, columns, stages, measure, readout, inputBits, simulated, note?, number? }`:

| Field | Meaning |
| --- | --- |
| `problem` | `deutsch`, `deutschJozsa`, `bernsteinVazirani`, `simon`, `unstructuredSearch` or `primeFactorization` |
| `qubits` | `{ id, name, role }`: `x0` `query`, `y` `helper`, `f0` `output`, `x0` `index`, `c0` `count`, `w0` `work` |
| `columns` | `{ id, gates }`, in the order they run. A gate is `{ id, type, qubits, control?, label?, oracle? }`, `type` one of `h`, `x`, `z`, `cx`, `measure`, `box`; a box spans its `qubits`, and the oracle is a box with `oracle: true` |
| `stages` | `{ label, from, to }`: "prepare", "query once", "interfere", "read", Grover's "round 1", ... |
| `measure` | The qubits the answer is read from, the most significant first |
| `readout` | `bits` (an outcome reads as a bit string) or `decimal` (Shor's counting register) |
| `inputBits` | n, the number of input bits of the oracle f, to write a classical call's input as bits |
| `simulated` | Whether frames carry `bars`: the circuit has at most 16 qubits. Shor's circuit is drawn, not simulated |
| `note`, `number` | Why there is no circuit, or what to know about it; the number to factor |

Each frame is `{ event, caption, focus, applied, queries, columns, gates, qubits, bars?, outcome?, counts?, result?, ok? }`:

| Field | Meaning |
| --- | --- |
| `applied` | How many columns have run. Columns before it are done, columns from it on are still to come; a classical solver never runs any |
| `queries` | The classical oracle calls so far: `{ input, output }` |
| `columns`, `gates`, `qubits` | Id to `{ state }`, only for those that are not `Background` |
| `bars` | Once a simulated circuit has run at least one column: `{ outcome, probability, sign }` per outcome of the measured qubits (`outcome` as a number, `sign` 1 or -1, 0 for complex amplitudes). With more than 5 measured qubits only outcomes above 0.4% are listed, at most 256 |
| `outcome` | The value read from the measured qubits, once known |
| `counts` | How often each outcome came up, when a quantum server said |
| `result` | The answer as the solver has it so far; in the `Done` frame, the solver's answer |

Ids are positions, never the instance's text: qubit k is `q{k}`, column c is `t{c}`, and a gate is `g{c}_{k}` for its column and its first qubit (a qubit carries at most one gate per column). Build them with `QuantumCircuits.QubitId`, `ColumnId` and `GateId`, or take a column's ids from `QuantumCircuitSpec.ColumnFocus`.

States (precedence `Rejected > ElementHighlight > Solution > Covered > Blocked > Background`, with `Untraveled` only where nothing else applies):

| State | Columns, gates and qubits |
| --- | --- |
| `ElementHighlight` | The focus: the column being applied and its gates, or the oracle while a classical solver calls it |
| `Solution` | The measured qubits and their measure gates, in an `Accept` (the reading) and a `Done` whose answer is right |
| `Rejected` | The measured qubits and gates in a `Done` whose answer is wrong; the focus of an incremental solver's `Reject` |
| `Untraveled` | Every column, in the `Done` frame of a run that never ran the circuit (a classical solver, or Shor's server run) |
| `Covered`, `Blocked` | not used |

A run stopped by the time limit ends with a blank `Done` frame, `ok: false`: nothing applied, called or read. Its answer string is never read. A long run keeps its first 150 steps, then its last `Accept` and the `Done` frame; the caption says how many steps were not shown.

The solvers and their steps (`QuantumProgress`: `applied`, `queries`, `outcome`, `counts` and `result`):

| Solver | Incremental | Steps |
| --- | --- | --- |
| Deutsch Classical, Deutsch-Jozsa Classical, Bernstein-Vazirani Classical, Simon Classical | yes | A `Try` per oracle call, with the oracle as the focus ("Call the oracle with x = 010: f(010) = 5, the same as f(000), so s could be 010."), an `Accept` with the answer and how many calls it took, and `Done` with the verifier's verdict. Deutsch-Jozsa records a `Reject` when f breaks the promise, Simon's solver when f is not 2-to-1 |
| Unstructured Search Brute Force | no | A `Reject` per item that is not marked, an `Accept` for the first marked one, `Done` ("No item of the 4 is marked: no solution." when none is) |
| Prime Factorization Trial Division | yes | A `Reject` per divisor that fails ("3 does not divide 35."), an `Accept` per prime factor ("35 ÷ 5 = 7, so 5 is a prime factor."), and `Done` ("12 = 2 × 2 × 3.") |
| Deutsch's, Deutsch-Jozsa, Bernstein-Vazirani and Grover's Algorithm | yes | A `Try` per column of the circuit, captioned from simulating it ("Apply H to the query qubits again. The signs interfere: 101 now has probability 100%."), then the server call: an `Accept` with what it measured ("The quantum server measured 101 in 1,024 of 1,024 runs, so s = 101. One oracle call.") or a `Reject` when it gave no answer, and `Done`. Grover's solver still throws when the server gives no answer (#538), so that request fails |
| Shor's Algorithm | yes | A `Try` that sends the number, an `Accept` with the factors (or a `Reject`), `Done`. The server picks its own base and circuit and reports neither, so no column is applied |

The solvers that call a server record the columns before asking, and nothing in the picture comes from the server but the reading: with no server, the circuit and its bars are still drawn, and the run ends with a `Reject` and `ok: false` (except Grover's, above). The server is asked exactly once, as before; a slow one keeps the request waiting (its client's timeout is 30 s), and the step after it ends a run that is past its budget.

The eight quantum visualizations got the picture (both `D3` and `Q` views of Deutsch, Deutsch-Jozsa and Bernstein-Vazirani, Unstructured Search's and Shor's), and Simon's Problem got `SimonVisualization`, whose list format is the quantum circuit payload with an empty QASM string. Their list formats are unchanged, and no problem's default changed.

Tests never reach a real server: `FakeQuantumServer` (in `redux-tests/Steps`) answers each endpoint as a noise-free server would, or with a canned reply, for the current test only (`QuantumSolverSettingsGlobal.UseForThisFlow`, #809). An endpoint test also sets `factory.Server.PreserveExecutionContext = true` before it makes its client, so the fake reaches the request.

To add another quantum solver:

1. Implement `ISolver<TProblem, QuantumProgress>`. Record oracle calls as `queries`, or the circuit's columns with `QuantumSteps.Columns`, and finish with `QuantumSteps.Done`. Build partials only inside the recorder's lambdas.
2. Declare `IIncrementalSolver` unless each step judges a whole candidate on its own.
3. Add it to `IncrementalSolvers_Tests.Classification` and to `QuantumSolvers_Tests.VisualizationOf`, and prove nothing changed for old clients: snapshot what it answers before touching code (`Endpoints/Snapshots/quantum_*.tsv`, `QuantumCircuitFrames_Endpoint_Tests`, `QuantumSolvers_Tests`, `QuantumCircuitFrames_Tests`).

### The Search Tree picture (`format=frames`)

The `SearchTree` picture is a **second view** of a backtracking or branch-and-bound solver: the tree of choices it made, growing step by step. It is drawn from the steps the solver already records, so it never changes a problem's own picture. Each problem that has one gets a separate, selectable visualization class (`Sat3SearchTreeVisualization`, for example). Its list format, /info type and solver come from the problem's existing picture, and no problem's `defaultVisualization` changes.

**What a solver hands over.** Each step may name its nodes of the tree: `SolverStep.Tree`, a list of `SearchNode` patches. A patch whose id is new creates the node under its parent; a patch for a node that exists changes its state (and bound). The last patch is the node the step is about. A step without `Tree` still works in every other picture, and its JSON is exactly what it was (`tree` is omitted when null).

| `SearchNode` field | Meaning |
| --- | --- |
| `nodeId` | Unique within the run; a counter the solver keeps |
| `parentId` | The node it branched from; `null` for the root |
| `label` | The choice, short: `x2=F`, `(1,3)`, `Denver`; `start` for a root |
| `state` | `queued`, `explored`, `pruned` (cut without exploring), `failed` (explored, then backed out of), `solution`, `skipped` (game trees) |
| `bound` | Optional note on the bound or cost that decided it: `lb 28 ≥ 27`, `tour 27`, `hit by (0,0)`, `C3 false` |

Record patches with the recorder overloads that take a fourth lambda (`rec.Try(partial, caption, focus, () => [node])`, and the same for `Accept`, `Reject` and `Backtrack`). Like the rest of a step they are built only for kept steps. `rec.Done(answer, ok, caption, () => path)` names the answer's path, root first: past the 150-step cap only the last `Accept` is kept, and it need not be the step that found the answer, so a solver that branches names the path again there.

The payload (sent once) is `{ kind: "searchTree", problem, instance, maxLeaves }`. Each frame is `{ event, caption, focus, nodes, total, cut, ok? }`:

| Field | Meaning |
| --- | --- |
| `focus` | The id of the node the step is about; empty in `Done` and in steps without patches |
| `nodes` | The drawn tree, parents before children, children in the order they were made: `{ id, parent, label, kind, state, bound?, order?, hidden? }` |
| `total` | Nodes the search has made so far, folded ones included |
| `cut` | How many of them it cut without exploring |

`kind` is the node's search state (`queued` ... `skipped`), or `more` / `cut` for a fold, or `hidden` for a stand-in for a parent whose own step was not kept. `pruned` and `cut` are drawn dashed, `failed` solid. `order` is when a best-first solver took the node off its queue (1 for the first): a node that leaves `queued`, or gets a child while queued, gets the next number, so depth-first solvers get none. `hidden` is how many nodes a fold stands for.

Ids are `n{nodeId}`, so the same node has the same id in every frame. A fold's id comes from its node: `n{id}.more` for a finished subtree folded away ("+7 more"), `n{id}.cut` for that node's cut-off children ("3 cut").

States (precedence `Rejected > ElementHighlight > Solution > Covered > Background`):

| State | Nodes |
| --- | --- |
| `ElementHighlight` | The node the step is about (`Rejected` instead in a `Reject`) |
| `Solution` | The path from the root to the latest solution node, only in the `Accept` that reaches it and in a `Done` with `ok: true` |
| `Covered` | Explored nodes, and solution nodes everywhere else (a later, better solution demotes the earlier one to `explored`) |
| `Rejected` | Pruned and failed nodes, and `cut` folds |
| `Untraveled` | Queued and skipped nodes |
| `Background` | `more` folds and `hidden` stand-ins |

**Folding.** Past 12 leaves, two or more cut-off leaves under one node share one `n{id}.cut` node. While there are still more than 12 leaves, the finished subtree with the most leaves folds into one `n{id}.more` node. A subtree that holds the node in focus, a queued node or the answer's path is never folded.

A run stopped by the time limit ends with the tree so far in a `Done` frame, `ok: false`, with no focus and no answer path; its answer string is never read. A solver that records no tree (a brute force, picked with `&solver=`) gets frames with empty `nodes`.

Solvers that record a search tree:

| Problem | Solver | Visualization | Search |
| --- | --- | --- | --- |
| 3SAT | 3SAT Backtracking | `Sat3SearchTreeVisualization` | Best first. The root `start` is queued; each value tried is a node (`x2=F`) under the state it decides, then queued with its priority (`priority -1`), pruned with the clause it makes false (`C3 false`), or the solution. A `Backtrack` takes the node it goes back to off the queue, so nodes carry order badges. The `Done` step names the solution's path |
| Traveling Salesperson | Traveling Salesperson Branch and Bound | `TSPSearchTreeVisualization` (its own solver is the branch and bound; the list format is `TSPDefaultVisualization`'s for that solver) | Best first. The root is the start city, queued with its lower bound (`lb 27`); a path taken off the queue is explored, or pruned when the best tour has caught up (`lb 28 ≥ 27`); each extension is a node, queued or pruned (`no edge`, or a bound no better); a complete tour is the solution, the best so far (`tour 27`), or failed. The `Done` step names the best tour's path, unless the greedy tour it started from was never beaten |
| N-Queens | N-Queens Backtracking | `NQueensSearchTreeVisualization` | Depth first. Under `start`, each square considered in a row is a node `(row,col)` under the queen above: skipped squares are pruned (`hit by (0,0)`), the queen placed is explored (the last row's is the solution), and a dead row takes the queen above back (failed; the root when row 0 runs out). The `Done` step names the answer's path |
| Graph Coloring | Graph Coloring Backtracking | `GraphColoringSearchTreeVisualization` | Depth first. Each level colors the uncolored node with the fewest colors left (then the most neighbours); a node is `a=1`. A color a neighbour already uses is pruned (`b has 1`), a color whose subtree fails is failed when it is taken back (a `Backtrack`), and no node takes a color more than one past the highest used so far. The `Done` step names the solution's path |
| 3-Dimensional Matching | 3-Dimensional Matching Backtracking | `DM3SearchTreeVisualization` | Depth first. Each level chooses a triple (`T2`) for the element not yet used with the fewest triples left that use no element twice. A triple that would use an element twice is pruned (`Chloe used by T1`), a triple whose subtree fails is failed when it is taken back (a `Backtrack`). The `Done` step names the solution's path |

Not covered, because nothing in Redux branches for it: Knapsack (`KnapsackBruteForce` enumerates every set of items in a flat loop and `KnapsackDP` fills a table). SAT has only its brute force and Grover's solver; 3SAT's PPZ, Schöning and WalkSAT are randomized local searches. TSP's brute force and greedy solvers don't branch either.

To add a search tree to another solver:

1. Give every node an id from a counter (the root is 0) and keep, for each state you may come back to, its `SearchNode`. Build the patches only inside the recorder's lambdas, and keep the steps otherwise exactly as they were.
2. Add a visualization class next to the problem's own picture that implements `ISearchTreeVisualization<TProblem, TPartial>` (`TPartial` is the solver's step shape), declares `Type? IVisualization.StepShape => typeof(TPartial)`, and passes `visualize` and `SolvedVisualization` to the problem's picture.
3. Snapshot the solver's steps before touching it (`Endpoints/Snapshots/searchtree_*_steps.tsv`: a hash of each step's JSON) and test that the steps without `tree` still hash the same, the tree links themselves (`SearchTree*_Tests`), and the endpoint output with `SearchTreeJson` (`SearchTree*_Endpoint_Tests`).

## 3. How Redux finds your visualization

At startup, `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) scans every class in the project. Any class that implements `IVisualization` is registered under its **lowercased class name**. Because of that:

- Your class name must be unique across the whole project, ignoring upper/lower case.
- The class needs a public constructor with **no arguments**. Redux creates it with `Activator.CreateInstance`. A class that cannot be built this way is silently skipped by the type catalog and breaks the endpoints.
- You do not edit any list. Creating the class is enough.
- The class is attached to a problem by its generic type: `IVisualization<INDEPENDENTSET, ...>` means "a visualization of Independent Set". The navigation endpoints use that to answer "which visualizations does this problem have?" (`GET /Navigation/Problem_VisualizationsRefactor?chosenProblem=INDEPENDENTSET`).
- Each problem names one of its visualizations as `defaultVisualization`. That is the one the GUI picks first.

The endpoints you get for free (all in `ProblemProvider.cs`, all `POST` with the instance string as the request body):

| Endpoint | What it returns |
| --- | --- |
| `POST /ProblemProvider/visualize?visualization=<ClassName>` | A JSON list: the plain picture, then any step pictures, then the solved picture. Empty pictures (`API_empty`) are left out. |
| `POST /ProblemProvider/visualizeReduction?reduction=<ClassName>&solution=<solution>` | The same kind of list, but for the **TO** problem of a reduction. It uses the TO problem's `defaultVisualization`. |
| `POST /ProblemProvider/gadgets?reduction=<ClassName>` | The reduction's **gadgets**: small records that say "these parts of problem A turned into those parts of problem B", so the GUI can highlight them together. |

Where does the solution come from? Your visualization has a `solver` member. The endpoint runs `solver.solve(instance)` to get the solution that it passes to `SolvedVisualization`, and `solver.GetSteps(instance)` to get the steps for `StepsVisualization`. So a visualization is only as correct as its solver.

> The old docs say each visualization needs its own controller or URL. That is out of date. The one `visualize` endpoint serves every class.

## 4. Case (a): a new problem with an existing visualization type

This is the common case. You write one C# file and touch nothing in Redux_GUI.

### Before you start

1. **The problem must already exist** in `Problems/NPComplete/` (or `NPHard/`, `P/`). If it does not, see [adding-a-problem.md](adding-a-problem.md). A visualization also needs a solver, so see [adding-a-solver.md](adding-a-solver.md) if you do not have one yet.
2. Work on a branch based on `CSharpAPI`. PRs go back to `CSharpAPI`.

   ```bash
   git fetch origin
   git switch -c add-visualization-<problem> origin/CSharpAPI
   ```

3. Make sure the project builds before you change anything. See [building-and-testing.md](building-and-testing.md).

### Where the file goes

```text
Problems/NPComplete/NPC_<PROBLEM>/Visualizations/<ClassName>.cs
```

| Thing | Rule | Worked example |
| --- | --- | --- |
| Folder | `NPC_<PROBLEM>/Visualizations/` | `NPC_INDEPENDENTSET/Visualizations/` |
| File name | Same as the class name | `IndependentSetDefaultVisualization.cs` |
| Namespace | `API.Problems.NPComplete.NPC_<PROBLEM>.Visualizations` | `API.Problems.NPComplete.NPC_INDEPENDENTSET.Visualizations` |
| Implements | `IVisualization<PROBLEM, PAYLOAD>` | `IVisualization<INDEPENDENTSET, API_GraphJSON>` |

### Step 1. Get the template

You have two options. Both give you the same file.

- **Copy it by hand.** The template is [ProblemTemplate/Templates/Visualizations/PROBLEMVisualization.txt](../../ProblemTemplate/Templates/Visualizations/PROBLEMVisualization.txt). Copy it to your folder, rename it `<ClassName>.cs`, and replace the placeholders `{PROBLEM}`, `{VISUALIZATION}`, and `{VISUALIZATION_PASCAL_CASE}`.
- **Download it from a running API.** Start the API, then call `GET /ProblemTemplate/visualization` with two query parameters:

  | Parameter | Meaning | Example |
  | --- | --- | --- |
  | `problemName` | The problem's **upper-case folder name** (the part after `NPC_`) | `CLIQUE` |
  | `visualizationName` | A human-readable name. It becomes the class name with spaces removed. | `My Clique Visualization` |

  ```bash
  curl -o VisualizationTemplate.zip "http://127.0.0.1:27000/ProblemTemplate/visualization?problemName=CLIQUE&visualizationName=My%20Clique%20Visualization"
  ```

  You get a zip containing `NPC_CLIQUE/Visualizations/MyCliqueVisualization.cs` and a `README.md`. The code is in [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs).

  **Where each file in the zip goes.** Paths are from the repo root (the folder that contains `Redux.slnx`):

  | The file in the zip | Where it goes in the repo | What it is, and what to do with it |
  | --- | --- | --- |
  | `NPC_CLIQUE/Visualizations/MyCliqueVisualization.cs` | `Problems/NPComplete/NPC_CLIQUE/Visualizations/MyCliqueVisualization.cs` | Your visualization. The zip always uses the `NPC_` prefix: for a problem under `Problems/NPHard/` or `Problems/P/`, use that problem's own folder (`NPH_` or `P_`) and fix the namespace. |
  | `README.md` | Nowhere. It is not copied into the repo. | Reference only: a description of every member you fill in. Read it, then leave it in the zip. |


Two template details to fix by hand:

- The template starts with `visualizationType = VisualizationType.Unimplemented`. **Replace it** with a real type. A left-over `Unimplemented` fails a test (section 6), on purpose. If no renderer fits, use `DummyVisualization` as the problem's default visualization instead of this class.
- The template sets `solver => new PROBLEM().defaultSolver`, so `visualize` works out of the box. You may replace it with a specific solver, for example `new MyBruteForce()`. Keep it expression-bodied (`=>`), because the problem's constructor builds its default visualization and an initializer would recurse.
- The template declares `IVisualization<{PROBLEM}>` with a plain `API_JSON` return type. Change it to `IVisualization<PROBLEM, API_GraphJSON>` (or your payload class) so the compiler checks you return the right shape.

### Step 2. Fill in the members

The members come from [Interfaces/VisualizationInterface.cs](../../Interfaces/VisualizationInterface.cs). Here is each one in plain words, with the value used by the worked example,
[`IndependentSetDefaultVisualization.cs`](../../Problems/NPComplete/NPC_INDEPENDENTSET/Visualizations/IndependentSetDefaultVisualization.cs).

| Member | What it is for | Worked example value |
| --- | --- | --- |
| `visualizationName` | The name people see in the GUI's visualization picker. | `"Independent Set Visualization"` |
| `visualizationDefinition` | One sentence on what the picture shows. | `"This is a default visualization for Independent Set"` |
| `source` | A citation, if the picture comes from a paper. Empty is allowed. | `""` |
| `sourceFile` | The repo-relative path of this class's source file. The template sets it to `SourceFile.Path()`; leave that line as it is, because the compiler fills in the path. | `SourceFile.Path()` |
| `contributors` | Names of the people who wrote it. | `{ "Russell Phillips" }` |
| `visualizationType` | Which GUI renderer draws it. Pick from the table in section 2. | `VisualizationType.GraphD3` |
| `solver` | The solver whose answer gets highlighted. Must **not** be `null`. | `new IndependentSetBruteForce()` |
| `visualize(problem)` | **The plain picture.** Returns the payload. | see Step 3 |
| `SolvedVisualization(problem, solution)` | The picture with the solution highlighted. Optional, but strongly recommended. | see Step 4 |
| `StepsVisualization(problem, steps)` | One picture per solver step. Optional. The default returns nothing. | not used here |

The constructor must be public with no arguments: `public IndependentSetDefaultVisualization() { }`.

Then **connect the visualization to its problem** if it is the default one. In the problem's class ([INDEPENDENTSET_Class.cs](../../Problems/NPComplete/NPC_INDEPENDENTSET/INDEPENDENTSET_Class.cs)):

```csharp
public IndependentSetDefaultVisualization defaultVisualization { get; } = new IndependentSetDefaultVisualization();
```

A problem can have more than one visualization class (for example Clique has `CliqueDefaultVisualization` and `CliqueLatexVisualization`). Only one is the default.

### Step 3. Write `visualize`

`visualize` reads the problem's fields and builds the payload. For a graph problem this is one line, because the problem already holds a graph that knows how to convert itself:

```csharp
public API_GraphJSON visualize(INDEPENDENTSET independentSet) {
    return independentSet.graph.ToAPIGraph();
}
```

`ToAPIGraph()` ([Interfaces/graphs/Graph.cs](../../Interfaces/graphs/Graph.cs) and [UtilCollectionGraph.cs](../../Interfaces/graphs/UtilCollectionGraph.cs)) returns an `API_GraphJSON`. Its JSON is a list of `nodes` (each with a `name` and an optional `color`) and a list of `links` (each with a `source`, a `target`, and optional `color`, `weight`, `directed`, and so on).

If your problem is not a graph, build the right payload yourself. For example, a set problem builds an `API_SET` and a SAT problem builds an `API_SAT`.

### Step 4. Write `SolvedVisualization`

`SolvedVisualization` receives the solution as a string, in the problem's certificate format. The usual pattern: build the same payload as `visualize`, then set a `color` on the nodes or links that are part of the solution. The worked example does this:

1. Parse the solution string into a list of node names (`{1,3}` becomes `1`, `3`).
2. Build the graph payload.
3. Give nodes in the solution the color `"Solution"` and all others `"Background"`.
4. Return it.

The color words are not CSS colors. They are **keys** that the GUI maps to real colors, in [VisColorsArray.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/constants/VisColorsArray.js) (for example `Solution`, `Background`, `ElementHighlight`). Use those words so your picture matches the others.

Two rules for the output:

- If the solution is empty, return the plain `visualize` result (or `API_empty`). Never crash on empty or garbage input. A test (section 6) throws garbage at every visualization and fails on any server error (HTTP 500).
- Returning `new API_empty()` means "nothing to show". The endpoint leaves it out of the list.

### Step 5. See it with your own eyes

1. Build and run the API (see [building-and-testing.md](building-and-testing.md) and [setup.md](setup.md)).
2. Call the endpoint. Note that the instance is sent as a JSON string in the body:

   ```bash
   curl -X POST "http://127.0.0.1:27000/ProblemProvider/visualize?visualization=IndependentSetDefaultVisualization" \
     -H "Content-Type: application/json" \
     -d '"(({a,b,c},{{a,b},{b,c}}),2)"'
   ```

   (Use a real instance for your problem. `GET /ProblemProvider/info?interface=<ProblemName>` shows the `defaultInstance` and `instanceFormat`.)
3. Success looks like a JSON list. The first element has `"kind": "graph"` (or your payload's kind), with `nodes` and `links`. If a solver finds an answer, the last element is the solved picture with some `color` fields set to `"Solution"`.
4. Optional but best: run Redux_GUI against your API and pick your visualization in the picker. See Redux_GUI's [README.md](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/README.md) for how to start it. Your visualization appears automatically, because the GUI asks the API what exists.

That is the end of case (a). There is no GUI code, no manifest change, and no registration.

## 5. Case (b): a brand-new visualization type

Only do this if **no existing renderer can draw your payload**. It means changes in both repos, and a new renderer is a lot of work. Ask in the project Discord or open an issue first, so nobody builds a duplicate.

### The contract in one picture

There are **three places** that must agree on the type's name. Spell it exactly the same, same capitals, in all three:

| Place | File | What to add |
| --- | --- | --- |
| API, source of truth | [Interfaces/VisualizationType.cs](../../Interfaces/VisualizationType.cs) | A new member of the `VisualizationType` enum |
| API, committed copy | [Documentation/visualization-types.json](../visualization-types.json) | The same name in the list |
| GUI, renderer | [Visualizations.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/Visualizations.js) | A new entry in the renderer registry, plus a copy of the manifest in [visualizationTypes.json](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/visualizationTypes.json) |

### Order of operations

Do the GUI PR **first**, then the API PR. Here is why, and what happens if you get it wrong.

1. **GUI PR (Redux_GUI, targets `ReduxAPI_GUI`).**
   1. Write the renderer component (see "How a renderer receives its data" below).
   2. Register it in [Visualizations.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/Visualizations.js): add a factory function and one line in the `new Map([...])`, under "current wire values". Use the **double-quoted key as the first thing in the entry**, like `["MyNewType", renderMyNewType],`. The checker reads that file with a text pattern, not a real JavaScript parser, and fails if it cannot find the keys.
   3. Add the name to the vendored [visualizationTypes.json](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/visualizationTypes.json) (keep it sorted).
   4. Optionally add a friendly display name in [visualizationCategories.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/visualizationCategories.js), so a tooltip says "Chessboard" and not "ChessboardD3". If you skip it, the raw name is shown.
   5. Run `npm run check:visualizations`, `npm run format:check`, and `npm run lint` (section 7). Open the PR.
   6. Do **not** register your renderer under a legacy-style key such as `"My New Type"` with spaces. The registry file explains that those old aliases are on their way out.
2. **API PR (Redux, targets `CSharpAPI`).** In **one** PR:
   1. Add the enum member to [VisualizationType.cs](../../Interfaces/VisualizationType.cs), with a one-line doc comment.
   2. Add the same name to [Documentation/visualization-types.json](../visualization-types.json). The list is sorted in plain alphabetical order where capitals come first (the test sorts with `StringComparer.Ordinal`). Copy the current style, one line, in that order.
   3. Add the payload JSON class in `Interfaces/JSON_Objects/` if you need one (give it a `kind` field like its neighbors).
   4. Write the visualization class as in section 4, using your new type.

   The enum and manifest **must change together**. A test called `ManifestMatchesEnum` (section 6) fails if they differ.

What if the order is wrong?

| What happened | What you see | Is anyone hurt? |
| --- | --- | --- |
| GUI merged first, API later (recommended) | A daily robot opens an issue "Visualization type manifest has drifted from the API" until the API PR merges. | No. The GUI just has a renderer nobody calls yet. |
| API merged first, GUI later | The GUI shows "not renderable" for your visualization, and the same drift issue opens. | Users see a polite message instead of a picture, until the GUI PR merges. |
| Enum changed but the manifest was not | The Redux build fails (`ManifestMatchesEnum`). | It is caught before merging. |
| Renderer registered but vendored manifest not updated (or the reverse) | The GUI PR check `npm run check:visualizations` fails. | It is caught before merging. |

**Link the two PRs** by writing each PR's number in the other's description, so the reviewers know both are needed.

### How a renderer receives its data

> Older GUI docs say a visualization component "takes only a URL". That is out of date. The GUI fetches the data itself and **hands the finished JSON to your component as props**. Your component does not call the API.

The flow, in short:

1. The GUI calls `POST /ProblemProvider/visualize` (or `/visualizeReduction`) and gets the JSON **list** from section 3.
2. The GUI lets the user step through that list. The element currently shown is called `problemData` (a reduction's picture is called `reductionData`).
3. [VisualizationLogic.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/widgets/VisualizationLogic.js) looks up the type in the registry and calls the renderer factory:

   ```javascript
   Visualizations.get(visualizationType)(
     solve,
     url,
     mappedProblemData,
     gadgetMap,
     visualizationState.gadgetsOn,
   );
   ```

4. The factory in `Visualizations.js` turns those arguments into props for your component. A real one:

   ```javascript
   const renderGraphD3 = (solve, url, problemData, gadgetMap, gadgetsOn) => {
     return (
       <StandardGraphSvgReact
         problemData={problemData}
         solve={solve}
         url={url}
         gadgetMap={gadgetMap}
         gadgetsOn={gadgetsOn}
       ></StandardGraphSvgReact>
     );
   };
   ```

So your component receives these props:

| Prop | What it is |
| --- | --- |
| `problemData` | One payload object from the API list: exactly the JSON your C# payload class produces (for a graph, `{ kind: "graph", nodes: [...], links: [...] }`). |
| `solve` | `true` when the user switched on "show solution". |
| `url` | The API base address. Most renderers do not need it. |
| `gadgetMap` | The reduction's gadgets (from `/gadgets`), so you can highlight matching parts. Only needed if your type is used in reductions. |
| `gadgetsOn` | `true` when the user switched gadget highlighting on. |

Other things worth knowing:

- Your component must return an SVG (or other drawable element). Copy the structure of a small existing renderer such as [StandardGraphSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardGraphSvgReact.js), and take colors from [VisColorsArray.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/constants/VisColorsArray.js) so light and dark themes keep working.
- If your renderer throws an error while the factory runs, the GUI catches it and shows a "visualization failed to render" message instead of a blank page. That is a safety net, not a plan. Handle missing fields yourself.
- The same registry draws a reduction's right-hand picture. A reduction's picture uses the **TO problem's `defaultVisualization`**, so your renderer must work for reductions too.
- `Unimplemented` is never a registry key. [renderability.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/renderability.js) treats it as "no renderer".
- Redux_GUI has no unit-test setup. Its automated checks are listed in section 7.

## 6. Tests

### Tests that already check your visualization for free

You do not have to register your visualization with these tests. They find it through reflection, and each runs once per visualization (or checks the whole list). All paths are in [redux-tests/](../../redux-tests/).

| Test | What it checks |
| --- | --- |
| [Metadata/VisualizationType_Tests.cs](../../redux-tests/Metadata/VisualizationType_Tests.cs): `NoNewUndeclared` | Your class does **not** declare `VisualizationType.Unimplemented`. Only one old class may (`DummyVisualization`). A new one that is still `Unimplemented` fails here, with your class name in the message. |
| same file: `AllowlistHasNoStaleEntries` | That list contains no class that has since been given a real type. If a class on it is ever given one, delete its line from `UnimplementedAllowlist`. This is the only time you should edit that list. |
| same file: `ManifestMatchesEnum` | [Documentation/visualization-types.json](../visualization-types.json) lists exactly the members of the `VisualizationType` enum, in order. This is the API half of the cross-repo contract. |
| same file: `AllInfo_SerializesEveryVisualizationTypeAsString` | Every visualization's type is sent as a **word** (`"GraphD3"`), never a number, and the word appears in the manifest. A visualization with a type missing from the manifest fails here. |
| [Endpoints/ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs): `Visualize_GarbageInstance_IsNever500` | Sending garbage instance strings to `/visualize` for your class never produces a server error (HTTP 500). Bad input should give a friendly 400. |
| [Endpoints/ProblemTemplate_Endpoint_Tests.cs](../../redux-tests/Endpoints/ProblemTemplate_Endpoint_Tests.cs) | The visualization template download still works and leaves no `{PLACEHOLDER}` behind. Only matters if you edit the template. |
| [Endpoints/Navigation_Endpoint_Tests.cs](../../redux-tests/Endpoints/Navigation_Endpoint_Tests.cs) and [Batch_Endpoint_Tests.cs](../../redux-tests/Endpoints/Batch_Endpoint_Tests.cs) | The lists of visualizations per problem work. They check a few known problems, not yours. |

Those tests only prove the basics. They do **not** check that your picture is correct. That is your job.

> Do not add your class to `UnimplementedAllowlist` to make a failure go away. Pick a real type instead. If no type fits, you are in case (b).

### Tests you write yourself

> **The build check won't tell you if you skipped this.** CI checks that every test passes, not that you wrote any. Your own tests are the only thing that proves your code gives the right answers, so reviewers will look for the ones in this guide's checklist. See [building-and-testing.md](building-and-testing.md#4-how-to-read-the-rbs-report).

Visualization tests live in the problem's own test file:

```text
redux-tests/Problems/NPC_<PROBLEM>/<PROBLEM>_Tests.cs
```

The best pattern to copy is in [redux-tests/Problems/NPC_MAXCUT/MAXCUT_Tests.cs](../../redux-tests/Problems/NPC_MAXCUT/MAXCUT_Tests.cs). It builds the visualization directly, calls it, and checks the payload:

```csharp
[Fact]
public void MAXCUT_Visualization_Returns_Graph() {
    MAXCUT problem = new MAXCUT();
    MaxCutVisualization viz = new MaxCutVisualization();
    var graph = (API_GraphJSON)viz.visualize(problem);
    Assert.Equal(5, graph.nodes.Count);
    Assert.Equal(6, graph.links.Count);
}
```

The same file also tests `SolvedVisualization` with a small instance whose answer was worked out on paper, and checks which links got the `"Solution"` color. Things to copy:

- `using Xunit;`, a `using` line for the namespaces of your problem and visualization (they end in `.Visualizations`), and `using API.Interfaces.JSON_Objects.Graphs;` for graph payloads.
- `namespace redux_tests;` and `[Fact]` on each test. Test names read like sentences: `<PROBLEM>_<What>_<Expectation>`.
- Cast the result to your payload type, for example `(API_GraphJSON)viz.visualize(problem)`.

A minimal outline for your own visualization (placeholders in angle brackets are yours to fill in):

```csharp
[Fact]
public void PROBLEM_Visualization_Returns_ExpectedShape() {
    var problem = new PROBLEM("<small instance>");
    var viz = new YourVisualization();
    var graph = (API_GraphJSON)viz.visualize(problem);
    Assert.Equal(<node count>, graph.nodes.Count);
    Assert.Equal(<link count>, graph.links.Count);
}

[Fact]
public void PROBLEM_SolvedVisualization_Marks_Solution() {
    var problem = new PROBLEM("<small instance>");
    var viz = new YourVisualization();
    var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "<a solution>");
    Assert.Contains(graph.nodes, n => n.name == "<a node in the solution>" && n.color == "Solution");
}
```

Aim for at least: one test for `visualize`, one for `SolvedVisualization`, and one for an empty or bad solution string.

## 7. Build, test, and get CI green

### On the Redux side (every visualization PR)

Run these from the repo root (the folder that contains `Redux.slnx`). The full explanation, with what each result looks like and what to do when CI is red, is in [building-and-testing.md](building-and-testing.md).

```bash
dotnet format Redux.slnx
dotnet build Redux.slnx -c Release
dotnet test Redux.slnx -c Release --filter "Category!=Performance"
dotnet test Redux.slnx -c Release --filter "Category=Performance"
```

To run only the visualization-related metadata tests while you work:

```bash
dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~VisualizationType_Tests"
```

### On the Redux_GUI side (case (b) only)

Run these from the root of the Redux_GUI folder (see its [README.md](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/README.md) for setup):

```bash
npm ci
npm run format:check
npm run lint
npm run check:visualizations
```

| Check | What it does | When it fails |
| --- | --- | --- |
| `npm run check:visualizations` ([Tools/check-visualization-coverage.mjs](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/Tools/check-visualization-coverage.mjs)) | Required PR check named "visualization coverage" ([main.yml](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/.github/workflows/main.yml)). Every type in the vendored manifest (except `Unimplemented`) needs a renderer key in `Visualizations.js`, and every renderer key needs to be in the manifest. | You added one side and not the other, misspelled the name, or changed the shape of the registry so the text pattern cannot read it. Success prints `check-visualization-coverage: OK`. |
| [manifest-drift.yml](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/.github/workflows/manifest-drift.yml) | A **daily** robot, not a PR check. It downloads Redux's `Documentation/visualization-types.json` from `CSharpAPI` and compares it to the vendored copy. If they differ it opens (or updates) one issue titled "Visualization type manifest has drifted from the API". It never blocks a PR. | The two repos' lists disagree. Fix by merging the other half of your change, or by copying the API's file over the vendored one. |
| `npm run format:check`, `npm run lint` | Biome (formatting) and ESLint (code problems). | Run `npm run format` and `npm run lint:fix` to fix most of it. |
| `npm run test:e2e` | Browser tests against a running API. Not required for adding a type. See Redux_GUI's [TESTING.md](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/TESTING.md). | |

Redux_GUI also runs the rbs gates on PRs, and unlike Redux they are **not soft**. See [building-and-testing.md](building-and-testing.md).

## 8. Checklist before opening the PR

Case (a), always:

- [ ] The problem and its solver already exist.
- [ ] The file is at `Problems/NPComplete/NPC_<PROBLEM>/Visualizations/<ClassName>.cs`, and the namespace matches the folder.
- [ ] The class name is unique in the whole project, and it has a public constructor with no arguments.
- [ ] The class implements `IVisualization<PROBLEM, PAYLOAD>`.
- [ ] `visualizationName`, `visualizationDefinition`, and `contributors` are filled in (no `"TODO"` left).
- [ ] `visualizationType` is a real type from section 2 (not `Unimplemented`), and `solver` is not `null`.
- [ ] If it is the problem's default, the problem's `defaultVisualization` points at it.
- [ ] `SolvedVisualization` handles an empty or bad solution without crashing.
- [ ] You added tests for `visualize` and `SolvedVisualization`.
- [ ] `dotnet format Redux.slnx` was run, and build and tests pass.
- [ ] You read the rbs report on your PR (see [building-and-testing.md](building-and-testing.md)).
- [ ] The PR targets `CSharpAPI`.

Case (b), also:

- [ ] The new type is spelled identically in the enum, `Documentation/visualization-types.json`, `Visualizations.js`, and the vendored `visualizationTypes.json`.
- [ ] The enum and manifest changed in the same Redux PR.
- [ ] The renderer's registry key is a double-quoted string at the start of its `Map` entry, and is not a legacy-style key with spaces.
- [ ] The renderer accepts the props in section 5 and draws a reduction's picture too.
- [ ] `npm run check:visualizations`, `npm run format:check`, and `npm run lint` pass in Redux_GUI.
- [ ] The two PRs link to each other, and the GUI PR was opened first.

## 9. Common mistakes

- **Leaving `visualizationType` as `Unimplemented`** (the template's default). `NoNewUndeclared` fails. Pick a real type.
- **Setting `solver` to `null`.** The `visualize` endpoint then crashes. The template uses the problem's default solver; keep that or give it a real solver. (Use `=>`, not `=`, when it creates the problem.)
- **Adding an enum member without the manifest** (or the reverse). `ManifestMatchesEnum` fails. Change both in the same PR.
- **A type name spelled differently in different places.** `GraphD3` and `Graphd3` are different words. The GUI finds nothing and shows "not renderable".
- **Adding a GUI renderer but not the vendored manifest** (or the reverse). `npm run check:visualizations` fails.
- **Registering a renderer under an old-style key with spaces** (`"My New Type"`). Use the clean name.
- **Assuming the GUI component calls the API itself.** It does not. It receives `problemData` as a prop.
- **Returning the wrong payload class for the type.** For example, returning `API_SET` for a `GraphD3` type. The GUI draws garbage or an error. Match section 2.
- **Reusing a class name.** Class names are keys, ignoring case. A duplicate crashes the registry for everyone.
- **A `SolvedVisualization` that crashes on an empty solution.** The endpoint calls it with whatever the solver returned, which may be empty.
- **Editing `UnimplementedAllowlist`** to turn a failure green. Fix the cause.
- **Writing a controller.** Not needed. Reflection exposes your class.
