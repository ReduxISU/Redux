# Adding a visualization and its animation

This guide is for contributors who want a Redux problem to draw a picture, and for solvers and reductions that want to animate one. It explains the model, then walks through DFA end to end.

- To see what each picture type looks like and what it needs, read [PICTURE-TYPES.md](PICTURE-TYPES.md).
- To port the mockups into the frontends, or to see what the backend has and what is still planned, read [README.md](README.md).
- To write a visualization by hand that uses none of the picture types, read §8.

> **Status.** The names below are the real ones on the integration branch. They reach `CSharpAPI` in small PRs (tracker #655): typed steps are merged (#734); most picture types, the Reduction View and the time limit are still in review or waiting. If a name isn't on your base branch yet, check the tracker.

---

## 1. The idea in four sentences

1. A **problem** hands over its data: a graph, a list of clauses, a grid. It never draws anything.
2. A **visualization** picks a *picture type* (Graph, Automaton, Board, …) and describes the problem in that type's spec record. The picture type's shared frames builder makes every drawing decision, including every state.
3. A **solver** records **steps** while it runs: what it tried, what it accepted, what it threw out. Those steps play back as an animation on *any* picture of that problem that draws the same answer shape.
4. A **reduction** records how it builds the target instance, piece by piece, and how an answer to the target maps back to the source.

Nothing in a problem, solver or reduction mentions colors, coordinates or SVG. The frontends own the look; the backend owns the meaning.

```
 Problem ──data──▶ Visualization.Describe ──spec──▶ frames builder ──{ type, payload, frames }──▶ frontend
 Solver.Solve(problem, rec) ──SolverStep<TPartial>──────────┘
```

## 2. Words used in this guide

| Word | Meaning |
| --- | --- |
| **Picture type** | A family of drawings with one renderer per frontend: Graph, Grouped Graph, Bipartite Graph, Automaton, Layered Graph, Flow Network, Packing, Schedule, Board, Table, Quantum Circuit, Tripartite Matching, Geometry, Code Tree, Search Tree and Reduction; Formula and Nested Set are proposed (additional selectable views for SAT/3SAT and the set problems, next to Grouped Graph / Factor Graph / Bipartite Graph). See [PICTURE-TYPES.md](PICTURE-TYPES.md). It is the `type` of a frames response, and `pictureType` in `/info`. |
| **Shape** | A variant within a picture type that changes how the answer reads, such as subset, edges, cut, groups or tour in the Graph type. |
| **Spec** | The record a visualization returns from `Describe`: the problem's data in the picture type's terms, such as `GraphSubsetSpec`. |
| **Payload** | The JSON a picture type sends once per response. It has a `kind` field. |
| **Answer shape** | What kind of thing the answer is: `NodeSet`, `EdgeSet`, `Groups`, `Tour`, `Assignment`, `ChosenSets`, `ActiveStates`, and so on (`Interfaces/Steps/Shapes.cs`). Steps are typed by answer shape, as `SolverStep<TPartial>`. |
| **Rule** | What the answer must satisfy, such as `SubsetRule.Clique` or `BipartiteRule.ExactlyOnce`. The builder uses it to decide which parts are Covered or Rejected. |
| **Step** | One decision a solver made, with the answer so far, the ids it is looking at, and a one-sentence caption. |
| **Frame** | One picture in the animation. The builder turns each step into a frame, and adds a solved frame if the run did not end in Done. Each frame says which step it was drawn from (`step`, null on an added frame). The payload alone is the picture before Run. |
| **State** | The shared name for how an element looks in a frame (below). |

### The shared states

Every frame labels elements with these names (`PictureState`), never with colors. Each frontend maps them to its theme in one table. A frame lists only elements that are not `Background`.

| State | Use it for |
| --- | --- |
| `Background` | Nothing has happened to it yet |
| `ElementHighlight` | What the solver is looking at or trying right now, including a whole candidate a brute force is testing |
| `Solution` | Part of the confirmed answer only: the Done with `ok`, or an Accept that ends the run (the next step is that Done) |
| `Rejected` | Failed: a falsified clause, a rejecting state, an uncovered or doubled element, a wrong final answer |
| `Untraveled` | Never reached during this run, or not needed |
| `Covered` | A constraint that is met: an element covered, a set hit, a clause satisfied |
| `Blocked` | Can't be chosen without breaking a rule (exactly-once, independence) |
| `False` | An assigned-false literal or variable. An unassigned one stays `Background` |
| `Committed` | Kept so far but not confirmed: what an incremental solver has committed (queens placed, items packed, tree edges chosen), and a best so far a solver may still replace (#886). Drawn a lighter green with a dashed outline |

Some marks are **annotations, not states**: the ×2 badge for "covered twice", order numbers on a tour, the dashed "missing edge" (sent as a `phantoms` entry), group colors for color classes and cut sides, and the Garbage node in a DFA. The builder adds them; you never set them by hand.

## 3. What you write, and what you don't

| You write | The shared code does |
| --- | --- |
| Which picture type the problem uses (the interface you implement) | The payload, with ids and a deterministic layout hint |
| `Describe`: how the problem's fields fill the spec | Every state, from the steps, the rule and the verifier |
| `StepShape`: the answer shape your picture draws | Matching steps to the picture; a solver with another shape still gets the payload and the solved frame |
| In a solver: one `rec.…` call per decision, with a caption | Capping long runs, stopping at the time limit, the closing Done |
| Endpoint tests for the JSON | Responsive layout, dark mode, the key, hover and tap (frontends) |

If you find yourself writing `color = "green"` or looping over nodes to highlight them, stop: that belongs in the builder, and the visualization should only be describing the problem.

## 4. Adding a visualization to a problem

### 4.1 Pick the picture type

Find your problem in the index of [PICTURE-TYPES.md](PICTURE-TYPES.md). If it's listed, use that type. If it isn't:

- Does the answer pick nodes, edges, a split, groups or a tour on a graph? **Graph**.
- Directed, and the answer is an order, a removed set, components or shortest paths? **Layered Graph**.
- Two kinds of things and memberships between them? **Bipartite Graph** (and, once approved, **Nested Set** as a second view drawn from the same spec).
- A CNF formula? **Grouped Graph** (and, once approved, **Formula** as a second view drawn from the same spec). An equation, a system of linear constraints or another formula written out? The generalized **Formula** (`rows` / `equation` layouts, expression trees, operators by name; implement `IFormulaExprVisualization`, see [PICTURE-TYPES.md](PICTURE-TYPES.md#formula-generalized)). A view whose picture Redux_GUI cannot draw must be opt-in (`IOptInVisualization`; the Formula and Nested Set interfaces already are), so the GUI's listings leave it out unless a client passes `includePictures=true`.
- Numbers filling containers up to a line? **Packing**.
- A grid of cells? **Board**.
- A dynamic program, a constraint matrix or a trace? **Table**.

If none fits, open an issue under the tracker (#655) with a sketch before writing code. New picture types are mocked up and approved first ([README.md](README.md) §1). To draw it by hand without a picture type in the meantime, see §8: a handwritten visualization is tagged "Unclassified" and still drawn wherever a frontend can draw it.

### 4.2 Implement the picture's interface

Each picture type has one interface, `I<Type>Visualization<TProblem>`. Your visualization class keeps its legacy members (`visualize`, `visualizationType`, …) and adds two things:

- `Type? IVisualization.StepShape`: the answer shape your picture draws.
- `Describe`: your problem as the picture's spec.

Implement both **explicitly**, so nothing new appears in `/info`. The interface's default `BuildFrames` does the rest. This is Clique's, in full:

```csharp
class CliqueDefaultVisualization : IVisualization<CLIQUE, API_GraphJSON>, IGraphSubsetVisualization<CLIQUE> {
    // ... the legacy members, unchanged ...

    // --- Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    GraphSubsetSpec IGraphSubsetVisualization<CLIQUE>.Describe(CLIQUE clique) =>
        new(clique.nodes, clique.edges, clique.K, SubsetRule.Clique);
}
```

That is the whole change. The Graph builder knows that, for `Clique`, a pair of chosen nodes without an edge gets a missing-edge phantom, edges between chosen nodes are drawn with the chosen nodes, and a solver's Try of a whole candidate is `ElementHighlight`.

Some interfaces have more members:

- `ParseAnswer(problem, answer)` reads the solver's answer string into the answer shape, for the solved frame. Most have a default; override it when your problem's certificate looks different (Max Cut does).
- `ITableVisualization<TProblem, TPartial>` asks for `Draw` (one `TableDraft` per step), `Blank` and optionally `Start` and `Solved`, because a table's cells are problem-specific. `Blank` is the instance with nothing worked out: the payload sends it as `blank`, the picture before Run, and a stopped run closes on it. `Start` is the same blank as the run's steps write it, for a trace table whose first row depends on the run (DFA and NFA start in the start state, SSSP and SPSP Dijkstra at the source); null means use `Blank`.
- `ISearchTreeVisualization<TProblem, TPartial>` only needs `Describe`; the tree comes from the solver's `SolverStep.Tree` patches.

Don't override `BuildFrames`, `ListReadsTypedSteps`, `PictureType` or `DrawsAnswerAlone` unless you know why. `ListReadsTypedSteps` returns true only for a visualization whose list format already drew typed steps (DFA). `DrawsAnswerAlone` says whether the picture means anything when it can't draw the solver's steps; it is true except for the Search Tree, which `visualizeRun` then lists as `shape_mismatch`.

**Writing a new frames builder** (a new picture type only). Every frame must say which solver step it was drawn from (#895), so pictures of one run line up:

- Give the frame record `[JsonPropertyName("step")] [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int? Step { get; init; }`, so it is written even when null.
- Loop over `GraphSubsetFrames.DrawnSteps<TPartial>(run)` (each step with whether it ends the run, and its index in the run's full step list), or `GraphSubsetFrames.Indexed<TPartial>(run)` if you don't need `endsRun`, and add each frame `with { Step = index }`. Never count your own index after filtering.
- Leave `Step` null on frames you add yourself: the solved frame built from the answer, and the blank stopped frame when the run's own stopped Done can't be drawn. A stopped Done you draw from the run keeps its index.
- `FrameStep_Tests` checks every frames visualization for this, and `VisualizeRun_Shape_Tests` checks the answer-alone picture.

### 4.3 Register it

1. Put the class in the problem's `Visualizations/` folder. Nothing else: the server finds every `IVisualization` class by reflection and ties it to the problem its `IVisualization<TProblem>` names (§8.1 has the details), the visualize endpoint finds the frames hook, and `/info` reports `pictureType` by itself.
2. If you added a new **picture type** (rare), add the `VisualizationType` value and the matching entry in `Documentation/visualization-types.json` in the same change; CI compares them. Redux_GUI keeps a vendored copy that needs the same entry.

### 4.4 Test the JSON

Add your problem to the picture type's endpoint tests in `redux-tests/Endpoints/<Type>Frames_Endpoint_Tests.cs`. They post to `/ProblemProvider/visualize?format=frames` with every solver of the problem and check the payload and frames, and they check that the list format and `/info` are byte-for-byte unchanged against snapshots in `redux-tests/Endpoints/Snapshots/`.

A green build proves nothing about the JSON: `System.Text.Json` with `IncludeFields` leaks public fields and skips default interface members, so always look at the output. The frames viewer (`viewer/`) draws it, which is the quickest way to see it.

## 5. Adding steps (animation) to a solver

### 5.1 Choose the answer shape

Steps are typed by **answer shape**, not by picture type. A subset solver records the subset so far whether the frontend draws it as a Graph or a Search Tree. So one recording drives every picture of that problem that draws the same shape.

```csharp
record SolverStep<TPartial>(TPartial Partial, StepEvent Event, string[] Focus, string Caption) {
    public bool? Ok { get; init; }                        // Done only
    public IReadOnlyList<SearchNode>? Tree { get; init; } // search-tree patches, optional
}
enum StepEvent { Try, Accept, Reject, Backtrack, Done }
```

| Event | Means | Typical frame |
| --- | --- | --- |
| `Try` | Considering a choice | The choice is `ElementHighlight` |
| `Accept` | Kept the choice | It joins the answer so far |
| `Reject` | Threw the choice out | It is `Rejected` (or, for a whole candidate, highlighted with its failures marked) |
| `Backtrack` | Undid an earlier choice | The undone part leaves the answer |
| `Done` | Finished, with `ok` | The answer is `Solution`, or the failure `Rejected` |

`Focus` names what the solver is looking at (node ids, an edge id, a clause id, a cell). Use the ids the payload uses (`AutomatonFrames.StateId`, `GraphSubsetFrames.EdgeId`, …), so the frontend can highlight them.

### 5.2 Record inside the real algorithm

Implement `ISolver<TProblem, TPartial>` and put the algorithm in `Solve(problem, rec)`. The plain `solve` runs it with `StepRecorder<TPartial>.Off`, so solving costs nothing extra; the visualize endpoint passes a real recorder.

```csharp
class CliqueBruteForce : ISolver<CLIQUE, NodeSet> {
    public string solve(CLIQUE clique) => Solve(clique, StepRecorder<NodeSet>.Off);

    public string Solve(CLIQUE clique, StepRecorder<NodeSet> rec) {
        // ... for each candidate set:
        if (clique.defaultVerifier.verify(clique, certificate)) {
            rec.Accept(() => new NodeSet(Candidate()), () => $"Try {GraphSubsetFrames.Braces(Candidate())}. Every pair is joined.", Candidate);
            rec.Done(new NodeSet(Candidate()), true, $"{GraphSubsetFrames.Braces(Candidate())} is a clique, found after {tried} tries.");
            return certificate;
        }
        rec.Reject(() => new NodeSet(Candidate()), () => $"Try {GraphSubsetFrames.Braces(Candidate())}: …", Candidate);
        // ...
        rec.Done(new NodeSet([]), false, $"Checked all {tried} candidate sets. None is a clique of size {clique.K}.");
        return "{}";
    }
}
```

The partial, caption and (optionally) focus are lambdas, so nothing is built when recording is off or past the cap. When building the focus is costly, check `rec.Enabled` first.

Never write a second copy of the algorithm just to produce steps. If the steps come from a copy, they can drift from what `solve` really does, and the animation lies.

### 5.3 Whole candidates or pieces: `IIncrementalSolver`

How a picture draws your partial depends on what kind of solver you are:

- **Whole candidates** (a brute force): each Try or Reject judges a complete candidate. The picture highlights it as `ElementHighlight` and never draws it green. Do nothing.
- **Piece by piece** (greedy, backtracking, branch and bound, dynamic programming): also implement the marker `IIncrementalSolver`. Then:
  - Accept and Backtrack: the partial is what you have committed after the move.
  - Try and Reject: the partial is what you have committed, and may also hold the piece under test.
  - The focus names the piece under test, and only that, never the whole partial.

  The picture draws the committed part as `Committed` and only the focus as tried (`ElementHighlight`) or failed (`Rejected`). The committed part turns `Solution` on the correct Done, or on an Accept that ends the run.

Every picture follows the same rule (#886): `Solution` is the confirmed answer only, and what a solver keeps before then is `Committed`. A Done with `ok = false` draws the picks `Rejected`, never `Solution`. `GraphSubsetFrames.Picked(ev, incremental, endsRun, ok)` gives the state of picked elements, so use it rather than writing the rule again, and always pass the step's `ok`.

### 5.4 What to record

- **Record decisions, not loop iterations.** Choose, reject, back out, finish. A step per inner-loop comparison makes an animation nobody can follow.
- **Long runs get capped.** The recorder keeps the first 150 decisions (`StepRecorder.DefaultCap`), plus the last Accept past the cap and the Done step. Done's caption gets "(n not shown)".
- **Long runs get stopped.** The visualize endpoint gives the recorder a time limit (`Visualization:SolveTimeLimitSeconds`, default 10). Past it, the next recorded step throws `StepBudgetExceededException`, and the run comes back stopped. Don't catch it.
- **Always end with `Done`**, even when no answer exists. Its `ok` and partial must match `solve`'s answer.
- **Record the real tie-breaking.** If the C# solver uses a priority queue, the steps follow its order, even when it looks surprising. The animation is there to show what the solver does, not what a textbook does.
- **Branching solvers** can also pass `SearchNode` patches (`nodeId`, `parentId`, `label`, `SearchNodeState`) through the recorder overloads that take a `tree` lambda. That feeds the Search Tree picture; every other picture ignores it.

### 5.5 Writing captions

The caption is the only text that explains a step. Write it for a student watching the animation.

| Do | Don't |
| --- | --- |
| "Read 'b': move from 1 to 3." | "Transition processed." |
| "Switch to x₄ = F. C2 has no true literal left, so undo." | "Backtracking (depth 4)." |
| "Pick S4 = {4, 5}: it covers 2 new elements." | "i = 3, j = 1, best = 2" |
| One sentence, two at most | A paragraph |
| The problem's own words (clause, set, tour) | Internal names (`nodePath`, `memo`) |

### 5.6 Test the replay

There is nothing to write per solver. `redux-tests/Steps/SolverReplay_Tests.cs` finds every typed solver by reflection, runs it on its problem's default instance, and checks that the last step is Done and that its partial matches the answer `solve` returns. If your answer string needs translating into the partial, add a case to `ExpectedFinalPartial` there. Add solver-specific checks to the matching `redux-tests/Steps/<Shape>Solvers_Tests.cs`.

### 5.7 Randomized solvers: the seed, and which tries to show

A randomized solver (WalkSAT, Schöning and PPZ, #932) records steps like any other, from inside the run that gives the answer. Two things are different: the run must be repeatable, and it can be far too long to show whole. The three 3SAT solvers in `Problems/NPComplete/NPC_SAT3/Solvers/` are the worked examples, and the try recording they share is `SatTries.cs` in the same folder.

**Take every random choice from a seed.**

- Implement the marker `IRandomizedSolver` next to `ISolver<TProblem, TPartial>`. Its one member is the seed. Implement it explicitly, `int? IRandomizedSolver.Seed { get; set; }`, so `/info` doesn't show it (the `/info` snapshots check this).
- In `Solve`, make one generator per solve: `Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);`. Draw every random choice from it, never from `new Random()` or `Random.Shared`.
- Go over the instance in a fixed order: a list in the order of first appearance, never a `HashSet<string>`. String hash codes change from process to process, so a hash set would give a different run for the same seed after a restart.
- Recording must not draw from the generator. Then a run with steps and one without give the same answer.
- Mark it `IIncrementalSolver` only if a try builds its answer piece by piece (§5.3). WalkSAT and Schöning are not incremental: each step's partial is the whole assignment they are trying, so the picture highlights it as a candidate. PPZ is: a try sets one variable per step, and the partial holds only the variables set so far.

You don't handle the seed anywhere else. `ISolver<T, TPartial>.Run` uses the seed it was given, or picks one, and reports it in `SolveRun.Seed`. It then puts the solver's own seed back, so picking a seed for one run never fixes the next one. `RandomizedSolvers.Apply(solver, seed)` gives a solver the seed a caller asked for, and does nothing for a solver that isn't randomized.

**Where the seed goes in and comes out.** Every endpoint that draws a run takes an optional `seed` query parameter (an int):

| Endpoint | The seed used comes back as |
| --- | --- |
| `visualize?format=frames` (a picture type that builds its own frames) | `"seed"` at the top of the frames response |
| `visualizeRun` | `"seed"` next to `"solver"` and `"answer"` |
| `reductionView` (the solve phase) | `"seed"`, right after `"solver"` |
| `visualize` in the list format, and the plain `solve` | not reported: the list is a bare array and `solve` a bare string, and Redux_GUI reads both |

`"seed"` is only written for a randomized solver, so every other response looks exactly as it did. Redux_GUI reads none of the three responses that carry it, and sends no seed, so nothing changes for it. The same seed on the same instance gives the same steps, the same answer and the same bytes. Without `seed`, the response says which seed was picked, so a frontend can offer "run again with this seed".

**Show the first try and the successful one.** A run can be 100 tries of 300n flips. Recording every flip would use up the 150-step cap inside the first try and hide the try that found the answer. So the solver holds its steps back while it searches, which costs nothing when recording is off. `SatTries<TMove>` does the holding back and the recording; a solver reports its tries to it and says how a move reads:

1. While the search runs, report each try: `tries.Begin(start)` with its starting values, `tries.Add(move)` for each move (WalkSAT's: the clause, the variable and why that variable was picked), and `tries.End(values, solved)`. `SatTries` keeps only the first try, the current one and the last failed one, and keeps nothing at all when recording is off. Call `rec.CheckTime()` once per move (or per try, if a try is short), so the time limit still stops a long run even though no step has been recorded yet.
2. Once the search ends, call `tries.Record(rec)`. It records the first try in full; the tries in between as one Reject ("Tries 2–37: no satisfying assignment (each ends after 900 flips)."); and the successful try in full. If every try failed, it records the first try and one step for the rest.
3. It fits the cap itself. `rec.Cap` tells it how many steps the recorder keeps (20 in the Reduction View's solve phase, 150 elsewhere). The successful try gets at least half the room. A try longer than its share keeps its start, its first and last moves, and one step for the moves in the middle ("Flips 9–592 of try 1 are not shown one by one. After them, C3 has no true literal."). Then the recorder never has to cut anything, and the Done caption needs no "(n not shown)".

A solver subclasses `SatTries<TMove>` with its move type (a private nested `Tries` class in each of the three solvers). It must give `SolverLabel` (for the give-up caption), `MoveNoun` ("flip"), `EachTry` (the summary's "ends after 900 flips"), `Apply` (play a move on the values), `MoveCaption` and `MoveFocus`. The rest has defaults that read like a local search (the table below); a solver that judges its assignment only once its moves are done sets `ChecksAtEnd` and overrides `StartCaption` and `Close`, as PPZ does. `SatFormula` reads the formula the way all three need it: variables in order of first appearance, clauses as index pairs, and the certificate.

The steps of one try:

| Step | Event | Partial | Focus |
| --- | --- | --- | --- |
| The try's random start ("Try 1: start from a random assignment, …", or "Restart. Try 5: …") | `Try`; `Accept` if the start already satisfies every clause | The start assignment | none |
| One flip ("Flip 3: C2 (x1 \| !x2 \| x3) has no true literal. Flip x2 to False: a free flip, no clause turns false. Now C4 has no true literal.") | `Try`; `Accept` for the flip that satisfies every clause | The whole assignment after the flip | The clause (`c1`) and the variable (`v:x2`) |
| A failed try ends ("Try 1 ends after 900 flips: C2 has no true literal.") | `Reject` | Where it ended | none |

The run ends with a Done. When it found an answer, the Done says where: "Every clause has a true literal under x1 = True, …: found by try 3 after 4 flips." When every try failed: **"Gave up after 100 tries: no answer found."** Its `ok` is false and its partial is empty. A heuristic that gives up has proved nothing, so its captions never say the formula is unsatisfiable or has no solution. Only an exact solver can say that (#877).

**Pictures.** A randomized solver draws on the pictures of whole assignments: the Grouped Graph, the Formula and the factor graph. The Search Tree is not one of them, because tries are not a tree of choices. `ISearchTreeVisualization` refuses every `IRandomizedSolver` through `IFramesVisualization.DrawsSolver`, so `visualize` and `reductionView` answer 400 `solver_mismatch`, and `visualizeRun` lists the tree as `solver_mismatch`. A new picture that can't draw some solvers of its own problem overrides `DrawsSolver` the same way.

**Schöning and PPZ.** These follow the same recipe through `SatTries`; only the moves differ:

- **Schöning:** a try is a random start and up to 3n walk steps. Each step picks a clause with no true literal and flips the variable of one of its literals, chosen at random. It uses WalkSAT's three rows; the flip caption says "Pick one of its literals at random and flip its variable". It makes few, short tries ((4/3)^n of them, at most 1,000), so the summary step matters more than it does for WalkSAT. The assignment after the last flip of a try is checked too.
- **PPZ:** a try takes the variables in a random order and sets them one per step. A variable that a clause forces (every other literal of the clause is set and false) takes the forced value, and the caption names that clause; any other variable takes a random value. Then the try checks the assignment. Its rows:

| Step | Event | Partial | Focus |
| --- | --- | --- | --- |
| The try's order ("Try 1: take the variables in a random order: x3, x1, x2.", or "Restart. Try 5: …") | `Try` | Nothing set | none |
| One variable ("Step 2: set x1. Every other literal of C3 (x1 \| !x2 \| x3) is false, so C3 forces x1 = True.", or "… No clause forces it, so it takes a random value: x1 = True.") | `Try` | The variables set so far; the rest are left out, so the picture draws them Background | The forcing clause, if there is one, and the variable |
| The check ("Try 1 has set every variable, but C2 has no true literal.") | `Accept` if every clause has a true literal, else `Reject` | The whole assignment | A Reject: the clauses with no true literal |

- All three end with the same Done: "found by try 3" (PPZ) or "found by try 3 after 4 flips", or "Gave up after N tries: no answer found. PPZ can miss an answer, so the formula may still have one."
- Tests: `redux-tests/Steps/WalkSat_Tests.cs`, `Schoning_Tests.cs` and `Ppz_Tests.cs` check that the same seed gives the same steps, with or without recording; that the steps match the answer; the give-up wording; that the run fits a cap of 150, 20 and 7; and that a run that restarts keeps try 1 and the successful try. To reach a run that restarts, the tests use an internal constructor with fewer flips or tries and then search seeds. `redux-tests/Endpoints/WalkSatSeed_Endpoint_Tests.cs` and `SchoningPpzSeed_Endpoint_Tests.cs` check that the seed goes in and comes out on all three endpoints, and that the Search Tree refuses the solver. Once a solver records steps, take it out of any "solver without steps" endpoint test; no 3SAT solver without steps is left.

## 6. Reductions

A reduction already builds the target instance. To appear in the Reduction View, it needs three more things.

1. **Gadgets.** Each `Gadget` has a `gadgetKind` from `GadgetKind` (`Element`, `Group`, `EdgeRule`, `Palette`, `OrGadget`, `Bound`; JSON `element`, `group`, `edgeRule`, …), the source ids it came from (`sourceIds`) and the target ids it made (`targetIds`), and an optional `note`. Ids must match the ids each side's own picture uses; that is what links the two panes. Use the constructor `new Gadget(GadgetKind kind, sourceIds, targetIds, note: …)`.
2. **Build steps.** Implement `IRecordedReduction<T, U>`. Build the target in one place that takes a `StepRecorder<BuildDelta>`, and record one step per gadget, in construction order, as you really build it. Each `BuildDelta` names the source ids (`SourceFocus`), the target `Nodes` and `Edges` (`BuildEdge`) it added, the pairs a rule deliberately left unjoined (`Skipped`), the gadget indices (`Gadgets`), and the target's `Bound` once fixed. Captions use both problems' words ("Clause C2 becomes cluster 2: one node per literal."). Then:
   - `Rebuild(rec)` builds a fresh copy with that recorder and returns a `ConstructionRun`.
   - Also give the class a public constructor `(string instance, StepRecorder<BuildDelta> rec)`, so the endpoints build once. A test checks every recorded reduction has it.
3. **A backward map.** Implement `IReversibleReduction<T, U, TPartial>`, where `TPartial` is the SOURCE's answer shape:
   - `MapBack(targetAnswer, rec)` returns the source's answer as text and as `TPartial`, recording one step per target element read ("x₂ is in the clique, so set x₂ = T"). Focus the target's ids. Don't record Done; the interface does, using the source's verifier.
   - `EmptyAnswer()` returns the source's empty answer.

`reduceToCLIQUE` (Independent Set → Clique) is a short, complete example of all three.

The Reduction View then plays three phases: **Build**, **Solve** (the target's solver, by default the reduction's `ReductionViewSolver()` or the target's default solver; `?solver=` overrides), and **Map back**. Both panes reuse the source and target problems' own frames pictures; a reduction never draws anything itself. Only the last frame carries a verdict (`ok`).

If the target's default solver can't finish the reduction's own default instance within the time limit, override `ISolver? ReductionViewSolver()` to name a faster exact solver of the target.

## 7. Worked example: DFA

This is DFA Acceptance on the integration branch.

![DFA in the Automaton picture type, mid-run](images/auto--dfa-ends-in-ab-input-abaab.png)

### 7.1 The problem

`Problems/P/P_DFA/DFA_Class.cs` parses `((N,A,E,S,F),I)` into `nodes`, `alphabet` (`List<char>`), `edges` (`DFAEdge(From, Symbol, To)`), `startState`, `acceptStates` and `inputString`. Nothing here changed for the picture.

### 7.2 The visualization: one mapping

`Visualizations/DFAVisualization.cs` keeps its legacy `GraphLaTeX` members and adds the Automaton picture:

```csharp
class DFAVisualization : IVisualization<DFA, API_GraphJSON>, IAutomatonVisualization<DFA> {
    public VisualizationType visualizationType { get; } = VisualizationType.GraphLaTeX; // /info is unchanged
    public ISolver solver { get; } = new DFASolver();
    // ... the other legacy members ...

    Type? IVisualization.StepShape => typeof(ActiveStates);

    AutomatonSpec IAutomatonVisualization<DFA>.Describe(DFA problem) => Spec(problem);

    // The list format drew typed steps before the picture existed, so it keeps getting them.
    bool IFramesVisualization.ListReadsTypedSteps() => true;

    internal static AutomatonSpec Spec(DFA problem) => new(AutomatonKind.Dfa,
        problem.nodes, problem.alphabet.Select(c => c.ToString()).ToList(),
        problem.edges.Select(e => new AutomatonTransition(e.From, e.Symbol.ToString(), e.To)).ToList(),
        problem.startState, problem.acceptStates, problem.inputString);
}
```

The Automaton builder (`Interfaces/graphs/AutomatonFrames.cs`) does the rest:

- gives each state a layer, its breadth-first distance from the start state;
- merges parallel transitions into one arrow with a combined label (`a,b`);
- sends the start state, accept states and input tape;
- adds an isolated **Garbage** state (`garbage`) when a DFA is missing a transition, listing the missing moves, so a run that falls off the machine has somewhere visible to go. A symbol of the input outside the alphabet is a missing move too: `garbage.missing` lists the `{ state, symbol }` the run hits, and Garbage appears even if every in-alphabet move exists.

### 7.3 The solver: record each symbol read

The Automaton type's answer shape is `ActiveStates(states, position)`: the states the run is in and how many symbols it has read. DFA and NFA share it. `Solvers/DFASolver.cs`, shortened:

```csharp
class DFASolver : ISolver<DFA, ActiveStates> {
    public string solve(DFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    public string Solve(DFA problem, StepRecorder<ActiveStates> rec) {
        string inputString = problem.inputString == "ε" ? "" : problem.inputString;
        string currentNode = problem.startState;
        int consumed = 0;
        rec.Try(() => new ActiveStates([problem.startState], 0),
            () => $"Start in {problem.startState}.", () => [AutomatonFrames.StateId(problem.startState)]);

        foreach (char character in inputString) {
            var edge = problem.edges.FirstOrDefault(e => e.From == currentNode && e.Symbol == character);
            if (edge is null) {
                rec.Reject(() => new ActiveStates([], consumed),
                    () => $"{currentNode} has no move on '{character}', so the DFA falls into the Garbage state and stays there.",
                    () => [AutomatonFrames.GarbageId]);
                rec.Done(new ActiveStates([], consumed), false,
                    $"Rejected: {currentNode} has no move on '{character}', so the run ends in the Garbage state.");
                return "No Solution Exists: DFA cannot transition with this character";
            }
            string from = currentNode;
            currentNode = edge.To;
            consumed++;
            rec.Try(() => new ActiveStates([edge.To], consumed),
                () => $"Read '{character}': move from {from} to {edge.To}.", () => [AutomatonFrames.TransitionId(from, edge.To)]);
        }
        bool ok = problem.acceptStates.Contains(currentNode);
        if (!ok)
            rec.Reject(() => new ActiveStates([currentNode], consumed),
                () => $"The input is used up in {currentNode}, which is not an accept state.", () => [AutomatonFrames.StateId(currentNode)]);
        rec.Done(new ActiveStates([currentNode], consumed), ok, ok
            ? $"The input is used up in {currentNode}, an accept state, so the DFA accepts."
            : $"Rejected: the run ended in {currentNode}, which is not an accept state.");
        return ok ? "The sequence of states to accept is: …" : "No Solution Exists: The DFA ended in a non-accepting state";
    }
}
```

The real solver loops over the edges instead of `FirstOrDefault` and keeps the path for its answer text. A symbol outside the alphabet takes the same branch as a missing move: the DFA falls into Garbage. An empty set of states in a DFA step means the run is in Garbage. An NFA has no Garbage state: a branch that meets a symbol outside the alphabet dies there (a Reject at a dead end).

What the frontend draws from those steps, without any DFA-specific code:

| Step | Picture |
| --- | --- |
| `Try` "Read 'b': move from 1 to 3." | State 3 and the arrow 1 → 3 are `ElementHighlight`; the tape shows two symbols read |
| `Reject` into Garbage | Garbage turns `Rejected`; the missing move is named in the caption |
| `Done`, `ok: true` | The final state is `Solution`; states never reached are `Untraveled` |
| `Done`, `ok: false` | The final state is `Rejected`; states never reached are `Untraveled` |

The same steps also drive the DFA **Trace Table** (`DFATableVisualization`, one row per step, built with `AutomatonTraceTable`).

NFA uses the same `ActiveStates`. `NFASolver` is a depth-first search that records each move and each backtrack; `NFASubsetSolver` records the whole active set per symbol. The Automaton builder also computes the NFA's runs itself (up to 40) and sends them as `paths`, which feed the "follow one path" picker.

### 7.4 The tests

- `redux-tests/Endpoints/AutomatonFrames_Endpoint_Tests.cs`: the frames JSON for DFA and NFA with every solver, plus the list-format and `/info` snapshots.
- `redux-tests/Steps/AutomatonSolvers_Tests.cs`: what the solvers record, including the Garbage case.
- `SolverReplay_Tests` covers `DFASolver` automatically.

### 7.5 What went away

- The hand-written coloring loops and the `"green"` / `"white"` strings, for the frames picture. The legacy list format keeps them until #732 removes it.
- The `List<Object>` cast: steps are typed.
- TikZ, once every frontend draws the Automaton picture (TikZ leaves Redux after every visualization is ported).

## 8. A custom visualization outside the picture types

The picture types cover most problems, but a contributor can still write a visualization by hand that uses none of them. The project's rule: **a handwritten visualization is tagged "Unclassified", and it is still drawn wherever a frontend can draw it.** Nothing in the backend refuses one. This section says what the backend does with it, what each frontend does with a type it doesn't know, and how to make one draw.

Prefer a picture type when one fits (§4.1): it is drawn by both frontends' shared renderers, its states follow the shared rules, and its frames line up with every other picture of the run. Write a custom one when no type fits and a new type isn't worth a mockup and an approval yet, or to try an idea out before proposing it as a type.

### 8.1 Step 1: the backend, two ways

**(a) A legacy list visualization.** Implement `IVisualization<TProblem>` (or `IVisualization<TProblem, TPayload>` to name the payload class) and serve the old flat list: `visualize` (the instance), `SolvedVisualization` (the answer) and, optionally, `StepsVisualization` (one item per step). Every item is an `API_JSON`.

```csharp
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_JOBSEQ;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;

// The payload. Use properties: the list format is written without IncludeFields, so a public field is silently dropped.
class API_Lateness : API_JSON {
    public string kind { get; } = "lateness";
    public List<string> rows { get; set; } = new();
}

class JobSeqLatenessVisualization : IVisualization<JOBSEQ, API_Lateness> {
    public string visualizationName { get; } = "Job Sequencing Lateness";
    public string visualizationDefinition { get; } = "How late each job finishes, in the order the solver tries.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Your Name" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable; // see below
    public ISolver solver { get; } = new JobSeqBruteForce();

    public API_Lateness visualize(JOBSEQ problem) => new();
    public API_JSON SolvedVisualization(JOBSEQ problem, string solution) => new API_Lateness { rows = { solution } };

    // Optional: typed steps for the list. Declare the shape your solver records (explicitly, so /info doesn't show it);
    // the list format then hands StepsVisualization that solver's SolverStep<JobOrder> objects.
    Type? IVisualization.StepShape => typeof(JobOrder);
    public List<API_JSON> StepsVisualization(JOBSEQ problem, List<object> steps) =>
        steps.Cast<SolverStep<JobOrder>>().Select(s => (API_JSON)new API_Lateness { rows = { s.Caption } }).ToList();
}
```

- **`visualizationType` is not a free string.** It is the `VisualizationType` enum, and it is what Redux_GUI picks its renderer by (§8.2). Declare the existing value whose renderer can draw your payload (an `API_TableJSON` under `DynamicTable`, an `API_GraphJSON` under `GraphD3`), or add a new value. A new value goes in three places in one change set (README §3.3): the enum, `Documentation/visualization-types.json` (`VisualizationType_Tests.ManifestMatchesEnum`), and Redux_GUI's vendored `visualizationTypes.json` plus a key in its `Visualizations.js`, which `npm run check:visualizations` requires for every manifest value. `Unimplemented` is allowed only for classes on `VisualizationType_Tests.UnimplementedAllowlist`, and neither frontend offers it (§8.2).
- **Steps.** The list format gives a legacy visualization the solver's steps only when their shape matches its `StepShape`: `SolverStep<TPartial>` objects for a typed solver, the solver's own objects for an untyped one (`StepShape` null, the default). With another shape it gets none, and the response is the instance and the answer.
- **Under `format=frames`** it gets the generic wrapping: `{ "type": "<its visualizationType>", "payload": <visualize(instance)>, "frames": [<each StepsVisualization item>, <SolvedVisualization(instance, answer)>] }`. `payload` is null when `visualize` returns `API_empty`, and empty items are left out. A run stopped by the time limit gets no closing item. The frames are your list items as they are: no `event`, `caption`, `focus`, `ok` or `step` unless your payload has fields by those names. This is the old list in an envelope, not a picture.
- **`pictureType` is null** in `/info`, in the problem's `defaultVisualization` and in `allInfo` (`PictureTypes.Of` returns null for a visualization with no frames hook). That null means "no frames picture": `visualizeRun` lists the visualization in `unavailable` with reason `no_frames`, and the Reduction View can't draw a side with it (400 `no_source_picture` / `no_target_picture`).

**(b) A frames visualization with its own picture type.** Implement `IFramesVisualization` and build the whole response yourself: a type name, a payload with a `kind`, and one frame per step.

```csharp
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;    // GraphSubsetFrames: DrawnSteps, Picked, StoppedCaption; StateRef
using API.Interfaces.Schedule;  // ScheduleFrames: JobId, Runs, ParseJobOrder; JobScheduleSpec
using API.Interfaces.Steps;

sealed record TimelinePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("jobs")] IReadOnlyList<string> Jobs);

sealed record TimelineFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("jobs")] IReadOnlyDictionary<string, StateRef> Jobs) {
    // Always written, null on a frame you add yourself (#895).
    [JsonPropertyName("step")] [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int? Step { get; init; }
    [JsonPropertyName("ok")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Ok { get; init; }
}

class JobSeqTimelineVisualization : IVisualization<JOBSEQ, API_TableJSON>, IFramesVisualization {
    // ... the legacy members (visualizationName, ..., visualizationType, solver, visualize, SolvedVisualization) ...

    Type? IVisualization.StepShape => typeof(JobOrder);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) { ... } // §8.5 has it in full
}
```

- **The type name.** `FramesResponse.Type` is a plain string, and the visualize endpoint sends whatever you put there. On the integration branch, though, `PictureType_Endpoint_Tests` checks that every frames visualization's type is a `VisualizationType` name, so a new picture type name is a new enum value and manifest entry (the three places above). There is no "custom" escape value: that is a gap, and the test is where to change it if the project decides a free-form type name should pass.
- **`pictureType`** in `/info` and `allInfo` is that type name. You don't declare it: the default `PictureType()` builds your frames once for the problem's default instance, from an empty stopped run (`SolveRun.TimedOutAnswer`, then `"{}"` if that throws), and reads `Type` off the response. So `BuildFrames` must cope with a run that has no steps, and the name in `/info` always matches what `format=frames` returns.
- **`DrawsAnswerAlone()`.** Leave it true (the default) if your picture shows an answer: given the steps of another shape (`SolveRun.StepsFor` returns none), draw the payload and one Done frame from the answer, `step: null`. Return false only for a picture of the search itself; `VisualizeRun_Shape_Tests.OnlyTheSearchTree_CannotDrawTheAnswerAlone` currently allows that only for the Search Tree, so a second such picture means changing that test.
- **Implement everything new explicitly** (`Type? IVisualization.StepShape`, `FramesResponse IFramesVisualization.BuildFrames`), so `/info` shows only the legacy members and `pictureType`. `/info` is written with `IncludeFields`: a public field on the visualization class appears in it, and a default interface member never does.
- **Keep the list members.** They are not optional: `IVisualization` requires them, and they are what both frontends draw until they have a renderer for your type (§8.2). The simplest list format is another visualization's, by delegation, as `SatFactorGraphVisualization` reuses `SatDefaultVisualization`'s.

**Registration and discovery** are the same for both:

1. Put the class in `Problems/<class>/<problem>/Visualizations/`. Nothing lists it: at startup `ProblemProvider` scans the assembly for classes that implement `IVisualization` (`ScanTypes`), keyed by lower-cased class name, so the name must be unique among visualizations (a clash throws at startup).
2. It belongs to the problem named by its `IVisualization<TProblem>`, or, failing that, by its own `solver`'s `ISolver<TProblem>` (`ProblemOfVisualization`). The Navigation endpoints (`Problem_VisualizationsRefactor`, `Batch/allVisualizations`, `allVisualizationTypes`, `allInfo`) and `visualizeRun` list it from then on. The problem's default stays the problem's `defaultVisualization`; an extra visualization changes nothing for old clients.
3. It needs a public parameterless constructor (Navigation and `/info` create it with `Activator.CreateInstance`).

**The step shape.** Steps come from the solver, typed by answer shape (§5): `ISolver<TProblem, TPartial>` records `SolverStep<TPartial>` through a `StepRecorder<TPartial>`, and `ISolver.Run` returns a `SolveRun` (`Answer`, `Steps`, `StepShape`, `TimedOut`, `Incremental`). Your visualization's `StepShape` must equal the solver's `TPartial` for its steps to reach it: `run.StepsFor(StepShape)` hands over the steps when the shapes match and nothing otherwise. Use an existing shape from `Shapes.cs` (here `JobOrder`) so every solver that already records it animates your picture with no change to the solver.

### 8.2 Step 2: what each frontend does with a type it doesn't know

**Redux_Frontend** (the renderers on `task/239-gadgets`; the catalog change is PR FE#269):

- **Catalog cards and filter (FE#269).** The card's chip comes from `derivePictureTypeOption` (`data/taxonomy.js`): `pictureType` from `allInfo` if it is a string, else the legacy type. A `pictureType` the site has no entry for, or (with `pictureType` null) a legacy type it has no entry for, is the chip **Unclassified**, and the Visualization filter counts it under Unclassified like any other option. So a legacy visualization that declares `DynamicTable` is filed under that type's option, and one that declares a new value or a new picture type reads Unclassified. Only `Unimplemented` gets no chip and no count. Before FE#269 (`task/239-gadgets`), an unknown type got no chip at all.
- **Problem page dropdown.** Every visualization of the problem is offered except a legacy `Unimplemented` one (`Workbench.js` drops only types the legacy map sends to null). An unknown type is tagged with its own name.
- **Which request.** `lib/pictureRequest.js` asks for `format=frames` only when `pictureType` is unknown to it (`undefined`) or names a picture type with a renderer (`registry.js`, `PICTURE_RENDERERS`), and keeps the response only when its `type` is in `PICTURE_RENDERERS`. Otherwise it asks again without `format` and draws the flat list. So a custom frames picture with no renderer here is drawn from its **list format**, and so is the generic wrapping of a legacy visualization (its `type` is a legacy name, never a picture renderer). `visualizeRun` is used only for a visualization whose picture has a renderer; a run's picture with no renderer is listed as `no_renderer`, and its pane shows the instance with the note "… isn't drawn from this run, so it shows the instance."
- **Drawing the list.** `VisualizationCanvas` picks a renderer with `resolveVisualizationType(visualizationType, frame)` (`data/visualizationTypes.js`): the frame's `kind` wins when it names one of the six universal types (`graph`, `quantumCircuit`, `booleanSatisfiability`, `recursiveSet`, `stepTable`, `pumpSchedule`), else the legacy type is mapped (`GraphD3` and `GraphLaTeX` to `graph`, `SetD3` to `recursiveSet`, `DynamicTable` to `stepTable`, …). If neither maps, it shows its cannot-render message, "This visualization type isn't supported yet." A frame that maps but doesn't validate shows "Cannot render: this frame's data doesn't match what this visualization type expects."

**Redux_GUI** (`ReduxAPI_GUI`):

- It never asks for `format=frames` and never reads `pictureType`: it calls `visualize` for the flat list and picks the renderer by the legacy `visualizationType` alone (`components/Visualization/svgs/Visualizations.js`, checked by `renderability.js` `isRenderable`). A frames picture of any type, built-in or custom, is drawn from its list format here.
- **Dropdown** (`VisualizeRowReact.js`): every visualization is listed; one whose `visualizationType` has no key in `Visualizations.js`, or is `Unimplemented`, is disabled with "(no renderer available)" and never auto-selected, and its type tag reads "Unimplemented" (`visualizationCategories.js`). If it is drawn anyway, the pane says `<problem> declares type "<type>", which this interface can't render.` A renderer that throws shows "The <name> visualization (type "…") failed to render."
- **Browse page filter and cards** group by the legacy type's category; an unknown type is counted only under "Unimplemented", and a card lists only renderable categories. Redux_GUI has no "Unclassified" chip.

So in both frontends a custom visualization is **listed** whatever its type, and **drawn** exactly when its list format is something the frontend can already draw.

### 8.3 Step 3: making a custom visualization draw

There are two ways.

**(i) Send a payload in a shape the frontends already draw.** For the list format (both frontends): return an existing payload class and declare its legacy type: `API_GraphJSON` (`kind: "graph"`) under `GraphD3`, `API_TableJSON` (`kind: "table"`) under `DynamicTable`, an `API_QUANTUMCIRCUIT` with a `d3` arm under `QuantumCircuitD3`, `API_SET` under `SetD3`, `API_SAT` under `BooleanSatisfiability`. Redux_GUI goes by the declared type; Redux_Frontend goes by a universal `kind` first (only `graph` and `quantumCircuit` among the backend's kinds; `table`, `sat` and `set` are mapped through the declared type). For `format=frames`, "an existing shape" means an existing picture type: if your drawing fits one, use it (§4) and you are back on the supported path. A frames response with a new `type` is never drawn by an existing renderer, whatever its payload looks like, because both renderer tables key on `type`.

**(ii) Add a renderer.** This is a new picture type in all but name: mock it up and get it approved first (README §1).

- Redux_Frontend, for a frames type (the steps are in the header of `components/pictures/registry.js`):
  1. `components/pictures/<Type>Renderer.js`: SVG inside `DiagramZoom`, colors only from `pictureStates.js` `pictureStateStyle()`, gadget marks with `gadgetRings.js`, `data-*-id` on every piece.
  2. `components/pictures/registry.js`: the renderer in `PICTURE_RENDERERS` under the exact `type`; optionally `PICTURE_ANNOTATIONS` (Legend entries), `LEGEND_BY_STEP`, `PICTURE_PLAIN_FRAMES`, `PICTURE_RUNS`.
  3. `lib/reductionView.js`: `PIECE_ID_ATTRIBUTES` for a new `data-*-id`, and `viewPictureWorkable` if the Reduction View needs a rule for it.
  4. `components/workbench/Legend.js` for a new swatch kind; `pictureStates.js` and `themeTokens.js` only for a new state (avoid one).
  5. `data/taxonomy.js` (the type's label and its catalog option, `PICTURE_TYPE_MAP` after FE#269) and `data/pictureTypes.js` (`PICTURE_TYPE_INFO`). Until the type is in `PICTURE_TYPE_MAP` its card chip stays Unclassified.
  6. A payload captured from the live API in `tests/e2e/picture-payloads.js`, covered in `tests/e2e/picture-renderers.spec.js`.

  For a legacy list type, the old path instead: `VISUALIZATION_TYPE_MAP` in `data/visualizationTypes.js`, `RENDERERS` in `components/detail/visualizations/VisualizationCanvas.js`, and a validator for `validateFrame`.
- Redux_GUI (legacy types only; it has no frames renderers at all):
  1. `components/Visualization/svgs/<X>SvgReact.js`.
  2. A render factory and a `["<VisualizationType>", factory]` entry in the `new Map([...])` of `Visualizations.js` (the key a double-quoted literal, first in its pair: the check reads it with a regex).
  3. The value in the vendored `components/Visualization/svgs/visualizationTypes.json`, copied from Redux's `Documentation/visualization-types.json`.
  4. Its category in `VISUALIZATION_TYPE_CATEGORIES` (`svgs/visualizationCategories.js`), or its tag and filter read "Unimplemented".
  5. `npm run check:visualizations`.

  A Redux_GUI renderer for a frames picture would first need the GUI to request `format=frames` at all. That doesn't exist yet: it is a gap for every picture type, not just custom ones.

### 8.4 Step 4: draw the steps, or the answer alone

Visualizations in Redux are expected to draw their problem's **default solver's steps** through the frames format: the project wants every solver and every reduction to show how it works, not just its answer. A custom visualization is no exception:

- Pick its `StepShape` to be the answer shape the problem's default solver records (`problem.defaultSolver`, and the visualization's own `solver`), so the default run animates. Check with `visualize?format=frames` (no `solver=`) and with `visualizeRun?solver=<default solver>`.
- Draw every step you are given, one frame each, in order, with `step` set to its index from `GraphSubsetFrames.DrawnSteps<TPartial>(run)` (never your own count), and follow the shared rules: `Solution` only on the confirmed answer, `Committed` for what an incremental solver keeps, the picks `Rejected` on a Done with `ok: false`, a blank Done after a time-limit stop (§5.3, `GraphSubsetFrames.Picked`).
- **When it can't draw the steps** (a solver of another shape, an untyped legacy solver, a stopped run with nothing drawable), fall back to the answer alone: the payload and one added Done frame built from `run.Answer`, `step: null`, `ok` from the problem's own verifier. That is what `DrawsAnswerAlone() => true` promises, and `FrameStep_Tests.RunOfAnotherShape_OnlyAnAddedFrame_WithNoStep` checks it for every frames visualization.
- A legacy list visualization can draw steps only through `StepsVisualization`, with the matching `StepShape` (§8.1); its frames have no `step`, so they can't line up with the other pictures of a run.

### 8.5 Step 5: checklist and a worked example

**Checklist**

- [ ] No picture type fits (§4.1), and the visualization is an extra one: the problem's `defaultVisualization` is unchanged.
- [ ] The class has a unique name, a parameterless constructor, and `IVisualization<TProblem>` naming its problem.
- [ ] Its list format (`visualize`, `SolvedVisualization`) returns a payload an existing legacy renderer draws, under that renderer's `visualizationType`, unless you also add the renderers (§8.3).
- [ ] Payload classes use properties, not fields, and carry a `kind`.
- [ ] A frames type: every new member is explicit; the type name is a `VisualizationType` value in the manifest; `BuildFrames` works on an empty stopped run; every frame has `step`; the answer alone draws when the steps can't.
- [ ] Its `StepShape` is what the default solver records, and the default run animates.
- [ ] You read the real JSON: `/ProblemProvider/info?interface=<class>` (`visualizationType`, `pictureType`), `visualize` in both formats, and `visualizeRun`.
- [ ] The full test suite passes (`FrameStep_Tests`, `VisualizeRun_Shape_Tests`, `PictureType_Endpoint_Tests` and `VisualizationType_Tests` cover a new visualization by reflection).

**Worked example: a deadline timeline for Job Sequencing.** The Schedule picture already draws Job Sequencing's bars; suppose a teacher wants a simpler second view, one row per job in run order with its slack (deadline minus finish time) and the late ones marked. It reuses Job Sequencing's answer shape `JobOrder` (`J0`, `J1`, … in run order), so `JobSeqBruteForce` animates it with no change, and `ScheduleFrames.Runs` already works out each job's start, end and lateness. Its list format is the default visualization's job table (`DynamicTable`), so both frontends draw that table until one has a `Timeline` renderer.

```csharp
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_JOBSEQ;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
using API.Problems.NPComplete.NPC_JOBSEQ.Visualizations;

sealed record TimelineJob(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("deadline")] int Deadline,
    [property: JsonPropertyName("penalty")] int Penalty);

sealed record TimelinePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("k")] int K,
    [property: JsonPropertyName("jobs")] IReadOnlyList<TimelineJob> Jobs);

/// <summary>One frame: the jobs placed so far, in run order, each with its slack, and a state per job.</summary>
sealed record TimelineFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("order")] IReadOnlyList<string> Order,
    [property: JsonPropertyName("slack")] IReadOnlyDictionary<string, int> Slack,
    [property: JsonPropertyName("jobs")] IReadOnlyDictionary<string, StateRef> Jobs) {
    [JsonPropertyName("step")] [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int? Step { get; init; }
    [JsonPropertyName("ok")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Ok { get; init; }
}

class JobSeqTimelineVisualization : IVisualization<JOBSEQ, API_TableJSON>, IFramesVisualization {
    public string visualizationName { get; } = "Job Sequencing Timeline";
    public string visualizationDefinition { get; } = "One row per job in the order it runs, with its slack: how long before its deadline it finishes. A late job is marked.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Your Name" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new JobSeqBruteForce();

    // The list format is the default visualization's table, so every frontend draws something today.
    private readonly JobSeqVisualization _list = new();
    public API_TableJSON visualize(JOBSEQ problem) => _list.visualize(problem);
    public API_JSON SolvedVisualization(JOBSEQ problem, string solution) => _list.SolvedVisualization(problem, solution);

    // --- The Timeline picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(JobOrder);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        var problem = ParseGuard.CreateProblem<JOBSEQ>(instance);
        var spec = JobSeqVisualization.Spec(problem);
        var payload = new TimelinePayload("timeline", problem.K,
            spec.Jobs.Select((j, i) => new TimelineJob(ScheduleFrames.JobId(i), $"Job {i}", j.Deadline, j.Penalty)).ToList());

        var frames = new List<object>();
        bool lastIsDone = false;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<JobOrder>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? Blank(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            lastIsDone = step.Event == StepEvent.Done;
        }
        if (run.TimedOut) {
            if (!lastIsDone) frames.Add(Blank(GraphSubsetFrames.StoppedCaption));
        } else if (!lastIsDone) {
            // The answer alone: steps of another shape, or a solver that records none.
            bool ok;
            try { ok = problem.defaultVerifier.verify(instance, run.Answer); } catch { ok = false; }
            var answer = ScheduleFrames.ParseJobOrder(spec.Jobs.Count, run.Answer);
            frames.Add(Frame(spec, answer, StepEvent.Done, [], ok ? "The answer." : "No order works.", ok));
        }
        return new FramesResponse("Timeline", payload, frames);
    }

    private static TimelineFrame Frame(JobScheduleSpec spec, JobOrder order, StepEvent ev, string[] focus, string caption,
        bool? ok, bool incremental = false, bool endsRun = false) {
        var index = Enumerable.Range(0, spec.Jobs.Count).ToDictionary(ScheduleFrames.JobId, i => i, StringComparer.Ordinal);
        var runs = ScheduleFrames.Runs(spec, order.Order.Select(id => index[id]).ToList());
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var slack = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var jobs = new SortedDictionary<string, StateRef>(StringComparer.Ordinal);
        foreach (var r in runs) {
            slack[r.Job] = spec.Jobs[index[r.Job]].Deadline - r.End;
            jobs[r.Job] = new StateRef(r.Late ? PictureState.Rejected : picked);
        }
        foreach (var id in focus)
            if (index.ContainsKey(id) && !(jobs.TryGetValue(id, out var s) && s.State == PictureState.Rejected))
                jobs[id] = new StateRef(PictureState.ElementHighlight);
        return new TimelineFrame(ev, caption, focus, order.Order, slack, jobs) { Ok = ok };
    }

    private static TimelineFrame Blank(string caption) =>
        new(StepEvent.Done, caption, [], [], new SortedDictionary<string, int>(), new SortedDictionary<string, StateRef>()) { Ok = false };
}
```

What it takes beyond the class, as the code stands:

- `"Timeline"` must be added to `VisualizationType` and `Documentation/visualization-types.json`, or `PictureType_Endpoint_Tests` fails for it (§8.1 (b)). Redux_GUI's vendored manifest then needs the value too, and its coverage check a key in `Visualizations.js`, although the GUI never sends `format=frames`: the manifest has no way to mark a type as frames-only yet. That is a gap.
- Nothing else registers it. `/info?interface=JobSeqTimelineVisualization` reports `"visualizationType": "DynamicTable", "pictureType": "Timeline"`; `allInfo` and the Navigation lists include it; `visualizeRun?solver=JobSeqBruteForce` draws it next to the Schedule picture from the same run.
- In Redux_Frontend its card chip reads Unclassified (FE#269) and the problem page draws the job table, since `Timeline` has no renderer; in Redux_GUI it is listed with its `DynamicTable` table. Adding a `TimelineRenderer` (§8.3 (ii)) and a `PICTURE_TYPE_MAP` entry turns it into a drawn, classified picture without any backend change.
- `JobSeqVisualization.Spec` is `internal static`, so this compiles inside the API project. The captions here are placeholders; real ones follow §5.5 and come from the solver's steps.

## 9. Checklist for a pull request

- [ ] The problem uses a picture type from [PICTURE-TYPES.md](PICTURE-TYPES.md), and that type is Approved.
- [ ] The visualization only implements `StepShape` and `Describe` (and `ParseAnswer` if needed), explicitly; no colors, coordinates or highlight loops.
- [ ] Each solver with steps implements `ISolver<T, TPartial>`, records decisions inside the real algorithm, ends with `Done`, and captions every step.
- [ ] Solvers that build answers piece by piece implement `IIncrementalSolver`, and their focus names only the piece under test.
- [ ] A randomized solver implements `IRandomizedSolver` (explicitly), draws every choice from `RandomizedSolvers.Generator(seed)`, repeats with the same seed, fits `rec.Cap` itself, and says "gave up", never "unsatisfiable" (§5.7).
- [ ] `SolverReplay_Tests` passes, and the picture's endpoint tests cover the problem.
- [ ] You looked at the actual JSON from the endpoint (or in the frames viewer), not just the build.
- [ ] A new frames builder sets `step` on every frame drawn from a step, and leaves it null on frames it adds (`FrameStep_Tests` passes).
- [ ] The list format and `/info` are unchanged (the snapshot tests pass).
- [ ] Any new `VisualizationType` value is also in `Documentation/visualization-types.json`.
- [ ] Problem names and descriptions match what the code does.
- [ ] The rbs report on the PR passes (read the report; the check can be green while it fails).

## 10. Questions people ask

**Do I need to know D3 or React?** No. Backend contributors never touch the frontend. Frontend contributors write one renderer per picture type, not per problem.

**My solver doesn't fit any answer shape.** It almost always does once you name what its answer is. If it truly doesn't, say so in an issue; a new answer shape is a design change, not a local fix.

**Can two solvers for the same problem animate differently?** Yes, that's the point. Greedy and Brute Force for Set Cover record different steps on the same picture, so students can see why one overshoots. The user picks the solver, and `visualize` takes it as `solver=`.

**What if my solver's shape doesn't match the visualization's?** The response still has the payload and the solved frame (`step: null`), just no steps. `visualizeRun` returns that solved-only picture too, except for the Search Tree, which it lists as `shape_mismatch`.

**How does a frontend switch pictures without rerunning the solver?** `POST visualizeRun?solver=…` runs it once and returns every picture of the problem built from that run. Frames line up by `step`, not by position.

**My solver is random. How do I make a run repeatable?** Implement `IRandomizedSolver` and take every choice from a seeded generator (§5.7). The endpoints take `seed=` and report the seed they used, so a run can be asked for again.

**What about very large instances?** Steps are capped and long solves are stopped, and both say so in the last caption. Very large drawings are a frontend concern.

**What if my problem's checker disagrees with its description?** Fix the text or the checker first. The picture follows the code, and a picture that contradicts the definition confuses everyone.
