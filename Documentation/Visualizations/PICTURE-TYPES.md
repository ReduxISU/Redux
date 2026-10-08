# Picture types

What each Redux picture type looks like, which problems use it, what a problem hands over, and how it animates. The screenshots come from the design mockup ([`mockups/design.html`](mockups/design.html)); open it in a browser to click through every example, solver and step.

- To add a visualization or record solver steps, read [ADDING-A-VISUALIZATION.md](ADDING-A-VISUALIZATION.md).
- To implement a picture type in the backend or a frontend, read [README.md](README.md).

All the types below are **Approved** as designs. Each type's open review questions are listed at the bottom of its tab in the mockup; resolve them before implementing that type.

## How to read this page

Every picture type has three views, picked with the **Instance / Solved / Steps** switch:

| View | Shows | Comes from |
| --- | --- | --- |
| Instance | The problem as given, nothing highlighted | The problem |
| Solved | The answer, plus checks that show why it's right | The problem and the solver's certificate |
| Steps | The solver's run, one decision per frame, with a caption | The selected solver's recorded steps |

The **side panel** holds the stepper (◀, Play, ▶, a slider and the caption), the answer, the checks, and the key. The key only lists the states that type uses, described in that type's own words.

**On phones and tablets** (up to 1,199 px wide) the side panel moves under the diagram, stepper first, and the key folds closed. On phones (up to 700 px) the diagram keeps a minimum width of 560 px and scrolls sideways on its own, with a "Swipe the diagram sideways" hint; the Reduction view needs about 960 px. "Hover" becomes "Tap" on touch screens.

