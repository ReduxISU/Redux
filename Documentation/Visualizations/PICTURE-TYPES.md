# Picture types

What each Redux picture type looks like, which problems use it, what the backend sends, and how it animates. The screenshots come from the design mockup ([`mockups/design.html`](mockups/design.html)); open it in a browser to click through every example, solver and step. To see the real backend frames drawn the same way, run the frames viewer ([`viewer/README.md`](viewer/README.md)).

- To add a visualization or record solver steps, read [ADDING-A-VISUALIZATION.md](ADDING-A-VISUALIZATION.md).
- To implement a picture type in a frontend, or to see what is merged and what is planned, read [README.md](README.md).

All the types below are **Approved** as designs. Each type's open review questions are listed at the bottom of its tab in the mockup.

## How to read this page

Every picture type has three views, picked with the **Instance / Solved / Steps** switch:

| View | Shows | Comes from |
| --- | --- | --- |
| Instance | The problem as given, nothing highlighted | The payload |
| Solved | The answer, plus checks that show why it's right | The last frame (a Done) |
| Steps | The solver's run, one decision per frame, with a caption | The frames, from the selected solver's steps |

The **side panel** holds the stepper (◀, Play, ▶, a slider and the caption), the answer, the checks, and the key. The key only lists the states that type uses, described in that type's own words.

**On phones and tablets** (up to 1,199 px wide) the side panel moves under the diagram, stepper first, and the key folds closed. On phones (up to 700 px) the diagram keeps a minimum width of 560 px and scrolls sideways on its own, with a "Swipe the diagram sideways" hint; the Reduction view needs about 960 px. "Hover" becomes "Tap" on touch screens.

Every picture and screenshot meets the contrast rules in [README.md](README.md) §4.2 in both light and dark mode: text at 4.5:1 or better, lines and marks at 3:1 or better, including dimmed and faded ones. Fading is done by color, never by opacity.

### What the backend sends

`POST /ProblemProvider/visualize?visualization=<name>&format=frames[&solver=<name>]` returns:

```
{ "type": "<picture type>", "payload": { "kind": …, … }, "frames": [ frame, … ] }
frame = { "event": "Try" | "Accept" | "Reject" | "Backtrack" | "Done", "caption", "focus": [ids],
          "ok"?: bool,          // Done only
          …state maps and annotations for that type }
```

- **State maps** look like `"nodes": { "<id>": { "state": "Solution" } }`. Elements not listed are `Background`.
- **Frames:** one per kept solver step, then a solved Done frame if the steps didn't end in one. A run stopped by the time limit ends on a blank Done frame with `ok: false` and a caption that starts "Stopped after".
- **Ids are text** and the payload names both ends of every edge, so a renderer never splits an id. Names with separator characters are escaped with a backslash.

