# Adding a visualization and its animation

This guide is for contributors who want a Redux problem to draw a picture, and for solvers and reductions that want to animate one. It explains the model, then walks through DFA end to end.

- To see what each picture type looks like and what it needs, read [PICTURE-TYPES.md](PICTURE-TYPES.md).
- To port the mockups into the frontends, or to see what the backend has and what is still planned, read [README.md](README.md).

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
- A CNF formula? **Grouped Graph** (and, once approved, **Formula** as a second view drawn from the same spec).
- Numbers filling containers up to a line? **Packing**.
- A grid of cells? **Board**.
- A dynamic program, a constraint matrix or a trace? **Table**.

If none fits, open an issue under the tracker (#655) with a sketch before writing code. New picture types are mocked up and approved first ([README.md](README.md) §1).

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

1. Add the visualization to the problem's list of visualizations as today. Nothing else: the visualize endpoint finds the frames hook, and `/info` reports `pictureType` by itself.
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

## 8. Checklist for a pull request

- [ ] The problem uses a picture type from [PICTURE-TYPES.md](PICTURE-TYPES.md), and that type is Approved.
- [ ] The visualization only implements `StepShape` and `Describe` (and `ParseAnswer` if needed), explicitly; no colors, coordinates or highlight loops.
- [ ] Each solver with steps implements `ISolver<T, TPartial>`, records decisions inside the real algorithm, ends with `Done`, and captions every step.
- [ ] Solvers that build answers piece by piece implement `IIncrementalSolver`, and their focus names only the piece under test.
- [ ] `SolverReplay_Tests` passes, and the picture's endpoint tests cover the problem.
- [ ] You looked at the actual JSON from the endpoint (or in the frames viewer), not just the build.
- [ ] A new frames builder sets `step` on every frame drawn from a step, and leaves it null on frames it adds (`FrameStep_Tests` passes).
- [ ] The list format and `/info` are unchanged (the snapshot tests pass).
- [ ] Any new `VisualizationType` value is also in `Documentation/visualization-types.json`.
- [ ] Problem names and descriptions match what the code does.
- [ ] The rbs report on the PR passes (read the report; the check can be green while it fails).

## 9. Questions people ask

**Do I need to know D3 or React?** No. Backend contributors never touch the frontend. Frontend contributors write one renderer per picture type, not per problem.

**My solver doesn't fit any answer shape.** It almost always does once you name what its answer is. If it truly doesn't, say so in an issue; a new answer shape is a design change, not a local fix.

**Can two solvers for the same problem animate differently?** Yes, that's the point. Greedy and Brute Force for Set Cover record different steps on the same picture, so students can see why one overshoots. The user picks the solver, and `visualize` takes it as `solver=`.

**What if my solver's shape doesn't match the visualization's?** The response still has the payload and the solved frame (`step: null`), just no steps. `visualizeRun` returns that solved-only picture too, except for the Search Tree, which it lists as `shape_mismatch`.

**How does a frontend switch pictures without rerunning the solver?** `POST visualizeRun?solver=…` runs it once and returns every picture of the problem built from that run. Frames line up by `step`, not by position.

**What about very large instances?** Steps are capped and long solves are stopped, and both say so in the last caption. Very large drawings are a frontend concern.

**What if my problem's checker disagrees with its description?** Fix the text or the checker first. The picture follows the code, and a picture that contradicts the definition confuses everyone.
