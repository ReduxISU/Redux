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
| `Graph` | not a list item: a `payload` plus `frames` of named states ([`GraphSubsetFrames`](../../Interfaces/graphs/GraphSubsetFrames.cs)) | none yet | Clique, Vertex Cover, Minimum Vertex Cover, Independent Set, Dominating Set. Reported only as the `type` of `visualize?format=frames`; these visualizations still report `GraphD3` in /info and in the default list format |
| `Board` | not a list item: a `payload` plus `frames` of named states ([`BoardFrames`](../../Interfaces/Board/BoardFrames.cs)) | none yet | Sudoku, N-Queens. Reported only as the `type` of `visualize?format=frames`; Sudoku's visualization still reports `Unimplemented` and N-Queens' reports `DynamicTable` in /info and in the default list format. See "The Board picture" below |
| `SearchTree` | not a list item: a `payload` plus `frames` of named states ([`SearchTreeFrames`](../../Interfaces/SearchTree/SearchTreeFrames.cs)) | none yet | A second view of a solver that branches and backs up (see "The Search Tree picture" below). Reported only as the `type` of `visualize?format=frames`; each search-tree visualization reports its problem's legacy type in /info and serves that picture's list format |
| `SetD3` | [`API_SET`](../../Interfaces/JSON_Objects/API_SET.cs) | [StandardSetSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardSetSvgReact.js) | Set Cover, Exact Cover, Partition |
| `BooleanSatisfiability` | [`API_SAT`](../../Interfaces/JSON_Objects/API_SAT.cs) | [StandardSATSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardSATSvgReact.js) | SAT, 3SAT |
| `QuantumCircuitD3` | [`API_QUANTUMCIRCUIT`](../../Interfaces/JSON_Objects/API_QUANTUMCIRCUIT.cs) | [StandardCircuitSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/StandardCircuitSvgReact.js) | Deutsch (D3 view) |
| `QuantumCircuitQjs` | `API_QUANTUMCIRCUIT` | [QuantumCircuitVis.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/QuantumCircuitVis.js) | Deutsch, Shor's |
| `PumpSchedule` | a pump-frame class defined next to the visualization | [PumpSchedulingSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/PumpSchedulingSvgReact.js) | Pump Scheduling |
| `DynamicTable` | [`API_TableJSON`](../../Interfaces/JSON_Objects/Tables/API_TableJSON.cs) | [DynamicTableSvgReact.js](https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/components/Visualization/svgs/DynamicTableSvgReact.js) | DFA table view, shortest paths |
| `Unimplemented` | `API_empty` | nothing, on purpose | Convex Hull, Sudoku (see below) |

Two things that trip people up:

- Every payload class carries a `kind` field (`"graph"`, `"sat"`, `"set"`, `"table"`, `"empty"`, ...). It tells a reader what the JSON is without guessing. You do not set it. The class does.
- `Unimplemented` means "no renderer exists". The GUI shows an honest "not renderable" message instead of a broken picture. A few old classes use it, and a test (section 6) stops **new** classes from joining them.

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
| `Solution` | What the solver has put down: in every step of an incremental solver (what it has committed), in an `Accept`, and in a correct `Done`. Never a clue |
| `Rejected` | The focus of an incremental solver's `Reject` (a square with no digit left, a row with no safe square and the queen above it that must move); a square a step ruled out (the start of a link); everything placed in a `Done` whose answer is wrong |
| `Covered`, `Blocked`, `Untraveled` | not used. Squares a queen attacks are not marked: a frontend's Hints switch can shade them from the queens' positions |

A run stopped by the time limit ends with a `Done` frame, `ok: false`, showing only the clues: no pieces, no links, every square `Background`. Its answer string is never read. Long searches keep their first 150 steps and then the `Done` frame with the finished board; its caption says how many steps were not shown.

