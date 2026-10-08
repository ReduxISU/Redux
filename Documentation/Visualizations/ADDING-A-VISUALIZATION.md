# Adding a visualization and its animation

This guide is for contributors who want a Redux problem to draw a picture, and for solvers and reductions that want to animate one. It explains the model, then walks through DFA end to end.

- To see what each picture type looks like and what it needs, read [PICTURE-TYPES.md](PICTURE-TYPES.md).
- To port the mockups into the frontends, or to plan the backend work, read [README.md](README.md).

> **Status: planned API.** The class and method names below (`AutomatonVisualization<T>`, `IStepSolver`, `StepRecorder`, `StepReplay`, `VisualizationJson`) are the design from backend PRs 2 and 3 (#657, #658). They don't exist in Redux yet. Until they land, use this guide for the *shape* of the work. When they land, the PR that adds them updates this guide to match the real names.

---

## 1. The idea in four sentences

1. A **problem** hands over its data: a graph, a list of clauses, a grid. It never draws anything.
2. A **visualization** picks a *picture type* (Graph, Automaton, Board, …) and says how the problem's data fills it. The picture type's shared base does all the drawing decisions, including every color.
3. A **solver** can record **steps** while it runs: what it tried, what it accepted, what it threw out. Those steps play back as an animation on *any* picture of that problem.
4. A **reduction** records how it builds the target instance, piece by piece, and how an answer to the target maps back to the source.

Nothing in a problem, solver or reduction mentions colors, coordinates or SVG. The frontends own the look; the backend owns the meaning.

```
 Problem ──data──▶ Visualization ──payload──▶  ┐
                  (picks a picture type)       ├──▶ frontend renderer for that picture type
 Solver ──steps──────────────────────frames──▶ ┘
```

## 2. Words used in this guide

| Word | Meaning |
| --- | --- |
| **Picture type** | A family of drawings with one renderer per frontend: Graph, Grouped Graph, Bipartite Graph, Automaton, Layered Graph, Flow Network, Packing, Schedule, Board, Table, Quantum Circuit, Geometry, Search Tree, Reduction, and the rest in [PICTURE-TYPES.md](PICTURE-TYPES.md). |
| **Subtype** | A variant of a graph picture type that changes the layout, such as tour (circle layout) versus subset (force layout) in the Graph type. |
| **Payload** | The JSON a picture type's base sends to the frontend. Each payload has a `type` field. |
| **Answer shape** | What kind of thing the certificate is: a subset of nodes, a set of edges, a split into groups, a tour, an assignment, a set of chosen sets, a set of active states. Steps are typed by answer shape. |
| **Rule** | What the answer must satisfy, such as `vertexCover` or `exactlyOnce`. The base uses it to decide which parts are Covered or Rejected. |
| **Step** | One decision a solver made, with the answer-so-far and a one-sentence caption. |
| **Frame** | One picture in the animation. The base turns each step into a frame. |
| **State** | The shared name for how an element looks in a frame (below). |

### The shared states

Every frame labels elements with these names, never with colors. Each frontend maps them to its theme in one table.

| State | Use it for |
| --- | --- |
| `Background` | Nothing has happened to it yet |
| `ElementHighlight` | What the solver is looking at or trying right now |
| `Solution` | Part of the answer, or a run that ended accepted |
| `Rejected` | Failed: a falsified clause, a rejecting state, an uncovered or doubled element |
| `Untraveled` | Never reached during this run |
| `Covered` | A constraint that is met: an element covered, a set hit, a clause satisfied |
| `Blocked` | Can't be chosen without breaking a rule (exactly-once, independence) |
| `False` | Assigned and false. Still under review; don't rely on it yet |

Some marks are **annotations, not states**: the ×2 badge for "covered twice", order numbers on a tour, the red dashed "missing edge", group colors for color classes and cut sides, and the Garbage node in a DFA. The base adds them; you never set them by hand.

## 3. What you write, and what you don't

| You write | The shared code does |
| --- | --- |
| Which picture type the problem uses | Layout (deterministic: the same instance always looks the same) |
| How the problem's fields map to that type's inputs | Every state and color, from the answer plus the rule |
| In a solver: one `rec.…` call per decision, with a caption | Turning steps into frames, capping long runs, the stepper UI |
| A replay test and a JSON test (one line each) | Responsive layout, dark mode, the key, hover and tap |

If you find yourself writing `color = "green"` or looping over nodes to highlight them, stop: that belongs in the base, and the problem should only be declaring its answer shape and rule.

## 4. Adding a visualization to a problem

### 4.1 Pick the picture type

Find your problem in the table in [PICTURE-TYPES.md](PICTURE-TYPES.md). If it's listed, use that type. If it isn't:

- Does the answer pick nodes, edges, groups or an order on an undirected graph? **Graph**.
- Directed, and direction matters to the answer (orders, cycles, shortest paths)? **Layered Graph**.
- Two kinds of things and memberships between them? **Bipartite Graph**.
- Numbers filling containers up to a line? **Packing**.
- A grid of cells? **Board**.
- A dynamic program or a constraint matrix? **Table**.

If none fits, open an issue under the tracker (#655) with a sketch before writing code. New picture types are mocked up and approved first ([README.md](README.md) §1).

### 4.2 Subclass the base and map your fields

Each picture type has one base class. You override one method that maps your problem onto the base's input record. The base's `visualize`, `SolvedVisualization` and `StepsVisualization` are already written.

```csharp
class CliqueVisualization : GraphVisualization<CLIQUE> {
    public override string visualizationName => "Graph";
    protected override GraphInput Input(CLIQUE p) => new(
        Nodes: p.nodes, Edges: p.edges.Select(e => new GraphEdge(e.source, e.target)),
        K: p.K, Shape: AnswerShape.Subset, Rule: GraphRule.Clique);
}
```

That is the whole visualization. The Graph base knows that, for `Clique`, a pair of chosen nodes without an edge gets a missing-edge mark, chosen nodes are `Solution`, and edges between them are `Covered`.

### 4.3 Register it

1. Add the visualization to the problem's list of visualizations as today.
2. If you added a new **picture type** (rare), add the `VisualizationType` value and the matching entry in `Documentation/visualization-types.json` in the same change; CI compares them. Redux_GUI keeps a vendored copy that needs the same entry.

### 4.4 Test the JSON

```csharp
[Fact] public void Clique_graph_payload() =>
    VisualizationJson.Check<CliqueVisualization>(CLIQUE.Default, "Clique.graph.json");
```

`VisualizationJson.Check` serializes with the API's real settings and compares to a checked-in file. A green build proves nothing about the JSON: `System.Text.Json` with `IncludeFields` leaks public fields and skips default interface members, so always look at the output.

## 5. Adding steps (animation) to a solver

### 5.1 Choose the step type

Steps are typed by **answer shape**, not by picture type. A subset solver records the subset so far whether the frontend draws it as a Graph, a Search Tree or a Table. So one recording drives every picture of that problem.

```csharp
record SolverStep<TCert>(TCert Partial, StepEvent Event, string[] Focus, string Caption);
enum StepEvent { Try, Accept, Reject, Backtrack, Done }
```

| Event | Means | Typical frame |
| --- | --- | --- |
| `Try` | Considering a choice | The choice is `ElementHighlight` |
| `Accept` | Kept the choice | It joins the answer-so-far |
| `Reject` | Threw the choice out | It flashes `Rejected`, then leaves |
| `Backtrack` | Undid an earlier choice | The undone part leaves the answer |
| `Done` | Finished, with the final verdict | Answer `Solution`, or the failure `Rejected` |

`Focus` names what the solver is looking at (node ids, a clause id, a cell). Use the same ids the payload uses, so the frontend can highlight them.

### 5.2 Record inside the real algorithm

Put the algorithm in a private `Run` method that takes a recorder. `solve` passes a recorder that ignores everything, so solving costs nothing extra; `GetSteps` passes a real one.

```csharp
class CliqueBruteForce : ISolver<CLIQUE>, IStepSolver<CLIQUE, NodeSet> {
    public string solve(CLIQUE p) => Run(p, StepRecorder<NodeSet>.Ignore);
    public IReadOnlyList<SolverStep<NodeSet>> GetSteps(CLIQUE p) {
        var rec = new StepRecorder<NodeSet>(limit: 150);
        Run(p, rec);
        return rec.Steps;
    }
    string Run(CLIQUE p, StepRecorder<NodeSet> rec) { … rec.Try(set, focus, "Try {a, b, c}."); … }
}
```

Never write a second copy of the algorithm just to produce steps. If the steps come from a copy, they can drift from what `solve` really does, and the animation lies.

### 5.3 What to record

- **Record decisions, not loop iterations.** Choose, reject, back out, finish. A step per inner-loop comparison makes an animation nobody can follow.
- **Long runs get capped.** The recorder keeps the first `limit` steps plus the final `Done`, and the last frame says how many were skipped ("…and 3,407 more tries"). The mockups cap at 120 to 250.
- **Always end with `Done`**, even when no answer exists. The last step's verdict must match `solve`.
- **Record the real tie-breaking.** If the C# solver uses a priority queue, the steps follow its order, even when it looks surprising. The animation is there to show what the solver does, not what a textbook does.

### 5.4 Writing captions

The caption is the only text that explains a step. Write it for a student watching the animation.

| Do | Don't |
| --- | --- |
| "Read **b**: 1 → 3." | "Transition processed." |
| "Switch to x₄ = F. C2 has no true literal left, so undo." | "Backtracking (depth 4)." |
| "Pick S4 = {4, 5}: it covers 2 new elements." | "i = 3, j = 1, best = 2" |
| One sentence, two at most | A paragraph |
| The problem's own words (clause, set, tour) | Internal names (`nodePath`, `memo`) |

### 5.5 Test the replay

```csharp
[Fact] public void Clique_brute_force_replays() =>
    StepReplay.Check(new CliqueBruteForce(), CLIQUE.Default);
```

`StepReplay.Check` runs `GetSteps` and `solve`, then checks that the last step is `Done`, that its `Partial` equals `solve`'s answer, and that every step has a caption. Add one line per solver.

## 6. Reductions

A reduction already builds the target instance. To animate it, it also records:

1. **Gadgets.** Each gadget has a `kind` from the shared list (`element`, `group`, `edgeRule`, `palette`, `orGadget`, `bound`), the source ids it came from, and the target ids it made. Ids must match the ids each side's own picture uses; that is what makes hover-linking work across the two panes.
2. **Build steps.** One per gadget, in construction order: which nodes and edges it added, which pairs a rule deliberately left unjoined, and a caption in both problems' words ("Clause C2 becomes cluster 2: one node per literal.").
3. **A backward map.** Given the target's answer, produce the source's answer, piece by piece, with a caption per piece ("x₂ is in the clique, so set x₂ = T"). Today `mapSolutions` maps forward only.

The Reduction view then plays three phases: **Build**, **Solve** (the target's solver, any of them), and **Map back**. Both panes reuse the source and target problems' own picture types; a reduction never draws anything itself. See `reduction.js` in the mockups for complete examples.

## 7. Worked example: DFA

This is the full set of changes for DFA Acceptance once PRs 2 and 3 land.

![DFA in the Automaton picture type, mid-run](images/auto--dfa-ends-in-ab-input-abaab.png)

### 7.1 What exists today

- `Problems/P/P_DFA/DFA_Class.cs` parses `((N,A,E,S,F),I)` into `nodes`, `alphabet`, `edges` (`DFAEdge(From, Symbol, To)`), `startState`, `acceptStates` and `inputString`. Nothing here changes.
- `Visualizations/DFAVisualization.cs` uses the `GraphLaTeX` type (TikZ) and colors nodes `"green"` or `"white"` by hand, in three separate loops.
- `Solvers/DFASolver.cs`'s `GetSteps` returns the visited state names as a `List<Object>` of strings. The visualization casts them back and hopes.

### 7.2 The visualization: one mapping

```csharp
class DFAVisualization : AutomatonVisualization<DFA> {
    public override string visualizationName => "Automaton Graph";
    protected override Automaton Machine(DFA p) => new(
        Kind: AutomatonKind.Deterministic,
        States: p.nodes,
        Alphabet: p.alphabet.Select(c => c.ToString()),
        Transitions: p.edges.Select(e => new Transition(e.From, e.Symbol.ToString(), e.To)),
        Start: p.startState, Accept: p.acceptStates, Input: p.inputString);
}
```

The Automaton base does the rest:

- lays the states out left to right in breadth-first order from the start state;
- merges parallel transitions into one arrow with a combined label (`a,b`);
- draws the start arrow, double rings on accept states, and the input tape;
- adds the isolated red **Garbage** state when a DFA is missing a transition, so a run that falls off the machine has somewhere visible to go.

### 7.3 The solver: record each symbol read

The Automaton type's answer shape is *active states*. Its step type is shared by DFA and NFA:

```csharp
record RunState(int Read, IReadOnlySet<string> Active, IReadOnlyList<Transition> Taken);
```

```csharp
class DFASolver : ISolver<DFA>, IStepSolver<DFA, RunState> {
    public string solve(DFA p) => Run(p, StepRecorder<RunState>.Ignore);

    public IReadOnlyList<SolverStep<RunState>> GetSteps(DFA p) {
        var rec = new StepRecorder<RunState>();
        Run(p, rec);
        return rec.Steps;
    }

    string Run(DFA p, StepRecorder<RunState> rec) {
        string at = p.startState;
        rec.Try(new(0, Set(at), []), [at], $"Start in {at}.");
        for (int i = 0; i < p.inputString.Length; i++) {
            char c = p.inputString[i];
            var edge = p.edges.FirstOrDefault(e => e.From == at && e.Symbol == c);
            if (edge is null) {
                rec.Reject(new(i + 1, Set(Garbage), []), [at],
                    $"No move from {at} on {c}, so the run falls into Garbage.");
                return rec.Done(new(p.inputString.Length, Set(Garbage), []), accepted: false,
                    "Rejected: the input fell off the machine.");
            }
            var t = new Transition(edge.From, c.ToString(), edge.To);
            rec.Try(new(i + 1, Set(edge.To), [t]), [edge.To], $"Read {c}: {at} → {edge.To}.");
            at = edge.To;
        }
        bool ok = p.acceptStates.Contains(at);
        return rec.Done(new(p.inputString.Length, Set(at), []), accepted: ok,
            ok ? $"Accepted: the input ends in {at}, an accept state."
               : $"Rejected: the input ends in {at}, which isn't an accept state.");
    }
}
```

What the frontend draws from those steps, without any DFA-specific code:

| Step | Picture |
| --- | --- |
| `Try` "Read b: 1 → 3." | State 3 and the arrow 1 → 3 are `ElementHighlight`; the tape shows two symbols read |
| `Reject` into Garbage | Garbage turns `Rejected`; the missing move is named in the caption |
| `Done`, accepted | The final state is `Solution`; states never reached are `Untraveled` |
| `Done`, rejected | The final state is `Rejected` |

The same steps also drive the DFA **Trace Table** (one row per step), so `GetTableSteps` and its row classes go away.

NFA uses the same `RunState`, with `Active` holding every live state at once. That gives the approved default view, all branches together; the "follow one path" dropdown is computed by the frontend from the same steps.

### 7.4 The tests

```csharp
[Fact] public void DFA_automaton_payload() =>
    VisualizationJson.Check<DFAVisualization>(new DFA(), "DFA.automaton.json");

[Fact] public void DFA_solver_replays() => StepReplay.Check(new DFASolver(), new DFA());

[Fact] public void DFA_missing_move_goes_to_garbage() =>
    StepReplay.Check(new DFASolver(), new DFA("(({1,2},{a,b},{(1,a,2)},1,{2}),ab)"),
        last: s => s.Partial.Active.Contains("Garbage"));
```

### 7.5 What got deleted

- The three hand-written coloring loops in `DFAVisualization`.
- The `"green"` / `"white"` strings.
- The `List<Object>` cast.
- The TikZ dependency for this problem (TikZ leaves Redux once every visualization is ported).

## 8. Checklist for a pull request

- [ ] The problem uses a picture type from [PICTURE-TYPES.md](PICTURE-TYPES.md), and that type is Approved.
- [ ] The visualization only maps fields; no colors, coordinates or highlight loops.
- [ ] Each solver with steps records decisions through a recorder inside the real algorithm, ends with `Done`, and captions every step.
- [ ] `StepReplay.Check` for every solver you touched, and `VisualizationJson.Check` for every visualization.
- [ ] You looked at the actual JSON from the endpoint, not just the build.
- [ ] Any new `VisualizationType` value is also in `Documentation/visualization-types.json`.
- [ ] Problem names and descriptions match what the code does.
- [ ] The rbs report on the PR passes (read the report; the check can be green while it fails).

## 9. Questions people ask

**Do I need to know D3 or React?** No. Backend contributors never touch the frontend. Frontend contributors write one renderer per picture type, not per problem.

**My solver doesn't fit any step type.** It almost always does once you name its answer shape. If it truly doesn't, say so in an issue; a new answer shape is a design change, not a local fix.

**Can two solvers for the same problem animate differently?** Yes, that's the point. Greedy and Brute Force for Set Cover record different steps on the same picture, so students can see why one overshoots.

**What about very large instances?** The frontend switches large Graph and Layered Graph instances to a WebGL renderer (Sigma.js) on its own, using the same states. You don't do anything.

**What if my problem's checker disagrees with its description?** Fix the text or the checker first. The picture follows the code, and a picture that contradicts the definition confuses everyone.