**States** are the shared names from [ADDING-A-VISUALIZATION.md](ADDING-A-VISUALIZATION.md#the-shared-states): `Background`, `ElementHighlight` (amber ring), `Solution` (green), `Rejected` (red), `Untraveled` (faded), `Covered` (light green), `Blocked` (dashed grey outline), `False` (assigned false). Annotations such as order badges, ×2 badges, missing-edge lines and group colors are not states.

**When things turn green.** A brute force judges whole candidates: its Try and Reject candidates are `ElementHighlight`, and only an accepted candidate or a correct Done is `Solution`. An incremental solver (`IIncrementalSolver`: greedy, backtracking, branch and bound, dynamic programming) keeps its committed pieces `Solution` and highlights only the piece under test. Board, Geometry and the Graph tour shape are stricter: green only for the confirmed answer. Each section below says which rule it follows.

### Index

| Picture type (`type`) | Problems | Backend interface |
| --- | --- | --- |
| [Graph](#graph) (`Graph`) | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set, Minimum Spanning Tree, Steiner Tree, Cut, Weighted Cut, Max Cut, Minimum Cut, Graph Coloring, Clique Cover, Hamiltonian Cycle, Directed Hamiltonian Cycle, Traveling Salesperson, Minimum Traveling Salesperson | `IGraphSubsetVisualization`, `IGraphEdgesVisualization`, `IGraphCutVisualization`, `IGraphGroupsVisualization`, `IGraphTourVisualization` |
| [Grouped Graph](#grouped-graph-clause-graph) (`GroupedGraph`) | SAT, 3SAT | `IGroupedGraphVisualization` |
| [Bipartite Graph](#bipartite-graph) (`BipartiteGraph`) | Set Cover, Exact Cover, Hitting Set; SAT and 3SAT as the Factor Graph | `IBipartiteVisualization`, `IFactorGraphVisualization` |
| [Automaton](#automaton) (`Automaton`) | DFA, NFA | `IAutomatonVisualization` |
| [Layered Graph](#layered-graph) (`LayeredGraph`) | Topological Sort, Feedback Arc Set, Feedback Node Set, Strongly Connected Components, Single-Source and Single-Pair Shortest Path | `ILayeredVisualization` |
| [Flow Network](#flow-network) (`FlowNetwork`) | Minimum S-T Cut | `IFlowVisualization` |
| [Packing](#packing) (`Packing`) | Partition, Subset Sum, Knapsack, Bin Packing | `IPackingVisualization` |
| [Schedule](#schedule) (`Schedule`) | Job Sequencing, both Pump Scheduling problems | `IJobScheduleVisualization`, `IPumpScheduleVisualization` |
| [Quantum Circuit](#quantum-circuit) (`QuantumCircuit`) | Deutsch, Deutsch-Jozsa, Bernstein-Vazirani, Simon's Problem, Unstructured Search, Prime Factorization | `IQuantumCircuitVisualization` |
| [Board](#board) (`Board`) | Sudoku, N-Queens | `IBoardVisualization` |
| [Table and Trace Table](#table-and-trace-table) (`Table`) | Edit Distance, 0-1 Integer Programming; trace tables for DFA, NFA, SSSP, SPSP | `ITableVisualization` |
| [Tripartite Matching, Geometry, Code Tree](#tripartite-matching-geometry-and-code-tree) (`TripartiteMatching`, `Geometry`, `CodeTree`) | 3-Dimensional Matching, Convex Hull, Lossless Data Compression | `ITripartiteVisualization`, `IGeometryVisualization`, `ICodeTreeVisualization` |
| [Search Tree](#search-tree-and-game-tree) (`SearchTree`) | Second view for 3SAT, Graph Coloring, Traveling Salesperson, N-Queens, 3-Dimensional Matching | `ISearchTreeVisualization` |
| [Reduction](#reduction) (`Reduction`) | 20 reductions (listed in that section) | `ReductionView`, endpoint `reductionView` |
| [Game Tree](#search-tree-and-game-tree) | QBF, Generalized Geography | *Planned* (#662) |
| [Boolean Circuit and Grammar](#boolean-circuit-and-grammar) | Circuit Value, Circuit-SAT, CFG Membership | *Planned* (#662) |
| [Tape and Stack Machines](#tape-and-stack-machines) | Turing machine, LBA, PDA | *Planned* (#662) |
| [Dominoes and Two-Graph Mapping](#dominoes-and-two-graph-mapping) | Post Correspondence, Graph Isomorphism, Subgraph Isomorphism | *Planned* (#662) |

The planned types are designed so their problems can be added to Redux later (backend PR 7, #662). They have mockups but no backend code.

---

## Graph

One renderer for every plain graph problem. A visualization hands over its graph and says what kind of answer it has. The builder works out every state from the steps, so no problem writes its own highlight loop.

<table><tr>
<td><img src="images/graph--clique-solved.png" alt="Clique, solved" width="420"></td>
<td><img src="images/graph--clique-steps.png" alt="Clique, mid-run" width="420"></td>
</tr><tr><td>Clique, Solved</td><td>Clique, Steps (brute force: the candidate is highlighted, not green)</td></tr></table>

**Shapes**

| Shape | Interface, spec, step shape | Problems (`rule`) | Layout | How the answer reads |
| --- | --- | --- | --- | --- |
| `subset` | `IGraphSubsetVisualization`, `GraphSubsetSpec(Nodes, Edges, K, SubsetRule)`, `NodeSet` | Clique (`clique`), Vertex Cover (`vertexCover`), Minimum Vertex Cover (`minVertexCover`), Independent Set (`independentSet`), Dominating Set (`dominatingSet`) | force | Chosen nodes green. Vertex Cover: edges with a chosen end `Covered`, others `Rejected` once judged. Clique: a missing edge between chosen nodes is a `Rejected` phantom. Independent Set: an edge inside the set is `Rejected`, unchosen neighbors `Blocked`. Dominating Set: dominated nodes `Covered`. |
| `edges` | `IGraphEdgesVisualization`, `GraphEdgeSpec(Nodes, Edges, EdgeRule) { Terminals, K }`, `EdgeSet` | Minimum Spanning Tree (`spanningTree`), Steiner Tree (`steinerTree`) | force | Chosen edges green, their ends `Covered`. Steiner terminals carry `terminal: true` (a double outline). |
| `cut` | `IGraphCutVisualization`, `GraphEdgeSpec` with a cut rule, `Groups` (0 = S side, 1 = T side) | Cut (`cut`), Weighted Cut (`weightedCut`), Max Cut (`maxCut`), Minimum Cut (`minCut`) | force | The two sides come as `groups` (colors). Edges that cross are green. |
| `groups` | `IGraphGroupsVisualization`, `GraphGroupSpec(Nodes, Edges, K, GroupRule)`, `Groups` | Graph Coloring (`coloring`), Clique Cover (`cliqueCover`) | force | Nodes take their group's color, never green. Coloring: an edge inside a group is `Rejected`. Clique Cover: edges inside a group green, a missing one a `Rejected` phantom. A group index past K is `Rejected`. |
| `tour` | `IGraphTourVisualization`, `GraphTourSpec(Nodes, Edges, Directed, K, TourRule)`, `Tour` | Hamiltonian Cycle and Directed Hamiltonian Cycle (`hamiltonianCycle`), Traveling Salesperson (`tsp`), Minimum Traveling Salesperson (`minTsp`) | circle | `order` gives the tour so far (order badges). Tour edges green; a hop with no edge is a `Rejected` phantom. Tour nodes are `Covered` while running and `Solution` only on a correct Done. |

Cut and groups are separate shapes. Directed Hamiltonian Cycle is a directed tour (`subtype: "directed"`, edge ids `a>b`), not a Layered Graph. Traveling Salesperson is the decision version (a tour within K, sent as `k`); Minimum Traveling Salesperson is the separate NP-Hard problem with no K.

**Payload**

```
{ "kind": "graph", "subtype": "undirected" | "directed", "layout": "force" | "circle",
  "shape": "subset" | "edges" | "cut" | "groups" | "tour", "rule": …, "k"?: int,
  "nodes": [{ "id", "label", "terminal"?: true }],
  "edges": [{ "id", "a", "b", "weight"?: number, "label"?: "2,5" }] }
```

**Frame:** `event, caption, focus, ok?`, `nodes` and `edges` state maps, `phantoms: [{ a, b, state }]`, `groups: { nodeId: index }` (cut and groups shapes), `order: [ids]` (tour shape).

**Edges.** Ids are `a-b` (undirected, ends in ordinal order) or `a>b` (directed). A `-` or `>` inside a name is escaped with `\`. Parallel edges become one edge: `weight` is their sum for Weighted Cut, Max Cut and Minimum Cut, and the lightest for every other weighted problem; `label` lists every weight. Exact duplicates are dropped, and so is a reversed identical copy in an undirected graph. Undirected graph problems refuse a loop with a 400 at parse time (#836, #882), and the builder leaves out any loop it is given.

**Steps.** Each frame is the selected solver's answer so far plus its focus. The builder re-derives every state from that, so a greedy and a brute force solver animate on the same picture with no extra code. In a brute force the candidate is `ElementHighlight`; in an incremental solver the committed part is `Solution` and the focus is highlighted, or `Rejected` on a Reject.

**Layout.** Deterministic: the same instance always lands in the same place. A Force / Circle switch lets you override the default.

<details><summary>Every Graph problem, solved</summary>

<table>
<tr><td><img src="images/graph--vertex-cover-solved.png" alt="Vertex Cover" width="420"><br>Vertex Cover</td><td><img src="images/graph--minimum-vertex-cover-solved.png" alt="Minimum Vertex Cover" width="420"><br>Minimum Vertex Cover</td></tr>
<tr><td><img src="images/graph--independent-set-solved.png" alt="Independent Set" width="420"><br>Independent Set</td><td><img src="images/graph--dominating-set-solved.png" alt="Dominating Set" width="420"><br>Dominating Set</td></tr>
<tr><td><img src="images/graph--graph-coloring-solved.png" alt="Graph Coloring" width="420"><br>Graph Coloring</td><td><img src="images/graph--clique-cover-solved.png" alt="Clique Cover" width="420"><br>Clique Cover</td></tr>
<tr><td><img src="images/graph--max-cut-solved.png" alt="Max Cut" width="420"><br>Max Cut</td><td><img src="images/graph--minimum-cut-solved.png" alt="Minimum Cut" width="420"><br>Minimum Cut</td></tr>
<tr><td><img src="images/graph--cut-solved.png" alt="Cut" width="420"><br>Cut</td><td><img src="images/graph--weighted-cut-solved.png" alt="Weighted Cut" width="420"><br>Weighted Cut</td></tr>
<tr><td><img src="images/graph--minimum-spanning-tree-solved.png" alt="Minimum Spanning Tree" width="420"><br>Minimum Spanning Tree</td><td><img src="images/graph--steiner-tree-solved.png" alt="Steiner Tree" width="420"><br>Steiner Tree</td></tr>
<tr><td><img src="images/graph--hamiltonian-cycle-solved.png" alt="Hamiltonian Cycle" width="420"><br>Hamiltonian Cycle</td><td><img src="images/graph--traveling-salesperson-solved.png" alt="Traveling Salesperson" width="420"><br>Traveling Salesperson</td></tr>
</table>

Directed Hamiltonian Cycle and Minimum Traveling Salesperson have no Graph screenshot yet. The mockup draws Directed Hamiltonian Cycle as a Layered Graph (below); the backend draws it as a directed tour here.
</details>

---

## Grouped Graph (clause graph)

One node per literal, grouped into one cluster per clause, with the clusters on a ring. Edges join a literal to its complement in another clause, so a satisfying assignment shows up as one green literal in every cluster. Hovering a literal traces that variable everywhere it appears.

<table><tr>
<td><img src="images/clause--clause-graph-solved.png" alt="Clause graph, solved" width="420"></td>
<td><img src="images/clause--clause-graph-steps.png" alt="Clause graph, backtracking" width="420"></td>
</tr><tr><td>Solved: one true literal per clause</td><td>Steps: C2 has no true literal left, so the solver backs out</td></tr></table>

**Backend:** `IGroupedGraphVisualization`, spec `GroupedGraphSpec(Clauses, Variables)` (usually `GroupedGraphSpec.FromLiterals(problem.clauses)`), step shape `Assignment`. SAT and 3SAT get identical pictures from the same code.

**Payload**

```
{ "kind": "groupedGraph", "layout": "ring", "shape": "assignment", "rule": "satisfiability",
  "groups":    [{ "id": "c0", "label": "C1", "members": [literal ids] }],
  "nodes":     [{ "id": "c0-1", "label": "¬x2", "variable": "v:x2", "positive": false, "group": "c0" }],
  "variables": [{ "id": "v:x2", "label": "x2" }],
  "edges":       [{ "id", "a", "b" }],   // complement pairs across clauses
  "clauseEdges": [{ "id", "a", "b" }] }  // every pair inside a clause
```

**Frame:** `event, caption, focus, ok?`, `nodes` (literal states), `groups` (clause states; a state map here, not group indices), `assignment: { variableId: bool }`.

**States.** Literals of an unassigned variable `Background`; the variable being tried `ElementHighlight` on its occurrences; true literals green, and their clause `Covered`; false literals `False`; a clause whose literals are all false `Rejected`, with its literals.

**Edges.** An Edges switch shows complement pairs (default) or adds the within-clause edges, which makes this exactly the 3SAT → Independent Set graph.

**Ties to reductions.** Clause and literal ids (`c{i}`, `c{i}-{j}`, `v:{name}`) are the ids gadgets use, so the 3SAT reductions link straight to this picture.

---

## Bipartite Graph

Elements on one side, sets on the other, and an edge for every membership. The certificate picks from one side, marked **● PICKED**, and constrains the other.

<table><tr>
<td><img src="images/bip--set-cover-solved.png" alt="Set Cover, solved" width="420"></td>
<td><img src="images/bip--set-cover-steps.png" alt="Set Cover, greedy steps" width="420"></td>
</tr><tr><td>Set Cover, Solved</td><td>Set Cover, Steps</td></tr>
<tr>
<td><img src="images/bip--exact-cover-solved.png" alt="Exact Cover" width="420"></td>
<td><img src="images/bip--exact-hitting-set-solved.png" alt="Exact Hitting Set" width="420"></td>
</tr><tr><td>Exact Cover (picks sets, each element exactly once)</td><td>Hitting Set, as implemented: Exact Hitting Set (picks elements, each set hit exactly once)</td></tr></table>

**Backend:** `IBipartiteVisualization`, spec `BipartiteSpec(Universe, Sets, K, BipartitePick, BipartiteRule)`.

| Problem | `pickSide` | `rule` | Step shape |
| --- | --- | --- | --- |
| Set Cover | `sets` | `atLeastOnce` | `ChosenSets` (`S1`, `S2`, …) |
| Exact Cover | `sets` | `exactlyOnce` | `ChosenSets` |
| Hitting Set (#698: exactly once) | `elements` | `exactlyOnce` | `NodeSet` |

**Payload**

```
{ "kind": "bipartiteGraph", "layout": "columns", "pickSide", "rule", "k"?,
  "elements": [{ "id": "e:1", "label": "1" }],
  "sets":     [{ "id": "S1", "label", "members": [element ids] }],
  "edges":    [{ "id": "e:1~S1", "a", "b" }] }
```

**Frame:** `event, caption, focus, ok?`, `nodes`, `edges`, `hits: { id: count }` (drawn as ×N badges).

**States.** Picked nodes and their edges green (highlighted while a brute force tries them); constraints hit the required number of times `Covered`; hit twice under exactly-once `Rejected`; unhit `Rejected` once a whole answer is judged; under exactly-once, an unpicked choice that would double up `Blocked`.

**Layout.** The set column is sorted by the average position of its elements to cut crossings. Names stay in instance order (S1, S2, …) so they match the certificate.

**Factor Graph for SAT.** `IFactorGraphVisualization` (SatFactorGraphVisualization, Sat3FactorGraphVisualization) uses the same type with `pickSide: "assignment"`: variables (`v:x1`) on the left, clauses (`c0`) on the right, one edge per literal (id `c{i}-{j}`, `positive` false for ¬x, drawn dashed). Step shape `Assignment`. True variables and true-literal edges green, false ones `False`; a clause with a true literal `Covered`, with none `Rejected`.

<img src="images/clause--factor-graph-solved.png" alt="SAT factor graph" width="640">

**Wire change.** The set problems still send their nested set list in the old list format. The node-link payload is only under `format=frames`.

---

## Automaton

States laid out left to right in the order the machine reaches them. The start state gets an incoming arrow and accept states get a double ring. Transitions between the same two states share one arrow with a combined label. The input tape underneath shows what has been read.

<table><tr>
<td><img src="images/auto--dfa-ends-in-ab-input-abaab.png" alt="DFA mid-run" width="420"></td>
<td><img src="images/auto--nfa-contains-ab-with.png" alt="NFA mid-run, all branches" width="420"></td>
</tr><tr><td>DFA, Steps</td><td>NFA, Steps (all branches at once)</td></tr></table>

**Backend:** `IAutomatonVisualization`, spec `AutomatonSpec(AutomatonKind.Dfa | Nfa, States, Alphabet, Transitions, Start, Accepting, Input)` with `AutomatonTransition(From, Symbol, To)`; step shape `ActiveStates(states, position)`.

**Payload**

```
{ "kind": "dfa" | "nfa", "layout": "layered",
  "states": [{ "id": "q:1", "label": "1", "layer": 0 }], "start": "q:1", "accepting": [ids],
  "alphabet": ["a", "b"], "input": ["a", "b", "a"],
  "transitions": [{ "id": "q:1>q:2", "a", "b", "label": "a,b", "symbols": ["a", "b"], "epsilon": false }],
  "garbage"?: { "id": "garbage", "label": "Garbage", "missing": [{ "state", "symbol" }] },   // DFA
  "paths"?:   [{ "id": "p1", "label", "accepted", "outcome", "frames": [...] }], "pathsTruncated"? }  // NFA
```

**Frame:** `event, caption, focus, ok?`, `position` (symbols read), `active: [state ids]`, `nodes`, `edges`.

**States.** Active states `ElementHighlight` while running; the arrow just taken `ElementHighlight`. On Done, an accepted run's final accept states are green, a rejected run's active states `Rejected`, and states the run never reached `Untraveled`.

**Decided behavior**

- **DFA missing transitions** go to a separate, isolated **Garbage** state drawn in red. A run that falls in stays there and is rejected. A symbol outside the alphabet is a Reject on the current state, not Garbage.
- **NFA** shows all branches at once by default. A **One path** switch follows a single run, chosen from a dropdown. The backend computes the runs (`paths`, up to 40, accepting ones first), each with its outcome (`accepts`, `rejects` or `dies`) and its own frames.
- **ε-transitions** (NFA) are dashed (`epsilon: true`).
- **Edge labels stay strings**, never fed to a number scale.

A **Trace Table** view (one row per step) is available as a second visualization; see [Table](#table-and-trace-table).

<details><summary>More automaton examples</summary>

<table><tr>
<td><img src="images/auto--dfa-ends-in-ab-input-abba.png" alt="DFA, rejected input" width="420"><br>DFA, input that ends rejected</td>
<td><img src="images/auto--nfa-redux-default.png" alt="NFA, Redux default" width="420"><br>NFA, Redux's default instance</td>
</tr></table>
</details>

---

## Layered Graph

Directed graphs drawn left to right in ranks, so most edges point forward. Edges that point backward close a cycle; they run as curves under the rows, where they're easy to spot.

<table><tr>
<td><img src="images/layered--topological-sort-solved.png" alt="Topological Sort, solved" width="420"></td>
<td><img src="images/layered--topological-sort-steps.png" alt="Topological Sort, steps" width="420"></td>
</tr><tr><td>Topological Sort, Solved</td><td>Topological Sort, Steps</td></tr></table>

**Backend:** `ILayeredVisualization`, spec `LayeredSpec(Nodes, Edges, LayeredRule) { Directed, K, Source, Target }` with `LayeredEdgeInput(From, To, Weight)`. The step shape depends on the rule (`LayeredFrames.StepShape(rule)`):

| Problem (`rule`) | `shape` | Step shape | How it reads |
| --- | --- | --- | --- |
| Topological Sort (`topologicalSort`) | `order` | `Tour` | Order badges; placed nodes green, nodes ready to place `Covered`; a placed edge that points backward `Rejected` |
| Feedback Arc Set (`feedbackArcSet`) | `removedSet` | `EdgeSet` | Removed edges are the answer (green, or highlighted while a brute force tries them); a cycle that survives `Rejected` |
| Feedback Node Set (`feedbackNodeSet`) | `removedSet` | `NodeSet` | Removed nodes are the answer; their edges `Untraveled`; a surviving cycle `Rejected` |
| Strongly Connected Components (`stronglyConnectedComponents`) | `groups` | `Components` | Components as group colors; visited nodes `Covered`; Kosaraju's finish order in `finish` |
| SSSP (`singleSourceShortestPath`), SPSP (`singlePairShortestPath`) | `distances` | `Distances` | Each node's distance under it, ∞ until reached. Settled nodes and their tree edges green, reached ones `Covered`, unreachable ones `Untraveled`. SPSP ends with only the path green. |

**Payload**

```
{ "kind": "layeredGraph", "layout": "ranked", "shape", "rule", "directed": true, "weighted",
  "nodes": [{ "id", "label", "layer", "row" }], "layers": [[ids]],
  "edges": [{ "id": "a>b", "a", "b", "back": bool, "weight"?, "label"? }],
  "k"?, "source"?, "target"? }
```

**Frame:** `event, caption, focus, ok?`, `nodes`, `edges`, and per shape `order`, `groups` + `finish`, or `dist` (null = ∞) + `pred`.

**Edges and loops.** Parallel edges merge, keeping the lightest weight and listing all in `label`. The directed feedback problems, SSSP and SPSP accept loops; a loop is drawn as a back edge.

**Layout.** Break cycles by depth-first search (from the source first), rank by longest path, order each rank by the average position of its neighbors, and draw back edges as curves under the rows. The backend sends the layers and rows.

<details><summary>Every Layered Graph problem, solved</summary>

<table>
<tr><td><img src="images/layered--feedback-arc-set-solved.png" alt="Feedback Arc Set" width="420"><br>Feedback Arc Set</td><td><img src="images/layered--feedback-node-set-solved.png" alt="Feedback Node Set" width="420"><br>Feedback Node Set</td></tr>
<tr><td><img src="images/layered--strongly-connected-components-solved.png" alt="Strongly Connected Components" width="420"><br>Strongly Connected Components</td><td><img src="images/layered--single-source-shortest-path-solved.png" alt="Single-Source Shortest Path" width="420"><br>Single-Source Shortest Path</td></tr>
<tr><td><img src="images/layered--single-pair-shortest-path-solved.png" alt="Single-Pair Shortest Path" width="420"><br>Single-Pair Shortest Path</td><td><img src="images/layered--directed-hamiltonian-cycle-solved.png" alt="Directed Hamiltonian Cycle in the mockup" width="420"><br>Directed Hamiltonian Cycle, as the mockup draws it. The backend draws it as a directed tour in the <a href="#graph">Graph</a> picture.</td></tr>
</table>
</details>

---

## Flow Network

The source sits at the far left and the sink at the far right; every other node is placed by its hop distance from the source. Each edge reads flow/capacity and gets thicker as it fills.

<table><tr>
<td><img src="images/flow--flow-solved.png" alt="Flow network, solved" width="420"></td>
<td><img src="images/flow--flow-steps.png" alt="Flow network, augmenting path" width="420"></td>
</tr><tr><td>Solved: the S side, the T side and the cut edges</td><td>Steps: pushing flow along one augmenting path</td></tr></table>

**Backend:** `IFlowVisualization`, spec `FlowSpec(Nodes, Edges, Source, Sink)` with `FlowEdgeInput(From, To, Capacity)`; step shape `NetworkFlow` (`flow`, `path`, `value`, `side`). Used by Minimum S-T Cut.

**Payload**

```
{ "kind": "flowNetwork", "layout": "sourceToSink", "rule": "minimumSTCut", "source", "sink",
  "nodes": [{ "id", "label", "layer", "row" }], "layers": [[ids]],
  "edges": [{ "id": "a>b", "a", "b", "capacity", "label"? }] }
```

**Frame:** `event, caption, focus, ok?`, `nodes`, `edges`, `flow: { edgeId: units }`, `value`, `path: [{ edge, forward, units }]`, and on a correct Done `groups` (0 = S, 1 = T) and `cut`.

**Steps.** One frame per augmenting path: the flow on every edge and the path used, with backward residual arcs marked (`forward: false`). Full edges are `Covered`.

**Ending.** When no path is left, the nodes the source can still reach form side S. The full edges from S to T are green and add up to the maximum flow; edges from T back to S are `Untraveled`.

**Parallel edges** between the same two nodes in the same direction add their capacities; `label` lists each one.

---

## Packing

Every number problem here is the same question in a different container: which items go where so a total hits its mark. Items sit in a pool at the top; containers are drawn to scale below with their line. Anything past a limit turns red.

<table><tr>
<td><img src="images/packing--partition-solved.png" alt="Partition, solved" width="420"></td>
<td><img src="images/packing--partition-steps.png" alt="Partition, steps" width="420"></td>
</tr><tr><td>Partition, Solved</td><td>Partition, Steps</td></tr>
<tr>
<td><img src="images/packing--knapsack-binary-solved.png" alt="Knapsack" width="420"></td>
<td><img src="images/packing--bin-packing-solved.png" alt="Bin Packing" width="420"></td>
</tr><tr><td>Knapsack: a weight limit and a value goal</td><td>Bin Packing: one container per bin</td></tr></table>

**Backend:** `IPackingVisualization`, spec `PackingSpec(Items, PackingRule) { Target, Limit, AtLeast, Bins }` with `PackingItemInput(Size, Value, Label)`; step shape `Placement` (the container of each item, −1 = left out), so one drawing serves every problem.

| Problem (`rule`) | Containers |
| --- | --- |
| Partition (`partition`) | S1 and S2, each with a target of half the total |
| Subset Sum (`subsetSum`) | "Chosen", with target T |
| Knapsack (`knapsack`) | "Knapsack", with weight limit W and value goal V (`atLeast`) |
| Bin Packing (`binPacking`) | "Bin 1" … up to K, each with capacity C; `maxBins` = K |

**Payload:** `{ kind: "packing", rule, items: [{ id: "I1", label, size, value? }], containers: [{ id: "C1", label, target?, limit?, atLeast? }], total, maxBins? }`.

**Frame:** `event, caption, focus, ok?`, `items`, `containers`, `assign: [container index per item]`, `totals: [{ size, value? }]`.

**States.** Placed items green (highlighted while a brute force tries a whole placement); a container over its limit `Rejected`; a container whose line is met `Covered`; a missed target `Rejected` once a whole placement is judged; items left out of a correct answer `Untraveled`.

<details><summary>Subset Sum</summary>

<img src="images/packing--subset-sum-solved.png" alt="Subset Sum" width="640">

The mockup also shows Subset Product on a log scale. That problem lives only on a demo branch, not in Redux.
</details>

---

## Schedule

Time runs left to right, drawn to scale. Both problems use the `Schedule` type; `payload.rule` tells them apart.

<table><tr>
<td><img src="images/schedule--job-sequencing-solved.png" alt="Job Sequencing" width="420"></td>
<td><img src="images/schedule--pump-scheduling-cost-minimization-solved.png" alt="Pump Scheduling" width="420"></td>
</tr><tr><td>Job Sequencing: one row per job, deadline ticks</td><td>Pump Scheduling: 24 hourly rows, tank level and cost below</td></tr></table>

**Job Sequencing** (`IJobScheduleVisualization`, spec `JobScheduleSpec(Jobs, K)` with `ScheduleJob(Time, Deadline, Penalty)`, step shape `JobOrder`) gives each job a row. Its bar is when it runs on the one machine, the tick is its deadline, and a job that ends past the tick turns `Rejected` and costs its penalty. Jobs are numbered from 0 (`J0`, "Job 0"), as in the instance (#846).

```
payload: { kind: "schedule", rule: "jobSequencing", jobs: [{ id, label, time, deadline, penalty }], k, span }
frame:   order, runs: [{ job, start, end, late }], jobs (states), penalty
```

**Pump Scheduling** (`IPumpScheduleVisualization`, spec `PumpScheduleSpec(...)`, step shape `PumpPlan`) gives each pump a row of 24 hours, with the tank level and the cost of each hour charted underneath on the same axis. Shaded hours are the peak tariff. A startup mark shows when a pump turns on and pays its startup cost.

```
payload: { kind: "schedule", rule: "costMinimization" | "emergencyResilience",
           hours: [{ id: "t0", label: "00:00", demand, peak }], pumps: [{ id, name, flow, power, startupCost }],
           tank: { capacity, level, minimum }, rates: { onPeak, offPeak }, budget? }
frame:   reached: [{ states, low, high, best }], schedule: [{ hour, on, starts, inflow, level, energy, startup }],
         upTo, hours, cells (P1t0 …), budget?
```

**Steps.** Job Sequencing records the orders tried (a brute force). Pump Scheduling records three phases: the dynamic program's forward pass (the band of tank levels still reachable, hour by hour, in `reached`), the trace back, and the chosen schedule replayed hour by hour (`upTo`). Pumps that are on are green; an hour whose tank level leaves its bounds is `Rejected`.

<details><summary>Steps and Emergency Resilience</summary>

<table><tr>
<td><img src="images/schedule--job-sequencing-steps.png" alt="Job Sequencing steps" width="420"><br>Job Sequencing, Steps</td>
<td><img src="images/schedule--pump-scheduling-emergency-resilience-solved.png" alt="Emergency Resilience" width="420"><br>Pump Scheduling, Emergency Resilience</td>
</tr></table>
</details>

---

## Quantum Circuit

One wire per qubit, gates in the order they run, and the oracle drawn as a box. Each step applies one column of gates. Bars underneath show how likely each outcome is if you measured right then; bar color shows the amplitude's sign, so you can watch the oracle flip signs and the last gates turn them into one clear answer.

<table><tr>
<td><img src="images/quantum--bernstein-vazirani-solved.png" alt="Bernstein-Vazirani, solved" width="420"></td>
<td><img src="images/quantum--bernstein-vazirani-steps.png" alt="Bernstein-Vazirani, steps" width="420"></td>
</tr><tr><td>Bernstein-Vazirani, Solved</td><td>Bernstein-Vazirani, Steps</td></tr></table>

**Backend:** `IQuantumCircuitVisualization`. `Describe` returns a `QuantumCircuitSpec` from one of the `QuantumCircuits` factories (`Deutsch`, `DeutschJozsa`, `BernsteinVazirani`, `Simon`, `UnstructuredSearch`, `PrimeFactorization`). There is no `ParseAnswer`. Step shape `QuantumProgress` (`applied`, `queries`, `outcome`, `counts`, `result`).

**Payload**

```
{ "kind": "quantumCircuit", "problem": "bernsteinVazirani" | …,
  "qubits":  [{ "id": "q0", "name": "x0", "role": "query" | "helper" | "output" | "index" | "count" | "work" }],
  "columns": [{ "id": "t0", "gates": [{ "id", "type": "h" | "x" | "z" | "cx" | "measure" | "box",
                                        "qubits": [int], "control"?, "label"?, "oracle"? }] }],
  "stages":  [{ "label", "from", "to" }],
  "measure": [qubit indices], "readout": "bits" | "decimal", "inputBits", "simulated": bool, "note"?, "number"? }
```

**Frame:** `event, caption, focus, ok?`, `applied` (columns applied so far), `queries: [{ input, output }]` (classical oracle calls), `columns`, `gates`, `qubits` (states), `bars?: [{ outcome, probability, sign }]`, `outcome?`, `counts?`, `result?`.

**States.** The column being applied and its gates `ElementHighlight`. Measured qubits turn green only when a measurement is accepted or the Done is correct, and `Rejected` otherwise. For a classical solver that applies no columns, the circuit ends `Untraveled`.

**Decided behavior**

- **Measured qubits are listed high bit first** (`measure`), so a measured outcome reads as the item number (#848).
- **Grover** (Unstructured Search) records one Try per column, then an Accept only if the measured item is marked, and a Reject if not. A missed measurement is never green (#847).
- **Shor** (Prime Factorization) is drawn but not simulated (`simulated: false`); the frontend may simulate it.
- Probabilities come from a statevector simulation in the backend, up to 16 qubits. This replaces today's two formats (a D3 gate list or raw OpenQASM behind a flag); QASM becomes an export.

<details><summary>The other quantum problems</summary>

<table>
<tr><td><img src="images/quantum--deutsch-solved.png" alt="Deutsch" width="420"><br>Deutsch</td><td><img src="images/quantum--deutsch-jozsa-solved.png" alt="Deutsch-Jozsa" width="420"><br>Deutsch-Jozsa</td></tr>
<tr><td><img src="images/quantum--simon-s-problem-solved.png" alt="Simon's Problem" width="420"><br>Simon's Problem</td><td><img src="images/quantum--unstructured-search-solved.png" alt="Unstructured Search" width="420"><br>Unstructured Search (Grover)</td></tr>
<tr><td><img src="images/quantum--prime-factorization-solved.png" alt="Prime Factorization" width="420"><br>Prime Factorization (Shor)</td><td></td></tr>
</table>
</details>

---

## Board

A grid of squares for puzzles that live on a board.

<table><tr>
<td><img src="images/board--sudoku-steps.png" alt="Sudoku, steps" width="420"></td>
<td><img src="images/board--n-queens-solved.png" alt="N-Queens, solved" width="420"></td>
</tr><tr><td>Sudoku, Steps (this screenshot predates #839: placed digits are now plain until the answer is confirmed)</td><td>N-Queens, Solved</td></tr></table>

**Backend:** `IBoardVisualization`, spec `BoardSpec(BoardGame.Sudoku | Queens, Size) { Box, LabelsFrom, Clues, PieceKind }`; step shape `BoardCells` (cell values and conflicts). Cell ids are `r{row}c{column}`, from 0.

```
payload: { kind: "board", game: "sudoku" | "queens", size, box?, labelsFrom, clues: [{ id, r, c, value }] }
frame:   values: { cellId: text }, pieces: [{ id, r, c, kind: "queen" }], cells (states),
         links: [{ from, to, state }]
```

**Sudoku** shows clues in bold and the solver's digits plain, with the square being tried highlighted. A clash is a `Rejected` link between two squares in the same row, column or box that hold the same digit.

**N-Queens** shows each queen as it lands. A square ruled out gets a `Rejected` link back to the queen attacking it. A Hints switch shades every attacked square.

**Green only for the confirmed answer (#839).** Squares the solver filled are plain while it works, `ElementHighlight` while a brute force tries a whole board, green only on a Done with `ok: true`, and `Rejected` on a failed Done. Squares, pieces and links are all a board needs, so later grid puzzles (Latin squares, Kakuro) fit the same shape.

---

## Table and Trace Table

Rows and columns instead of dots and lines. Three shapes share one renderer, drawn in HTML rather than SVG.

<table><tr>
<td><img src="images/table--edit-distance-steps.png" alt="Edit Distance, steps" width="420"></td>
<td><img src="images/table--0-1-integer-programming-solved.png" alt="0-1 Integer Programming" width="420"></td>
</tr><tr><td>Grid: Edit Distance, each cell and the neighbors it reads</td><td>Matrix: 0-1 Integer Programming, constraints against variables</td></tr></table>

**Backend:** `ITableVisualization<TProblem, TPartial>`. Unlike the other types, the visualization fills the cells: `Describe` returns a `TableSpec(TableShape, Columns) { RowLabels, Title, RowAxis, ColumnAxis }`, `Draw` returns one `TableDraft` per step, `Blank` the empty table, and `Solved` (optional) the answer. The builder adds ids, focus and the closing frame.

| `shape` | Problems | Step shape | How it reads |
| --- | --- | --- | --- |
| `grid` | Edit Distance | `TableCells` | The cell being computed is `ElementHighlight`, with arrows from the neighbors it read. The last frame draws the cheapest edit path green. |
| `matrix` | 0-1 Integer Programming | `Assignment` | One row per constraint, with its total against the bound. Rows that hold are green, rows over the bound `Rejected`. |
| `trace` | DFA, NFA (`AutomatonTraceTable`), SSSP, SPSP (`DistanceTraceTable`) | the problem's own (`ActiveStates`, `Distances`) | One row per step, added as the run reaches it. Cells that changed this step are underlined. |

```
payload: { kind: "table", shape, columns: [{ id: "c0", label, key? }], rows: [{ id: "r0", label? }],
           title?, rowAxis?, columnAxis? }
frame:   values: [[text | null]], rows (states), cells (states, ids "r0c1"), changed: [cell ids],
         arrows?: [{ from, to, best }]    // grid only
```

The trace tables are second visualizations next to the problem's own picture (`DFATableVisualization`, `NFATableVisualization`, `SSSPTableVisualization`, `SPSPTableVisualization`), driven by the same solver steps.

<details><summary>Trace tables</summary>

<table>
<tr><td><img src="images/table--dfa-acceptance-solved.png" alt="DFA trace table" width="420"><br>DFA</td><td><img src="images/table--nfa-acceptance-solved.png" alt="NFA trace table" width="420"><br>NFA</td></tr>
<tr><td><img src="images/table--single-source-shortest-path-problem-solved.png" alt="SSSP trace table" width="420"><br>Single-Source Shortest Path</td><td><img src="images/table--single-pair-shortest-path-problem-solved.png" alt="SPSP trace table" width="420"><br>Single-Pair Shortest Path</td></tr>
</table>
</details>

---

## Tripartite Matching, Geometry and Code Tree

Three problems that aren't graphs, each with its own picture type, following the same rules.

<table><tr>
<td><img src="images/misc--convex-hull-steps.png" alt="Convex Hull, merge step" width="420"></td>
<td><img src="images/misc--3-dimensional-matching-solved.png" alt="3-Dimensional Matching" width="420"></td>
</tr><tr><td>Geometry: Convex Hull, merging two hulls (this screenshot predates #849 and #850: the right hull is now purple, the merge walks a chain, and hull corners stay plain until the answer is confirmed)</td><td>Tripartite Matching: 3-Dimensional Matching</td></tr></table>

**Tripartite Matching** (`TripartiteMatching`; `ITripartiteVisualization`, spec `TripartiteSpec(X, Y, Z, Triples)`, step shape `ChosenTriples`). Three columns X, Y and Z; each triple is a line through its three names. Chosen triples green, names used once `Covered`, triples that share a name with a chosen one `Blocked`, names used twice `Rejected` with a ×2 badge.

```
payload: { kind: "tripartite", x: [{ id: "x1", name }], y, z, triples: [{ id: "T1", x, y, z }] }
frame:   chosen: [triple ids], repeats: { id: count }, elements (states), triples (states)
```

**Geometry** (`Geometry`; `IGeometryVisualization`, spec `GeometrySpec(Points)`, step shape `HullBuild`). Points drawn to scale on real axes. Steps follow Redux's divide-and-conquer solver: the pieces being merged (left and right hull), the chain the merge walks, and dropped points `Rejected`. **Green only for the confirmed hull (#849):** corners stay plain while the solver works, and on a correct Done the hull's corners are green, numbered in output order, and the points inside `Covered`.

```
payload: { kind: "geometry", points: [{ id: "P1", x, y }] }
frame:   pieces: [{ points, role: "piece" | "left" | "right" }], chain, hull (Done only), points (states)
```

**Code Tree** (`CodeTree`; `ICodeTreeVisualization`, spec `CodeTreeSpec(Text)`, step shape `HuffmanForest`). A Huffman tree that grows bottom-up. Each step merges the two lightest trees under a new parent; merged nodes and their edges are green as they are committed. Left edges are 0, right edges are 1; finished characters show their code under them, and the encoded bits run underneath.

```
payload: { kind: "codeTree", text, symbols: [{ id: "c1", code, label, count }] }
frame:   merged: [{ id: "n1", weight, left, right }], roots, codes, nodes, edges ("n1.0", "n1.1"), bits?
```

<img src="images/misc--lossless-data-compression-solved.png" alt="Huffman code tree" width="640">

---

## Search Tree and Game Tree

The solver's choices drawn as a tree that grows step by step. It's a second visualization for a backtracking or branch-and-bound solver.

<table><tr>
<td><img src="images/search--3sat-solved.png" alt="3SAT search tree" width="420"></td>
<td><img src="images/search--quantified-boolean-formula-game-tree-solved.png" alt="QBF game tree" width="420"></td>
</tr><tr><td>Search Tree: 3SAT backtracking</td><td>Game Tree: Quantified Boolean Formula (planned)</td></tr></table>

**Backend:** `ISearchTreeVisualization<TProblem, TPartial>`, spec `SearchTreeSpec(Problem)`. The tree comes only from the steps' `tree` patches (`SolverStep.Tree`), which a solver adds through the recorder overloads that take a `tree` lambda:

```
SearchNode = { nodeId: int, parentId: int | null, label: "x2=F" | "(1,3)" | …,
               state: "queued" | "explored" | "pruned" | "failed" | "solution" | "skipped", bound? }
```

A step is about its last patch. Steps without patches still work in every other picture, so solvers add them one at a time. Today five solvers do: `Sat3BacktrackingSolver`, `GraphColoringBacktracking`, `TSPBranchAndBound`, `NQueensBacktracking` and `ThreeDimensionalMatchingBacktracking`. Knapsack has a search-tree mockup but no backend tree yet.

```
payload: { kind: "searchTree", problem, instance, maxLeaves: 12 }
frame:   nodes: [{ id: "n3", parent, label, kind, state, bound?, order?, hidden? }], total, cut
```

**How it reads.** The node being worked on `ElementHighlight`; explored choices `Covered`; queued nodes `Untraveled` (best-first solvers only). Branches the solver cut without exploring, and branches it explored and backed out of, are `Rejected` (drawn dashed and solid). The answer's path turns green. Best-first solvers get numbered `order` badges for the order nodes left the queue.

**Folding.** Past 12 leaves, the backend folds cut siblings into one "k cut" node and finished branches into "+k more" (`kind: "cut"` and `"more"`).

**Game Trees** (*planned*, #662) add a band per turn and a badge for who wins from each position; branches never needed once a turn is decided are `skipped` (`Untraveled`). `SearchNodeState.Skipped` exists for them; no solver uses it yet.

<details><summary>Other search trees</summary>

<table>
<tr><td><img src="images/search--knapsack-binary-solved.png" alt="Knapsack branch and bound" width="420"><br>Knapsack, branch and bound (mockup only)</td><td><img src="images/search--traveling-salesperson-solved.png" alt="TSP" width="420"><br>Traveling Salesperson</td></tr>
<tr><td><img src="images/search--graph-coloring-solved.png" alt="Graph Coloring" width="420"><br>Graph Coloring</td><td><img src="images/search--n-queens-solved.png" alt="N-Queens" width="420"><br>N-Queens</td></tr>
<tr><td><img src="images/search--generalized-geography-game-tree-solved.png" alt="Generalized Geography" width="420"><br>Generalized Geography (planned)</td><td><img src="images/search--3sat-steps.png" alt="3SAT mid-run" width="420"><br>3SAT, Steps (best-first order badges)</td></tr>
</table>
</details>

---

## Reduction

The instance you gave on the left, the instance the reduction builds on the right. Both panes reuse the picture types above; a reduction never draws anything itself.

<img src="images/reduction--3sat-clique-build.png" alt="3SAT to Clique, build phase" width="760">

*Build: each clause becomes a cluster of three nodes.*

<img src="images/reduction--3sat-clique-map.png" alt="3SAT to Clique, map back" width="760">

*Map back: the clique on the right sets the assignment on the left, piece by piece.*

**Endpoint:** `POST /ProblemProvider/reductionView?reduction=<name>[&solver=<target solver>]`, body = the SOURCE instance. A reduction needs construction steps (`IRecordedReduction`), a backward map (`IReversibleReduction`) and a frames picture on both problems; otherwise the answer is a 400 naming what is missing.

**Three phases, one frame list**

1. **Build** (`phase: "build"`). One frame per construction step, in both problems' own words. The source ids a step came from and the target pieces it added are highlighted. Pairs a rule deliberately leaves unjoined come as `target.skipped` ("rule says no"). Past 150 steps the rest fold into one step, so the build always ends with the whole target.
2. **Solve** (`phase: "solve"`). The target is solved by the reduction's `ReductionViewSolver()` if it names one, else the target's default solver; `?solver=` overrides both. The source pane stays at rest.
3. **Map back** (`phase: "map"`). The target's answer is mapped to the source's answer, one piece at a time. The part mapped so far stays green.

Only the last frame is a Done with `ok`: the source's verifier on the mapped-back answer (#840).

**Response**

```
{ "type": "Reduction", "kind": "reduction", "reduction", "reductionName", "solver",
  "source": { "problem", "instance", "picture": { "type", "payload" } },
  "target": { "problem", "instance", "picture": { "type", "payload" },
              "buildOrder": { "nodes": [ids], "edges": [ids] } },
  "gadgets": [{ "gadgetKind", "sourceIds", "targetIds", "note"? }],
  // gadgetKind ∈ element | group | edgeRule | palette | orGadget | bound
  "frames": [{ "phase", "event", "caption", "ok"?, "bound"?, "gadgets"?: [indices],
               "sourceFocus": [ids], "targetFocus": [ids],
               "source": { …that picture's state fields },
               "target": { …that picture's state fields, "builtNodes"?, "builtEdges"?, "skipped"? } }] }
```

During Build, the target shows the first `builtNodes` nodes and `builtEdges` edges of `buildOrder`, so any frame renders on its own when scrubbed to. Each pane's state fields are exactly that picture's own frame fields (`nodes`, `edges`, `groups`, `assignment`, …).

**Hover and color.** Hovering any piece on either side highlights what it became, through the gadget list. "Color gadgets" gives each gadget and its source a shared color.

**Reductions with a Reduction View today:** Clique → 3SAT, Clique → Vertex Cover, Directed Hamiltonian → Hamiltonian, Exact Cover → Subset Sum, Graph Coloring → Clique Cover, Graph Coloring → Exact Cover, Graph Coloring → SAT, Hitting Set → Exact Cover, Independent Set → Clique, Partition → Weighted Cut, SAT → 3SAT, 3SAT → Clique, 3SAT → 3-Dimensional Matching, 3SAT → Graph Coloring, 3SAT → 0-1 Integer Programming, Subset Sum → Knapsack, Subset Sum → Partition, Vertex Cover → Feedback Arc Set, Vertex Cover → Feedback Node Set, Vertex Cover → Set Cover.

<details><summary>The other two mockup reductions</summary>

<table>
<tr><td><img src="images/reduction--clique-vertex-cover-build.png" alt="Clique to Vertex Cover, build" width="420"><br>Clique → Vertex Cover, Build</td><td><img src="images/reduction--clique-vertex-cover-map.png" alt="Clique to Vertex Cover, map back" width="420"><br>Clique → Vertex Cover, Map back</td></tr>
<tr><td><img src="images/reduction--3sat-graph-coloring-build.png" alt="3SAT to Graph Coloring, build" width="420"><br>3SAT → Graph Coloring, Build</td><td><img src="images/reduction--3sat-graph-coloring-map.png" alt="3SAT to Graph Coloring, map back" width="420"><br>3SAT → Graph Coloring, Map back</td></tr>
</table>
</details>

---

## Boolean Circuit and Grammar

*Planned (#662).* Two picture types defined so Circuit Value, Circuit-SAT and Context-Free Grammar Membership can be added later. There is no backend code yet; the formats below are the mockup's proposal.

<table><tr>
<td><img src="images/cgram--circuit-value-steps.png" alt="Circuit Value, steps" width="420"></td>
<td><img src="images/cgram--context-free-grammar-membership-solved.png" alt="CYK table and parse tree" width="420"></td>
</tr><tr><td>Boolean Circuit: evaluating gate by gate</td><td>Grammar: the CYK table, then the parse tree</td></tr></table>

**Boolean Circuit.** Inputs on the left, gates in evaluation order, the output on the right. A wire carrying 1 is thick and solid, a wire carrying 0 thin and dashed, so values read without color; green stays reserved for the answer. The gate being evaluated and the wires it reads are `ElementHighlight`. Circuit-SAT steps through input assignments.

**Grammar (CYK).** A triangle of cells, shortest substrings first. The cell being filled is `ElementHighlight`, with dashed amber marks on the pairs of cells that produced something. If the top cell holds the start variable, the parse tree is drawn and its cells outlined green. Grammars must be in Chomsky Normal Form.

```
// Circuit:  inputs: string[]; gates: { name, op, in: string[] }[]; output: string; assign?
// Grammar:  V, Σ: string[]; rules: { head, body: [a] | [B, C] }[]; start: string; w: string
```

<img src="images/cgram--circuit-sat-solved.png" alt="Circuit-SAT" width="640">

---

## Tape and Stack Machines

*Planned (#662).* The Automaton picture plus a tape with a head (Turing machine, LBA) or one stack per live branch (PDA). There is no backend code yet.

<table><tr>
<td><img src="images/machine--turing-machine-acceptance-steps.png" alt="Turing machine, steps" width="420"></td>
<td><img src="images/machine--pushdown-automaton-acceptance-solved.png" alt="PDA" width="420"></td>
</tr><tr><td>Turing machine: the head and the cell just written</td><td>Pushdown automaton: one stack per branch</td></tr></table>

**Formats** follow Redux's DFA and NFA tuple style in Sipser's order: `((Q, Σ, Γ, δ, q0, q_accept, q_reject), w)` for Turing machines and LBAs, `((Q, Σ, Γ, δ, q0, Z0, F), w)` for PDAs. Labels use standard notation: `a → b, R` on a Turing machine and `a, X → YX` on a PDA.

**How it reads.** The reject state has a dotted ring; a missing rule goes there by convention. The current state, the rule just used, and the head's cell or stack top are `ElementHighlight`. An LBA's input is wrapped in `<` and `>`, which the head can't cross. A PDA shows up to 6 branches side by side and 8 cells per stack, then "+ more".

**Honest endings.** A Turing machine that never halts can only be given up on. The run stops at a step limit and says "no answer yet", never "rejected".

<img src="images/machine--linear-bounded-automaton-acceptance-solved.png" alt="LBA" width="640">

---

## Dominoes and Two-Graph Mapping

*Planned (#662).* Pictures for Post Correspondence and for the isomorphism problems. There is no backend code yet.

<table><tr>
<td><img src="images/dmap--post-correspondence-problem-steps.png" alt="PCP, steps" width="420"></td>
<td><img src="images/dmap--graph-isomorphism-solved.png" alt="Graph Isomorphism" width="420"></td>
</tr><tr><td>Dominoes: Post Correspondence, mid-search</td><td>Two-Graph Mapping: Graph Isomorphism</td></tr></table>

**Dominoes (Post Correspondence).** The top and bottom strings are aligned letter by letter, with colored bars showing which tile wrote which letters. Letters where they agree are `Covered`; the leftover one row is ahead by is `ElementHighlight`; dashed slots are owed by the other row; the first disagreement is `Rejected`. Because PCP is undecidable, a search can end "no match within the limit" rather than "no match".

**Two-Graph Mapping (Graph and Subgraph Isomorphism).** The two graphs side by side. A node and its partner share a color and a tag naming the partner. The node being placed and the partner being tried are `ElementHighlight`; candidates ruled out by degree are `Blocked`; an edge whose partner edge exists is `Solution`, one without is `Rejected` with a missing-edge mark where it would have to be. Subgraph Isomorphism draws the host's extra edges dotted.

```
// PCP:          tiles: { top, bottom }[]; certificate: int[] (tile numbers, repeats allowed)
// Isomorphism:  first, second: { nodes, edges }; rule: "isomorphism" | "subgraph"
//               certificate: { from, to }[]
```

<details><summary>More</summary>

<table><tr>
<td><img src="images/dmap--post-correspondence-problem-solved.png" alt="PCP, solved" width="420"><br>Post Correspondence, Solved</td>
<td><img src="images/dmap--subgraph-isomorphism-solved.png" alt="Subgraph Isomorphism" width="420"><br>Subgraph Isomorphism</td>
</tr></table>
</details>

---

## Also planned

- **Certificate pictures (#853):** draw a certificate the user typed on the problem's picture, and show where an invalid one fails. No endpoint exists yet.
- **Removing the list format (#732)** once both frontends read frames.

## Updating this page

The screenshots are taken from `mockups/design.html` at 1,280 px in the light theme. If a mockup changes, rebuild it (`node build.js` in `mockups/`), run the contrast audits in `mockups/contrast/` (both must report no failures), and retake the affected screenshots. When a picture type is implemented in the frontends, replace its screenshots with ones from the real site, and update the payloads here to match the code. The payloads above follow the frames builders in `Interfaces/`; when they change, the endpoint tests in `redux-tests/Endpoints/` change with them, so update this page in the same PR.