**States** are the shared names from [ADDING-A-VISUALIZATION.md](ADDING-A-VISUALIZATION.md#the-shared-states): `Background`, `ElementHighlight` (amber ring), `Solution` (green), `Rejected` (red), `Untraveled` (faded), `Covered` (light green), `Blocked` (dashed grey outline). Annotations such as order badges, ×2 badges, missing-edge lines and group colors are not states.

### Index

| Picture type | Problems | Graph subtype |
| --- | --- | --- |
| [Graph](#graph) | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set, Graph Coloring, Clique Cover, Max Cut, Cut, Weighted Cut, Minimum Cut, Minimum Spanning Tree, Steiner Tree, Hamiltonian Cycle, Traveling Salesperson | Force layout (subset, edges, partition) or circle layout (tour) |
| [Grouped Graph](#grouped-graph-clause-graph) | SAT, 3SAT | Ring of groups |
| [Bipartite Graph](#bipartite-graph) | Set Cover, Exact Cover, Exact Hitting Set; SAT and 3SAT as the Factor Graph | Two columns |
| [Automaton](#automaton) | DFA, NFA | Layered from the start state |
| [Layered Graph](#layered-graph) | Topological Sort, Feedback Arc Set, Feedback Node Set, Directed Hamiltonian Cycle, Strongly Connected Components, Single-Source and Single-Pair Shortest Path | Ranked left to right |
| [Flow Network](#flow-network) | Minimum S-T Cut | Source to sink |
| [Packing](#packing) | Partition, Subset Sum, Knapsack, Bin Packing | |
| [Schedule](#schedule) | Job Sequencing, both Pump Scheduling problems | |
| [Quantum Circuit](#quantum-circuit) | Bernstein-Vazirani, Deutsch, Deutsch-Jozsa, Simon's Problem, Unstructured Search, Prime Factorization | |
| [Board](#board) | Sudoku, N-Queens | |
| [Table and Trace Table](#table-and-trace-table) | Edit Distance, 0-1 Integer Programming; trace tables for DFA, NFA, SSSP, SPSP | |
| [Geometry, Tripartite Matching, Code Tree](#geometry-tripartite-matching-and-code-tree) | Convex Hull, 3-Dimensional Matching, Lossless Data Compression | |
| [Search Tree and Game Tree](#search-tree-and-game-tree) | Second view for 3SAT, Knapsack, TSP, Graph Coloring, N-Queens; QBF, Generalized Geography | Tidy tree |
| [Reduction](#reduction) | 3SAT → Clique, Clique → Vertex Cover, 3SAT → Graph Coloring | Two panes |
| [Boolean Circuit and Grammar](#boolean-circuit-and-grammar) | Circuit Value, Circuit-SAT, CFG Membership (not in Redux yet) | |
| [Tape and Stack Machines](#tape-and-stack-machines) | Turing machine, LBA, PDA (not in Redux yet) | Automaton plus tape or stacks |
| [Dominoes and Two-Graph Mapping](#dominoes-and-two-graph-mapping) | Post Correspondence, Graph Isomorphism, Subgraph Isomorphism (not in Redux yet) | |

Types marked "not in Redux yet" are defined so the problems can be added later (backend PR 7, #662). Their problems are not part of that work.

---

## Graph

One renderer for every plain undirected graph problem. A problem hands over its graph and says what kind of answer it has. The base works out every color from that answer, so no problem writes its own highlight loop.

<table><tr>
<td><img src="images/graph--clique-solved.png" alt="Clique, solved" width="420"></td>
<td><img src="images/graph--clique-steps.png" alt="Clique, mid-run" width="420"></td>
</tr><tr><td>Clique, Solved</td><td>Clique, Steps</td></tr></table>

**Problem hands over**

```
nodes:   string[]
edges:   { a, b, weight? }[]
k?:      int
shape:   "subset" | "edges" | "partition" | "tour"
rule:    "clique" | "vertexCover" | "minVertexCover" | "independentSet" | "dominatingSet"
       | "coloring" | "cliqueCover" | "maxCut" | "cut" | "weightedCut" | "minCut"
       | "spanningTree" | "steinerTree" | "hamiltonianCycle" | "tsp"
```

**Subtypes by answer shape**

| Shape | Problems | Layout | How the answer reads |
| --- | --- | --- | --- |
| Subset of nodes | Clique, Vertex Cover, Min Vertex Cover, Independent Set, Dominating Set | Force | Chosen nodes `Solution`. Edges the answer handles `Covered`; edges breaking the rule `Rejected`. Clique draws a red dashed **missing edge** between chosen nodes that aren't joined. Independent Set marks neighbors of chosen nodes `Blocked`. |
| Set of edges | Minimum Spanning Tree, Steiner Tree, Cut, Weighted Cut | Force | Chosen edges `Solution`. Steiner terminals get a double outline. |
| Split into groups | Graph Coloring, Clique Cover, Max Cut, Minimum Cut | Force | Nodes filled with group colors (six colorblind-safe colors). Edges inside a group that the rule forbids are `Rejected`. |
| Tour | Hamiltonian Cycle, Traveling Salesperson | Circle | Tour edges `Solution`, with order badges on the nodes. A missing tour edge is a red dashed line. |

**Steps.** Each frame is the selected solver's certificate so far plus what it's trying. The base re-derives every state from that, so a Greedy and a Brute Force solver animate on the same picture with no extra code.

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
</details>

---

## Grouped Graph (clause graph)

One node per literal, grouped into one cluster per clause, with the clusters on a ring. Edges join a literal to its complement in another clause, so a satisfying assignment shows up as one green literal in every cluster. Hovering a literal traces that variable everywhere it appears.

<table><tr>
<td><img src="images/clause--clause-graph-solved.png" alt="Clause graph, solved" width="420"></td>
<td><img src="images/clause--clause-graph-steps.png" alt="Clause graph, backtracking" width="420"></td>
</tr><tr><td>Solved: one true literal per clause</td><td>Steps: C2 has no true literal left, so the solver backs out</td></tr></table>

**Problem hands over**

```
clauses:     int[][]     // [[-1, 2, 3], [-1, -2, 4], ...]  sign = polarity
variables?:  string[]    // display names, default x1..xn
```

The base derives the clusters, complement edges and colors, so SAT and 3SAT get identical pictures from the same code.

**States.** Unassigned literals `Background`; the variable being tried `ElementHighlight`; true literals `Solution` (their cluster is `Covered`); false literals muted (`False`, under review); a clause with every literal false `Rejected`.

**Edges.** An Edges switch shows complement pairs (default) or adds the within-clause edges, which makes this exactly the 3SAT → Independent Set graph.

**Steps.** Each frame is the assignment so far, the variable being tried, and any clause that just failed.

**Ties to reductions.** The groups match the clusters the 3SAT → Clique reduction builds, so the reduction's target can use the same layout.

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
</tr><tr><td>Exact Cover (picks sets, each element exactly once)</td><td>Exact Hitting Set (picks elements, each set hit exactly once)</td></tr></table>

**Problem hands over**

```
universe:  string[]
sets:      string[][]
k?:        int                        // Set Cover only
pick:      "sets" | "elements"        // side the certificate names
rule:      "atLeastOnce" | "exactlyOnce"
```

**States.** Chosen nodes `Solution`; nodes hit the required number of times `Covered`; the choice being tried `ElementHighlight`; a choice that would double up on something already covered `Blocked`; uncovered, doubly covered or out of options `Rejected`. A ×2 badge counts double coverage.

**Layout.** The set column is sorted by the average position of its elements to cut crossings. Names stay in instance order (S1, S2, …) so they match the certificate.

**Factor Graph for SAT.** SAT and 3SAT can pick this type as a second visualization: variables on the left, clauses on the right, solid edges for x and dashed edges for ¬x. Edges an assignment makes true turn green.

<img src="images/clause--factor-graph-solved.png" alt="SAT factor graph" width="640">

**Wire change.** The set problems send a nested set list today. Moving to this node-link payload needs a matching frontend change.

---

## Automaton

States laid out left to right in the order the machine reaches them. The start state gets an incoming arrow and accept states get a double ring. Transitions between the same two states share one arrow with a combined label. The input tape underneath shows what has been read.

<table><tr>
<td><img src="images/auto--dfa-ends-in-ab-input-abaab.png" alt="DFA mid-run" width="420"></td>
<td><img src="images/auto--nfa-contains-ab-with.png" alt="NFA mid-run, all branches" width="420"></td>
</tr><tr><td>DFA, Steps</td><td>NFA, Steps (all branches at once)</td></tr></table>

**Problem hands over**

```
states:       string[]
alphabet:     string[]
transitions:  { from, symbol, to }[]   // "ε" allowed for NFA
start:        string
accept:       string[]
input:        string
```

**Steps.** One frame per symbol read: the active states, the transitions just taken, and how far along the tape the run is. The run ends `Solution` (accepted) or `Rejected`. States the run never reached are `Untraveled`.

**Decided behavior**

- **DFA missing transitions** go to a separate, isolated **Garbage** state drawn in red. A run that falls in stays there and is rejected.
- **NFA** shows all branches at once by default. A **One path** switch follows a single run, chosen from a dropdown that lists each path with its outcome.
- **ε-transitions** (NFA) are dashed.
- **Edge labels stay strings**, never fed to a number scale.

A **Trace Table** view (one row per symbol) is available as a second visualization; see [Table](#table-and-trace-table).

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

**Problem hands over**

```
nodes:    string[]
edges:    { from, to, weight? }[]
k?:       int
source?:  string,  target?: string
shape:    "order"        // Topological Sort, Directed Hamiltonian Cycle
        | "removedSet"   // Feedback Arc Set (edges), Feedback Node Set (nodes)
        | "groups"       // Strongly Connected Components
        | "distances"    // SSSP (a tree), SPSP (one path)
```

**Subtypes by answer shape**

| Shape | How it reads |
| --- | --- |
| Order | Order badges on nodes; nodes ready to place `Covered`, placed `Solution` |
| Removed set | Removed edges dashed green (they're the answer); a cycle that survives `Rejected` |
| Groups | Components filled with group colors. Kosaraju's first pass shows finish numbers above the nodes |
| Distances | Each node's distance under it, ∞ until reached. Edges just relaxed `ElementHighlight`; settled nodes `Solution`; unreachable nodes `Untraveled` |

**Layout.** Break cycles by depth-first search, rank by longest path, order each rank by the average position of its neighbors, and draw back edges as curves under the rows.

<details><summary>Every Layered Graph problem, solved</summary>

<table>
<tr><td><img src="images/layered--feedback-arc-set-solved.png" alt="Feedback Arc Set" width="420"><br>Feedback Arc Set</td><td><img src="images/layered--feedback-node-set-solved.png" alt="Feedback Node Set" width="420"><br>Feedback Node Set</td></tr>
<tr><td><img src="images/layered--directed-hamiltonian-cycle-solved.png" alt="Directed Hamiltonian Cycle" width="420"><br>Directed Hamiltonian Cycle</td><td><img src="images/layered--strongly-connected-components-solved.png" alt="Strongly Connected Components" width="420"><br>Strongly Connected Components</td></tr>
<tr><td><img src="images/layered--single-source-shortest-path-solved.png" alt="Single-Source Shortest Path" width="420"><br>Single-Source Shortest Path</td><td><img src="images/layered--single-pair-shortest-path-solved.png" alt="Single-Pair Shortest Path" width="420"><br>Single-Pair Shortest Path</td></tr>
</table>
</details>

---

## Flow Network

The source sits at the far left and the sink at the far right; every other node is placed by its hop distance from the source. Each edge reads flow/capacity and gets thicker as it fills.

<table><tr>
<td><img src="images/flow--flow-solved.png" alt="Flow network, solved" width="420"></td>
<td><img src="images/flow--flow-steps.png" alt="Flow network, augmenting path" width="420"></td>
</tr><tr><td>Solved: the S side, the T side and the cut edges</td><td>Steps: pushing flow along one augmenting path</td></tr></table>

**Problem hands over**

```
nodes:   string[]
edges:   { from, to, capacity }[]
source:  string
sink:    string
```

**Steps.** One frame per augmenting path: the flow on every edge and the path used, with backward arcs drawn when a path uses one (a switch shows all of them). Edmonds-Karp and Ford-Fulkerson both fit this frame shape.

**Ending.** When no path is left, the nodes the source can still reach form side S. The full edges from S to T are `Solution` and add up to the maximum flow.

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

**Problem hands over**

```
items:       { size, value? }[]          // value only for Knapsack
containers:  { label, target? | limit? | atLeast? }[]
rule:        "partition" | "subsetSum" | "knapsack" | "binPacking"
```

The answer says which container each item is in (−1 = left out), so one drawing serves every problem.

**Lines.** The half-way mark for Partition, a target for Subset Sum, a weight limit and a value goal for Knapsack, a capacity per bin. Partition and Bin Packing use group colors for sides and bins.

<details><summary>Subset Sum</summary>

<img src="images/packing--subset-sum-solved.png" alt="Subset Sum" width="640">

The mockup also shows Subset Product on a log scale. That problem lives only on a demo branch, not in Redux.
</details>

---

## Schedule

Time runs left to right, drawn to scale.

<table><tr>
<td><img src="images/schedule--job-sequencing-solved.png" alt="Job Sequencing" width="420"></td>
<td><img src="images/schedule--pump-scheduling-cost-minimization-solved.png" alt="Pump Scheduling" width="420"></td>
</tr><tr><td>Job Sequencing: one row per job, deadline ticks</td><td>Pump Scheduling: 24 hourly rows, tank level and cost below</td></tr></table>

**Job Sequencing** gives each job a row. Its bar is when it runs on the one machine, the black tick is its deadline, and a run that ends past the tick turns `Rejected` and costs that job's penalty.

**Pump Scheduling** gives each pump a row of 24 hours, with the tank level and the cost of each hour charted underneath on the same axis. Shaded hours are the peak tariff. A startup mark shows when a pump turns on and pays its startup cost.

**Problems hand over**

```
// Job Sequencing
times, deadlines, penalties: int[];  k: int
// Pump Scheduling (both)
tank: { capacity, level, minimum? };  demand: number[24];  peakHours: int[]
rates: { onPeak, offPeak };  pumps: { name, flowGph, kw, startupCost }[];  budget?: number
```

**Steps.** Job Sequencing records the orders tried. Pump Scheduling records three phases: the dynamic program's forward pass (the band of tank levels still reachable, hour by hour), the trace back, and the chosen schedule replayed hour by hour. The C# solver builds the forward-pass table today and throws it away; it only has to return 24 summaries.

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

**Problem hands over**

```
qubits:   { name, role }[]          // x0, x1, y (helper), c0 (counting) …
columns:  { gates: Gate[] }[]       // in the order they run
  Gate = { type: "h" | "x" | "z" | "cx" | "measure" | "box",
           qubits: int[], control?: int, label?, oracle?: bool }
stages:   { label, from, to }[]     // "prepare", "query once", "read"
measure:  int[]                     // the qubits the answer is read from
```

This replaces today's two formats (a D3 gate list or raw OpenQASM behind a flag) with one gate list. QASM becomes an export. Probabilities come from a statevector simulation, which the mockup runs in the browser for up to 15 qubits.

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
</tr><tr><td>Sudoku, Steps: the square being tried and its row, column and box</td><td>N-Queens, Solved</td></tr></table>

**Sudoku** shows clues in bold and the solver's digits in green, with the square being tried highlighted. A clash links two squares in the same row, column or box that hold the same digit.

**N-Queens** shows each queen as it lands. Squares it had to skip get a red cross and a dashed line back to the queen attacking them. A Hints switch shades every attacked square.

**Payload**

```
{ "type": "Board", "size": 9, "box": 3,
  "cells":  [{ "r": 0, "c": 3, "value": 1, "given": true, "state": "Background" }],
  "pieces": [{ "r": 2, "c": 0, "kind": "queen", "state": "ElementHighlight" }],
  "links":  [{ "from": [0, 0], "to": [0, 5], "state": "Rejected" }] }
```

Squares, pieces and links are all the board needs, so later grid puzzles (Latin squares, Kakuro) fit the same shape. Long searches show their first steps and say how many were skipped.

---

## Table and Trace Table

Rows and columns instead of dots and lines. Three shapes share one renderer, drawn in HTML rather than SVG.

<table><tr>
<td><img src="images/table--edit-distance-steps.png" alt="Edit Distance, steps" width="420"></td>
<td><img src="images/table--0-1-integer-programming-solved.png" alt="0-1 Integer Programming" width="420"></td>
</tr><tr><td>Grid: Edit Distance, each cell and the neighbors it reads</td><td>Matrix: 0-1 Integer Programming, constraints against variables</td></tr></table>

| Shape | Problems | How it reads |
| --- | --- | --- |
| Grid (dynamic programming) | Edit Distance | The cell being computed is `ElementHighlight`, with arrows from the neighbors it came from. The last frame draws the cheapest edit path as `Solution`. |
| Matrix | 0-1 Integer Programming | One row per constraint, with its total against the bound. Rows that hold are `Solution`, rows over the bound `Rejected`. |
| Trace table | DFA, NFA, SSSP, SPSP (second view next to their graphs) | One row per step, added as the run reaches it. Cells that changed this step are underlined. |

This extends the existing `API_TableJSON` (title, columns, rows): rows appear as reached, cells can be marked changed, states use the shared names instead of colors, and grid mode carries the arrows.

<details><summary>Trace tables</summary>

<table>
<tr><td><img src="images/table--dfa-acceptance-solved.png" alt="DFA trace table" width="420"><br>DFA</td><td><img src="images/table--nfa-acceptance-solved.png" alt="NFA trace table" width="420"><br>NFA</td></tr>
<tr><td><img src="images/table--single-source-shortest-path-problem-solved.png" alt="SSSP trace table" width="420"><br>Single-Source Shortest Path</td><td><img src="images/table--single-pair-shortest-path-problem-solved.png" alt="SPSP trace table" width="420"><br>Single-Pair Shortest Path</td></tr>
</table>
</details>

---

## Geometry, Tripartite Matching and Code Tree

Three problems that aren't graphs, each with its own picture type, following the same rules.

<table><tr>
<td><img src="images/misc--convex-hull-steps.png" alt="Convex Hull, merge step" width="420"></td>
<td><img src="images/misc--3-dimensional-matching-solved.png" alt="3-Dimensional Matching" width="420"></td>
</tr><tr><td>Geometry: Convex Hull, merging two hulls</td><td>Tripartite Matching: 3-Dimensional Matching</td></tr></table>

**Geometry (Convex Hull).** Points drawn to scale on real axes. Steps follow Redux's divide-and-conquer solver: the left hull blue, the right hull orange, the bridge being tried `ElementHighlight`, dropped points `Rejected`. Finished corners are numbered in output order; points inside are `Covered`.

**Tripartite Matching (3-Dimensional Matching).** Three columns X, Y and Z; each triple is a line through its three names. Chosen triples `Solution`, names used once `Covered`, triples that share a name with a chosen one `Blocked`, names used twice ×2 and `Rejected`.

**Code Tree (Lossless Data Compression).** A Huffman tree that grows bottom-up. Each step merges the two lightest trees (`ElementHighlight`) under a new parent. Left edges are 0, right edges are 1; finished characters show their code under them, and the encoded bits run underneath.

<img src="images/misc--lossless-data-compression-solved.png" alt="Huffman code tree" width="640">

```
// Convex Hull        points: { x, y }[]            answer: corner indices, in order
// 3D Matching        X, Y, Z: string[]; M: { x, y, z }[]    answer: triple indices
// Data Compression   text: string                  answer: { char → code }, bits
```

---

## Search Tree and Game Tree

The solver's choices drawn as a tree that grows step by step. It's a second visualization for any backtracking or branch-and-bound solver, and the main picture for game problems.

<table><tr>
<td><img src="images/search--3sat-solved.png" alt="3SAT search tree" width="420"></td>
<td><img src="images/search--quantified-boolean-formula-game-tree-solved.png" alt="QBF game tree" width="420"></td>
</tr><tr><td>Search Tree: 3SAT backtracking</td><td>Game Tree: Quantified Boolean Formula</td></tr></table>

**Nothing new for problems to hand over.** A solver already records one step per decision. For the tree, each step also names the step it came from:

```
{ nodeId: int, parentId: int | null, label: "x2=F" | "take (20,100)" | …,
  state: "queued" | "explored" | "pruned" | "failed" | "solution" | "skipped",
  bound?: "≤ 240", value?: true | false | 1 | 2, caption }
```

A step without a `parentId` still works in every other picture, so solvers can add the field one at a time.

**How it reads.** The node being worked on `ElementHighlight`; explored choices light green; queued nodes dashed (best-first solvers only). Branches the solver cut without exploring are **red dashed**; branches it explored and backed out of are **red solid**. The answer's path turns green. Best-first solvers get numbered badges for the order nodes left the queue.

**Folding.** Past about 12 leaves, cut siblings share one "k cut" node and finished branches fold into "+k more".

**Game Trees** add a band per turn and a badge for who wins from each position; branches never needed once a turn is decided are `Untraveled`.

<details><summary>Other search trees</summary>

<table>
<tr><td><img src="images/search--knapsack-binary-solved.png" alt="Knapsack branch and bound" width="420"><br>Knapsack, branch and bound</td><td><img src="images/search--traveling-salesperson-solved.png" alt="TSP" width="420"><br>Traveling Salesperson</td></tr>
<tr><td><img src="images/search--graph-coloring-solved.png" alt="Graph Coloring" width="420"><br>Graph Coloring</td><td><img src="images/search--n-queens-solved.png" alt="N-Queens" width="420"><br>N-Queens</td></tr>
<tr><td><img src="images/search--generalized-geography-game-tree-solved.png" alt="Generalized Geography" width="420"><br>Generalized Geography</td><td><img src="images/search--3sat-steps.png" alt="3SAT mid-run" width="420"><br>3SAT, Steps (best-first order badges)</td></tr>
</table>
</details>

---

## Reduction

The instance you gave on the left, the instance the reduction builds on the right. Both panes reuse the picture types above; a reduction never draws anything itself.

<img src="images/reduction--3sat-clique-build.png" alt="3SAT to Clique, build phase" width="760">

*Build: each clause becomes a cluster of three nodes.*

<img src="images/reduction--3sat-clique-map.png" alt="3SAT to Clique, map back" width="760">

*Map back: the clique on the right sets the assignment on the left, piece by piece.*

**Three phases**

1. **Build.** One step per gadget, in both problems' own words. The pieces each step adds and the source they came from are `ElementHighlight`. Pairs a rule deliberately leaves unjoined are drawn as "rule says no".
2. **Solve.** The target is solved by its default solver.
3. **Map back.** The target's answer is mapped to the source's answer, one piece at a time.

**Reduction hands over**

```
target:   string                     // B's instance, in B's own format
gadgets:  { kind, from: string[], to: string[], note? }[]
          // kind ∈ element | group | edgeRule | palette | orGadget | bound
build:    { addNodes, addEdges, skipped?, focus: { from, to }, caption }[]
mapBack:  { fromB: string[], setsA: { id, value }[], caption }[]
```

**Hover and color.** Hovering any piece on either side highlights what it became. "Color gadgets" gives each gadget and its source a shared color.

<details><summary>The other two reductions</summary>

<table>
<tr><td><img src="images/reduction--clique-vertex-cover-build.png" alt="Clique to Vertex Cover, build" width="420"><br>Clique → Vertex Cover, Build</td><td><img src="images/reduction--clique-vertex-cover-map.png" alt="Clique to Vertex Cover, map back" width="420"><br>Clique → Vertex Cover, Map back</td></tr>
<tr><td><img src="images/reduction--3sat-graph-coloring-build.png" alt="3SAT to Graph Coloring, build" width="420"><br>3SAT → Graph Coloring, Build</td><td><img src="images/reduction--3sat-graph-coloring-map.png" alt="3SAT to Graph Coloring, map back" width="420"><br>3SAT → Graph Coloring, Map back</td></tr>
</table>
</details>

---

## Boolean Circuit and Grammar

*Not in Redux yet.* Two picture types defined so Circuit Value, Circuit-SAT and Context-Free Grammar Membership can be added later.

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

*Not in Redux yet.* The Automaton picture plus a tape with a head (Turing machine, LBA) or one stack per live branch (PDA).

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

*Not in Redux yet.* Pictures for Post Correspondence and for the isomorphism problems.

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

## Updating this page

The screenshots are taken from `mockups/design.html` at 1,280 px in the light theme. If a mockup changes, rebuild it (`node build.js` in `mockups/`) and retake the affected screenshots. When a picture type is implemented in the frontends, replace its screenshots with ones from the real site, and update the payloads here to match the code.