The solvers and their steps (`BoardCells`: `values`, every square that holds something, clues included, as a digit or `"Q"`; `conflicts`, each `{ cell, with }` a square ruled out and the square that rules it out):

| Solver | Incremental | Steps |
| --- | --- | --- |
| Sudoku Backtracking | yes | A `Try` per digit put in a square ("R1C1 can still hold 6, 8 or 9. Try 6.", "6 in R1C1 led to a dead end. Try 8 instead."), a `Reject` when a square has no digit left ("R2C6 has no digit left: every digit from 1 to 9 is already in its row, column or box. Undo the last choice."), `Done`. Clues that clash get one `Reject` naming them, linked, and nothing is searched |
| N-Queens Backtracking | yes | A `Try` per queen placed, with the squares skipped in its row as conflicts ("Row 1: columns 0 and 1 are attacked (0 along its column by the queen at (0,0)). Place the queen at column 2."), a `Reject` naming the whole row and the queen above it when no square is safe ("Row 2: every column is attacked, so back up and move the queen in row 1."), `Done` |
| N-Queens Constructive | yes | A `Try` with the column list the formula writes, then a `Try` per row ("Row 0: column 1, straight from the list. No checking needed."), `Done`. Rows are `Try`, not `Accept`: the recorder builds every `Accept` past its cap, which for a large n would build the board n times |

`SudokuVisualization` got the picture; its list format is unchanged (still an empty placeholder). N-Queens has a new `NQueensVisualization`, whose list format is a table of each row's queen (`DynamicTable`). N-Queens' default visualization is still `DummyVisualization`, so its /info is unchanged.

To add a grid puzzle or another board solver:

1. Implement `IBoardVisualization<T>` on its visualization, **explicitly**, and declare `Type? IVisualization.StepShape => typeof(BoardCells)`.
2. Return the spec from `Describe`, and read an answer string as a board in `ParseAnswer`. A static `Spec(problem)` next to it lets tests build the same description.
3. Record a `BoardCells` per decision with the whole board, clues included, and the squares the step ruled out as conflicts. Build it only inside the recorder's lambdas, and pass the focus as a lambda too, so solving without steps costs nothing. Declare `IIncrementalSolver` when the solver fills squares one at a time.
4. Prove nothing changed for old clients: snapshot the list-format output, `/info` and what the solvers answer before touching code (`Endpoints/Snapshots/board_*.tsv`, `BoardFrames_Endpoint_Tests`, `BoardSolvers_Tests`, `BoardFrames_Tests`).

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
| N-Queens | N-Queens Backtracking | `NQueensSearchTreeVisualization` | Depth first. Under `start`, each square considered in a row is a node `(row,col)` under the queen above: skipped squares are pruned (`hit by (0,0)`), the queen placed is explored (the last row's is the solution), and a dead row takes the queen above back (failed; the root when row 0 runs out). The `Done` step names the answer's path |

Not covered, because nothing in Redux branches for them: Knapsack (`KnapsackBruteForce` enumerates every set of items in a flat loop and `KnapsackDP` fills a table) and Graph Coloring (`GraphColoringBruteForce` counts through every coloring and `GraphColoringGreedy` never backs up). SAT has only its brute force and Grover's solver; 3SAT's PPZ, Schöning and WalkSAT are randomized local searches. TSP's brute force and greedy solvers don't branch either.

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
| [Metadata/VisualizationType_Tests.cs](../../redux-tests/Metadata/VisualizationType_Tests.cs): `NoNewUndeclared` | Your class does **not** declare `VisualizationType.Unimplemented`. Only four old classes may (`ConvexHullVisualization`, `SudokuVisualization`, `LosslessDataCompressionVisualization`, `DummyVisualization`). A new one that is still `Unimplemented` fails here, with your class name in the message. |
| same file: `AllowlistHasNoStaleEntries` | That list of four contains no class that has since been given a real type. If you finally implement one of the four, delete its line from `UnimplementedAllowlist`. This is the only time you should edit that list. |
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
