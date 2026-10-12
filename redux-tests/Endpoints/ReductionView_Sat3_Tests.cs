using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Reduction View (#692) of the reductions out of 3SAT and SAT (#693), on the real /ProblemProvider/reductionView output.
// Their targets are drawn with four different pictures (Graph, Table, Tripartite Matching, Grouped Graph), so these tests
// read each payload by its kind: what a picture calls its pieces, and which of them a build step reveals.
public class ReductionView_Sat3_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_Sat3_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    public const string Coloring = "KarpReduceGRAPHCOLORING", Ilp = "KarpIntProgStandard", Matching = "GareyJohnson", Sat3 = "KarpSATToSAT3";

    // Each reduction's target picture type. Every source is a CNF formula, drawn as the Grouped Graph.
    public static readonly Dictionary<string, string> TargetPicture = new(StringComparer.OrdinalIgnoreCase) {
        [Coloring] = "Graph",
        [Ilp] = "Table",
        [Matching] = "TripartiteMatching",
        [Sat3] = "GroupedGraph",
    };

    // Instances small enough for the targets' brute-force solvers to finish (3-coloring and 3-dimensional matching grow fast:
    // the default formula's targets have 27 nodes and 135 triples), with and without an answer, and with the awkward cases:
    // a repeated literal, a variable written both ways in one clause, names that are prefixes of each other, short clauses
    // and long ones.
    public static IEnumerable<(string Reduction, string Instance)> Cases() {
        foreach (string f in new[] { "(x1)", "(x1 | !x2)", "(x1 | x1 | x2)", "(!a)" })
            yield return (Coloring, f);
        foreach (string f in new[] { DefaultInstance(Ilp), "(x1)", "(x1) & (!x1)", "(x1 | !x1 | x2)", "(x10 | !x1) & (x1 | y)", "(a_1 | !b) & (b) & (!a_1 | b)", "(x1 | x1 | !x2) & (x2 | x3) & (!x3)" })
            yield return (Ilp, f);
        foreach (string f in new[] { "(x1)", "(x1) & (!x1)", "(x1 | !x2) & (x2)", "(x1 | x1)", "(!y)" })
            yield return (Matching, f);
        foreach (string f in new[] { DefaultInstance(Sat3), "(x1 | !x2 | x3 | x4 | !x5) & (x2 | x3) & (!x1)", "(a | b | c | d)", "(x1)", "(x1) & (!x1)",
                     "(x1 | x2 | x3 | x4 | x5 | x6 | x7)", "(x1 | !x1 | x2 | x2) & (!x2)" })
            yield return (Sat3, f);
    }

    public static TheoryData<string, string> CaseData() {
        var data = new TheoryData<string, string>();
        foreach (var (r, i) in Cases()) data.Add(r, i);
        return data;
    }

    internal static string DefaultInstance(string reduction) =>
        ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!).reductionFrom.defaultInstance;

    internal static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/" + path, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> View(string reduction, string instance, string? solver = null) {
        var (status, body) = await Post($"reductionView?reduction={reduction}" + (solver == null ? "" : $"&solver={solver}"), instance);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement;
    }

    private static List<JsonElement> Phase(JsonElement view, string phase) =>
        view.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == phase).ToList();

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static IEnumerable<string> Ids(JsonElement payload, string list) =>
        payload.TryGetProperty(list, out var items) ? items.EnumerateArray().Select(i => i.GetProperty("id").GetString()!) : [];

    // The pieces of a picture a build step reveals, by the picture's kind (see BuildDelta): what stands on its own (nodes) and
    // what is drawn between two of those (edges).
    internal static (List<string> Nodes, List<string> Edges) Pieces(JsonElement payload) => payload.GetProperty("kind").GetString() switch {
        "graph" => (Ids(payload, "nodes").ToList(), Ids(payload, "edges").ToList()),
        "groupedGraph" => (Ids(payload, "groups").Concat(Ids(payload, "nodes")).Concat(Ids(payload, "variables")).ToList(),
            Ids(payload, "edges").Concat(Ids(payload, "clauseEdges")).ToList()),
        "tripartite" => (Ids(payload, "x").Concat(Ids(payload, "y")).Concat(Ids(payload, "z")).Concat(Ids(payload, "triples")).ToList(), []),
        "table" => (Ids(payload, "rows").Concat(Ids(payload, "columns")).ToList(), []),
        var other => throw new NotSupportedException(other),
    };

    // Every id a picture can name: its pieces, and a table's cells.
    internal static HashSet<string> PictureIds(JsonElement payload) {
        var (nodes, edges) = Pieces(payload);
        var ids = nodes.Concat(edges).ToHashSet(StringComparer.Ordinal);
        if (payload.GetProperty("kind").GetString() == "table")
            foreach (string row in Ids(payload, "rows"))
                foreach (string column in Ids(payload, "columns"))
                    ids.Add(row + column);
        return ids;
    }

    // Whether an id is on screen once the first nodes and edges of the build order are: a table cell needs its row and column.
    private static bool Shown(string id, HashSet<string> visible) =>
        visible.Contains(id) || (API.Interfaces.Table.TableFrames.ParseCellId(id) is (int r, int c) && visible.Contains($"r{r}") && visible.Contains($"c{c}"));

    private static bool Verifies(IProblem problem, string instance, string answer) {
        try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
    }

    // The assignment a CNF pane shows (variable id to value), as certificate text with the variables' names.
    private static string ShownAssignment(JsonElement view, JsonElement frame) {
        var names = view.GetProperty("source").GetProperty("picture").GetProperty("payload").GetProperty("variables").EnumerateArray()
            .ToDictionary(v => v.GetProperty("id").GetString()!, v => v.GetProperty("label").GetString()!);
        var assignment = frame.GetProperty("source").GetProperty("assignment").EnumerateObject();
        return "(" + string.Join(",", assignment.Select(p => $"{names[p.Name]}:{(p.Value.GetBoolean() ? "True" : "False")}")) + ")";
    }

    // ---- Shape and ids ----

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Shape_BothPicturesTheGadgetsAndThreePhases(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        Assert.Equal("Reduction", view.GetProperty("type").GetString());
        Assert.Equal(red.reductionName, view.GetProperty("reductionName").GetString());
        Assert.Equal("GroupedGraph", view.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal(TargetPicture[reduction], view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal(red.reductionTo.instance, view.GetProperty("target").GetProperty("instance").GetString());
        var gadgets = view.GetProperty("gadgets").EnumerateArray().ToList();
        Assert.Equal(red.gadgets.Count, gadgets.Count);
        Assert.NotEmpty(gadgets);
        Assert.All(gadgets, g => Assert.True(Enum.TryParse<API.Interfaces.JSON_Objects.GadgetKind>(g.GetProperty("gadgetKind").GetString(), true, out _)));

        var phases = view.GetProperty("frames").EnumerateArray().Select(f => f.GetProperty("phase").GetString()!).ToList();
        Assert.Equal(["build", "solve", "map"], phases.Distinct());
        // One Done in the whole view, its last frame, and the only frame with ok (#840).
        var all = view.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(all[^1], Assert.Single(all, f => f.GetProperty("event").GetString() == "Done"));
        Assert.Equal("map", all[^1].GetProperty("phase").GetString());
        Assert.DoesNotContain(all.SkipLast(1), f => f.TryGetProperty("ok", out _));
        Assert.Equal("Accept", Phase(view, "build")[^1].GetProperty("event").GetString());
        Assert.All(view.GetProperty("frames").EnumerateArray(), f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString())));
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task EveryGadgetAndFocusId_IsAnIdOfItsPanesPicture(string reduction, string instance) {
        var view = await View(reduction, instance);
        var sourceIds = PictureIds(view.GetProperty("source").GetProperty("picture").GetProperty("payload"));
        var targetIds = PictureIds(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var g in view.GetProperty("gadgets").EnumerateArray()) {
            Assert.All(Strings(g.GetProperty("sourceIds")), id => Assert.Contains(id, sourceIds));
            Assert.All(Strings(g.GetProperty("targetIds")), id => Assert.Contains(id, targetIds));
        }
        foreach (var f in view.GetProperty("frames").EnumerateArray()) {
            Assert.All(Strings(f.GetProperty("sourceFocus")), id => Assert.Contains(id, sourceIds));
            Assert.All(Strings(f.GetProperty("targetFocus")), id => Assert.Contains(id, targetIds));
            if (f.TryGetProperty("gadgets", out var used))
                Assert.All(used.EnumerateArray(), i => Assert.InRange(i.GetInt32(), 0, view.GetProperty("gadgets").GetArrayLength() - 1));
        }
    }

    // Every gadget is built by exactly one build step, and every gadget names something on both sides except the fixed helpers
    // (the palette, the garbage collection) and the bound, which come from no single piece of the formula.
    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_EveryGadgetIsBuiltByOneStep(string reduction, string instance) {
        var view = await View(reduction, instance);
        var used = Phase(view, "build").Where(f => f.TryGetProperty("gadgets", out _))
            .SelectMany(f => f.GetProperty("gadgets").EnumerateArray().Select(i => i.GetInt32())).ToList();
        Assert.Equal(used.Count, used.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, view.GetProperty("gadgets").GetArrayLength()), used.Order());
        foreach (var g in view.GetProperty("gadgets").EnumerateArray()) {
            string kind = g.GetProperty("gadgetKind").GetString()!;
            if (kind is not ("palette" or "bound")) Assert.NotEmpty(g.GetProperty("sourceIds").EnumerateArray());
            if (kind != "bound") Assert.NotEmpty(g.GetProperty("targetIds").EnumerateArray());
        }
    }

    // ---- Build: replaying the steps gives exactly the target ----

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_ReplayEqualsTheTarget(string reduction, string instance) {
        var view = await View(reduction, instance);
        var target = view.GetProperty("target");
        var (nodes, edges) = Pieces(target.GetProperty("picture").GetProperty("payload"));
        var order = target.GetProperty("buildOrder");
        var nodeOrder = Strings(order.GetProperty("nodes"));
        var edgeOrder = Strings(order.GetProperty("edges"));
        // buildOrder names every piece of the target picture exactly once.
        Assert.Equal(nodes.Order(StringComparer.Ordinal), nodeOrder.Order(StringComparer.Ordinal));
        Assert.Equal(edges.Order(StringComparer.Ordinal), edgeOrder.Order(StringComparer.Ordinal));

        // The frames reveal it monotonically, the frame before Done shows all of it, and each frame's focus is on screen.
        var build = Phase(view, "build");
        int shownNodes = 0, shownEdges = 0;
        foreach (var frame in build) {
            int n = frame.GetProperty("target").GetProperty("builtNodes").GetInt32(), e = frame.GetProperty("target").GetProperty("builtEdges").GetInt32();
            Assert.True(n >= shownNodes && e >= shownEdges, "the target only grows");
            (shownNodes, shownEdges) = (n, e);
            var visible = nodeOrder.Take(n).Concat(edgeOrder.Take(e)).ToHashSet(StringComparer.Ordinal);
            Assert.All(Strings(frame.GetProperty("targetFocus")), id => Assert.True(Shown(id, visible), $"{id} is focused before it is built"));
        }
        Assert.Equal(nodes.Count, build[^2].GetProperty("target").GetProperty("builtNodes").GetInt32());
        Assert.Equal(edges.Count, build[^2].GetProperty("target").GetProperty("builtEdges").GetInt32());
    }

    // The replayed steps are the reduction's own, and the target they build is its real target, piece for piece.
    [Theory]
    [MemberData(nameof(CaseData))]
    public void Build_ReplayedPiecesAreTheRealTargets(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        var steps = red.BuildRun(true)!.Steps;
        var nodes = steps.SelectMany(s => s.Partial.Nodes).ToList();
        var edges = steps.SelectMany(s => s.Partial.Edges).ToList();
        Assert.Equal(nodes.Count, nodes.Distinct().Count());
        Assert.Equal(edges.Count, edges.Select(e => e.Id).Distinct().Count());
        switch (red.reductionTo) {
            case API.Problems.NPComplete.NPC_GRAPHCOLORING.GRAPHCOLORING g:
                Assert.Equal(g.nodes.Order(StringComparer.Ordinal), nodes.Order(StringComparer.Ordinal));
                Assert.Equal(g.edges.Select(e => API.Interfaces.Graphs.GraphSubsetFrames.EdgeId(e.Key, e.Value)).Distinct().Order(StringComparer.Ordinal),
                    edges.Select(e => e.Id).Order(StringComparer.Ordinal));
                break;
            case API.Problems.NPComplete.NPC_DM3.DM3 m:
                Assert.Equal(m.X.Count + m.Y.Count + m.Z.Count + m.M.Count, nodes.Count);
                Assert.Equal(m.M.Count, nodes.Count(n => n.StartsWith('T')));
                Assert.Empty(edges);
                break;
            case API.Problems.NPComplete.NPC_INTPROGRAMMING01.INTPROGRAMMING01 p:
                // Row 0 is the assignment, then one row per constraint; a column per variable, then Total, Bound and Holds.
                Assert.Equal(1 + p.C.Count, nodes.Count(n => n.StartsWith('r')));
                Assert.Equal(p.C[0].Count + 3, nodes.Count(n => n.StartsWith('c')));
                Assert.Empty(edges);
                break;
            case API.Problems.NPComplete.NPC_SAT3.SAT3 s:
                var payload = API.Interfaces.Graphs.GroupedGraphFrames.Payload(API.Interfaces.Graphs.GroupedGraphSpec.FromLiterals(new API.Problems.NPComplete.NPC_SAT3.SAT3(s.instance).clauses));
                Assert.Equal(payload.Groups.Select(g => g.Id).Concat(payload.Nodes.Select(n => n.Id)).Concat(payload.Variables.Select(v => v.Id)).Order(StringComparer.Ordinal),
                    nodes.Order(StringComparer.Ordinal));
                Assert.Equal(payload.Edges.Concat(payload.ClauseEdges).Select(e => e.Id).Order(StringComparer.Ordinal), edges.Select(e => e.Id).Order(StringComparer.Ordinal));
                // Every edge gives its two ends, as the picture does.
                var ends = payload.Edges.Concat(payload.ClauseEdges).ToDictionary(e => e.Id, e => (e.A, e.B));
                Assert.All(edges, e => Assert.Equal(ends[e.Id], (e.A, e.B)));
                break;
            default:
                throw new NotSupportedException(red.reductionTo.GetType().Name);
        }
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_PanesOnlyHighlight(string reduction, string instance) {
        var view = await View(reduction, instance);
        foreach (var frame in Phase(view, "build"))
            foreach (string pane in new[] { "source", "target" })
                Assert.All(ReductionView_EndpointStates(frame.GetProperty(pane)), s => Assert.Equal("ElementHighlight", s.State));
    }

    internal static IEnumerable<(string Id, string State)> ReductionView_EndpointStates(JsonElement pane) =>
        pane.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Object)
            .SelectMany(p => p.Value.EnumerateObject())
            .Where(e => e.Value.ValueKind == JsonValueKind.Object && e.Value.TryGetProperty("state", out _))
            .Select(e => (e.Name, e.Value.GetProperty("state").GetString()!));

    // ---- Stable gadgets ----

    [Theory]
    [MemberData(nameof(CaseData))]
    public void Gadgets_AreTheSame_AcrossBuilds_AndAfterReduceAgain(string reduction, string instance) {
        IReduction first = Build(reduction, instance), second = Build(reduction, instance);
        string gadgets = JsonSerializer.Serialize(first.gadgets);
        Assert.Equal(gadgets, JsonSerializer.Serialize(second.gadgets));
        first.reduce();
        Assert.Equal(gadgets, JsonSerializer.Serialize(first.gadgets));
        Assert.Equal(gadgets, JsonSerializer.Serialize(first.BuildRun(true)!.Gadgets));
        Assert.Equal(second.reductionTo.instance, first.reduce().instance);
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public void RecordedReduction_BuildsOnce_TheSameTargetGadgetsAndSteps(string reduction, string instance) {
        var rec = new StepRecorder<BuildDelta>(cap: int.MaxValue);
        var once = (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance, rec)!;
        IReduction plain = Build(reduction, instance);
        Assert.Equal(plain.reductionTo.instance, once.reductionTo.instance);
        Assert.Equal(JsonSerializer.Serialize(plain.gadgets), JsonSerializer.Serialize(once.gadgets));
        Assert.Equal(JsonSerializer.Serialize(plain.BuildRun(true)!.Steps), JsonSerializer.Serialize(ConstructionSteps.Limit(rec.Steps)));
    }

    // SAT to 3SAT's variable gadgets (#693): a variable of the SAT formula is the same variable of the 3SAT formula, and each new
    // variable of a split comes from the clause it splits. Redux_GUI draws literals, not variables, so their legacy ids are the
    // variable's occurrences on each side.
    [Theory]
    [InlineData("(x1 | !x2 | x3 | x4 | !x5) & (x2 | x3) & (!x1)")]
    [InlineData("(x1 | x2 | x3 | x4 | x5 | x6 | x7)")]
    public void KarpSatToSat3_VariableGadgets_NameTheSameVariable_AndTheGuiSeesItsOccurrences(string instance) {
        IReduction red = Build(Sat3, instance);
        var sat = ((API.Problems.NPComplete.NPC_SAT.SAT)red.reductionFrom).clauses;
        var sat3 = ((API.Problems.NPComplete.NPC_SAT3.SAT3)red.reductionTo).clauses;
        List<string> Occurrences(List<List<string>> clauses, string variable) =>
            clauses.SelectMany((c, i) => c.Select((l, j) => (l, id: $"{i}-{j}"))).Where(p => p.l.TrimStart('!') == variable).Select(p => p.id).ToList();
        var variables = sat.SelectMany(c => c).Select(l => l.TrimStart('!')).Distinct().ToList();
        foreach (string v in variables) {
            var g = Assert.Single(red.gadgets, g => g.sourceIds.SequenceEqual(["v:" + v]));
            Assert.Equal(["v:" + v], g.targetIds);
            Assert.Equal(Occurrences(sat, v), g.reductionFromIds);
            Assert.Equal(Occurrences(sat3, v), g.reductionToIds);
        }
        var helpers = sat3.SelectMany(c => c).Select(l => l.TrimStart('!')).Distinct().Except(variables).ToList();
        Assert.NotEmpty(helpers);
        foreach (string h in helpers) {
            var g = Assert.Single(red.gadgets, g => g.targetIds.SequenceEqual(["v:" + h]));
            Assert.Matches(@"^c\d+$", Assert.Single(g.sourceIds));
            Assert.Equal(Occurrences(sat3, h), g.reductionToIds);
        }
    }

    // The 0-1 program's table highlights the cell the Reduction View reads (#693); before, it only knew row focus.
    [Fact]
    public async Task Ilp_MapSteps_HighlightTheVariablesCell() {
        var view = await View(Ilp, "(x1 | !x2)");
        var map = Phase(view, "map");
        Assert.Equal(["r0c0", "r0c1"], map.Take(2).Select(f => Assert.Single(Strings(f.GetProperty("targetFocus")))));
        foreach (var frame in map.Take(2)) {
            string cell = Strings(frame.GetProperty("targetFocus"))[0];
            Assert.Equal("ElementHighlight", frame.GetProperty("target").GetProperty("cells").GetProperty(cell).GetProperty("state").GetString());
        }
    }

    // Example captions, in both problems' words.
    [Theory]
    [InlineData(Coloring, "(x1 | !x2)", "build", "Variable x1 only appears as x1: one node x1, joined to B, so it takes the true color or the false color.")]
    [InlineData(Coloring, "(x1 | !x2)", "map", "!x2 has T's color, so !x2 is true: x2 = False.")]
    [InlineData(Ilp, "(x1 | !x2 | x3) & (x2)", "build", "Clause C1 (x1 | !x2 | x3) becomes row 1: −x1 + x2 − x3 ≤ 0. Each plain literal counts −1 and each negated one +1, and the bound is the number of negated literals minus 1, so the row holds exactly when one of its literals is true.")]
    [InlineData(Ilp, "(x1 | !x2 | x3) & (x2)", "map", "x2 = 1, so x2 = True.")]
    [InlineData(Matching, "(x1 | !x2) & (x2)", "map", "T4 (x_x2_0, y_x2_1, z_!x2_0) is in the matching: x2's wheel takes its z_!x2 markers and leaves the z_x2 ones for the clauses, so x2 = True.")]
    [InlineData(Sat3, "(a | b | c | d)", "build", "Clause C1 (a | b | c | d) has 4 literals, so it is split into a chain of 2 clauses joined by the new variable x1: C1 (a | b | !x1), C2 (c | d | x1). Each new variable carries \"a literal further along is true\" from one clause of the chain to the next.")]
    [InlineData(Sat3, "(a | b | c | d) & (!a) & (!b) & (!c)", "map", "d = True in the 3SAT answer, and it is the same variable in the SAT formula: d = True.")]
    public async Task Captions_SayWhatHappened(string reduction, string instance, string phase, string caption) {
        var view = await View(reduction, instance);
        Assert.Contains(caption, Phase(view, phase).Select(f => f.GetProperty("caption").GetString()));
    }

    // ---- Solve and map ----

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class Timing : IClassFixture<AppFactory> {
        private readonly ReductionView_Sat3_Tests _outer;

        public Timing(AppFactory factory) {
            _outer = new ReductionView_Sat3_Tests(factory);
        }

        // 3SAT to Graph Coloring and to 3-Dimensional Matching name their backtracking solvers for the view (#814): on the default
        // formula, which the brute forces cannot finish in the 10 s limit, the view solves the target and maps the answer back to
        // an assignment 3SAT's verifier accepts. 5 s leaves room for a busy test run; alone each takes well under a second.
        [Theory]
        [InlineData(Coloring, "Graph Coloring Backtracking")]
        [InlineData(Matching, "3-Dimensional Matching Backtracking")]
        public async Task DefaultFormula_SolvesWithTheBacktracking_AndMapsBack(string reduction, string solverName) {
            string instance = DefaultInstance(reduction);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var view = await _outer.View(reduction, instance);
            clock.Stop();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"{reduction}: the view took {clock.Elapsed}");
            Assert.Equal(solverName, view.GetProperty("solver").GetString());
            Assert.True(ReductionView_Endpoint_Tests.SolvedOk(Phase(view, "solve")), Phase(view, "solve")[^1].GetProperty("caption").GetString());
            var map = Phase(view, "map");
            Assert.True(map[^1].GetProperty("ok").GetBoolean(), map[^1].GetProperty("caption").GetString());
            IReduction red = Build(reduction, instance);
            string assignment = ShownAssignment(view, map[^1]);
            Assert.True(Verifies(red.reductionFrom, instance, assignment), assignment);
        }
    }

    // The solver a reduction names for its view solves the target the view draws (the same check as ?solver=), and only
    // these two name one.
    [Fact]
    public void ReductionViewSolver_SolvesTheTarget_AndOnlyTheSlowTargetsNameOne() {
        foreach (var (name, type) in ProblemProvider.Reductions) {
            IReduction red;
            try { red = (IReduction)Activator.CreateInstance(type)!; } catch { continue; }
            ISolver? solver = red.ReductionViewSolver();
            if (name is "karpreducegraphcoloring" or "gareyjohnson") {
                Assert.NotNull(solver);
                Assert.Contains(solver!.GetType().GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)
                    && i.GetGenericArguments()[0] == red.reductionTo.GetType());
                Assert.True(solver is IIncrementalSolver);
            } else {
                Assert.Null(solver);
            }
        }
    }

    // Naming the brute force still runs it: the default only applies when no solver is named.
    [Theory]
    [InlineData(Coloring, "GraphColoringBruteForce", "Graph Coloring Brute Force")]
    [InlineData(Matching, "ThreeDimensionalMatchingBruteForce", "3-Dimensional Matching Brute Force")]
    public async Task NamedSolver_OverridesTheReductionsViewSolver(string reduction, string solver, string solverName) {
        var view = await View(reduction, "(x1 | !x2)", solver);
        Assert.Equal(solverName, view.GetProperty("solver").GetString());
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Map_DoneOk_IsTheSourceVerifierOnTheMappedAnswer(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        var map = Phase(view, "map");
        bool ok = map[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(Verifies(red.reductionFrom, instance, ShownAssignment(view, map[^1])), ok);
        // A correct target answer always maps back to a correct source answer.
        if (ReductionView_Endpoint_Tests.SolvedOk(Phase(view, "solve"))) Assert.True(ok, map[^1].GetProperty("caption").GetString());
        // The target has an answer exactly when the formula is satisfiable.
        Assert.Equal(GareyJohnson_Soundness_Tests.Satisfiable(new API.Problems.NPComplete.NPC_SAT3.SAT3(CnfOf(red))), ReductionView_Endpoint_Tests.SolvedOk(Phase(view, "solve")));
    }

    // The source formula as 3SAT text: a SAT formula's clauses written out again (only for satisfiability).
    private static string CnfOf(IReduction red) =>
        red.reductionFrom is API.Problems.NPComplete.NPC_SAT.SAT sat ? SatAsSat3(sat.clauses) : red.reductionFrom.instance;

    // A satisfiability-preserving 3SAT copy of a SAT formula, for the brute force in GareyJohnson_Soundness_Tests: the target
    // of Karp's own reduction (#821 checks its map; here only its satisfiability is used).
    private static string SatAsSat3(List<List<string>> clauses) {
        var red = new API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3.KarpSATToSAT3(new API.Problems.NPComplete.NPC_SAT.SAT(
            string.Join(" & ", clauses.Select(c => "(" + string.Join(" | ", c) + ")"))));
        return red.reductionTo.instance;
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Map_StepsReadTargetPieces_AndTheSourcePaneKeepsWhatIsMapped(string reduction, string instance) {
        var view = await View(reduction, instance);
        var map = Phase(view, "map");
        var targetIds = PictureIds(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var frame in map.Take(map.Count - 1)) {
            // Each step reads one piece of the target and names the source ids it was built from.
            var focus = Strings(frame.GetProperty("targetFocus"));
            Assert.Single(focus);
            Assert.Contains(focus[0], targetIds);
            Assert.NotEmpty(frame.GetProperty("sourceFocus").EnumerateArray());
        }
        // The assignment only grows: what an earlier step mapped is still there.
        var seen = new Dictionary<string, bool>();
        foreach (var frame in map) {
            var assignment = frame.GetProperty("source").GetProperty("assignment").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetBoolean());
            Assert.All(seen, p => Assert.Equal(p.Value, assignment[p.Key]));
            foreach (var p in assignment) seen[p.Key] = p.Value;
        }
    }

    // Every solver of the target, on every case: 200, the map verdict is the source verifier's on what the view mapped, and a
    // correct target answer maps back to a correct one. Some of these solvers are randomized or answer "no solution" (WalkSAT,
    // Greedy coloring), so the failing paths are covered too.
    public static TheoryData<string, string, string> EverySolverCases() {
        var data = new TheoryData<string, string, string>();
        foreach (var (reduction, instance) in Cases()) {
            Type target = Build(reduction, instance).reductionTo.GetType();
            foreach (var (key, type) in ProblemProvider.Solvers.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>) && i.GetGenericArguments()[0] == target))
                    data.Add(reduction, instance, key);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EverySolverCases))]
    public async Task EverySolverOfTheTarget_GivesAView_WhoseVerdictsAreTheVerifiers(string reduction, string instance, string solver) {
        var view = await View(reduction, instance, solver);
        IReduction red = Build(reduction, instance);
        var map = Phase(view, "map");
        bool ok = map[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(Verifies(red.reductionFrom, instance, ShownAssignment(view, map[^1])), ok);
        if (ReductionView_Endpoint_Tests.SolvedOk(Phase(view, "solve"))) Assert.True(ok, map[^1].GetProperty("caption").GetString());
    }

    // ---- The backward map on its own endpoint ----

    // A correct answer for the target, made from a satisfying assignment by the forward map, maps back to a correct one.
    [Theory]
    [InlineData(Coloring, "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", "(x1:True,x2:True,x3:False)")]
    [InlineData(Ilp, "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", "(x1:True,x2:True,x3:False)")]
    [InlineData(Sat3, "(x1 | !x2 | x3 | x4 | !x5) & (x2 | x3) & (!x1)", "(x1:False,x2:True,x3:False,x4:False,x5:False)")]
    public async Task MapSolutionBack_OfTheForwardMap_IsAcceptedBySourceVerifier(string reduction, string instance, string assignment) {
        IReduction red = Build(reduction, instance);
        string target = red.mapSolutions(assignment);
        Assert.True(Verifies(red.reductionTo, red.reductionTo.instance, target), target);
        var (status, body) = await Post($"mapSolutionBack?reduction={reduction}&solution={Uri.EscapeDataString(target)}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        string back = JsonSerializer.Deserialize<string>(body)!;
        Assert.True(Verifies(red.reductionFrom, instance, back), back);
    }

    [Theory]
    [InlineData(Coloring, "(x1 | !x2)", "F,!x2,C0N0")]
    [InlineData(Coloring, "(x1 | !x2)", "{{F,nosuchnode},{T},{B}}")]
    [InlineData(Ilp, "(x1 | !x2)", "(1 0 1)")]
    [InlineData(Ilp, "(x1 | !x2)", "(1 a)")]
    [InlineData(Matching, "(x1)", "{{x_x1_0,y_x1_0}}")]
    [InlineData(Matching, "(x1)", "{{x_x1_0,y_x1_0,z_x1_1}}")]
    [InlineData(Sat3, "(a | b | c | d)", "(a:maybe)")]
    [InlineData(Sat3, "(a | b | c | d)", "(q:True)")]
    public async Task MapSolutionBack_MalformedTargetAnswer_Is400(string reduction, string instance, string answer) {
        var (status, body) = await Post($"mapSolutionBack?reduction={reduction}&solution={Uri.EscapeDataString(answer)}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
    }

    // ---- Bad input ----

    [Theory]
    [InlineData(Coloring, "(x1 | x2 | x3 | x4)")]
    [InlineData(Ilp, "not a formula")]
    [InlineData(Matching, "(x1 | ")]
    [InlineData(Sat3, "x1 &")]
    [InlineData(Sat3, "")]
    public async Task UnparseableSourceInstance_Is400(string reduction, string instance) {
        var (status, body) = await Post($"reductionView?reduction={reduction}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
        Assert.Contains(JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), new[] { "instance_parse_error", "reduction_input_parse_error" });
    }

    [Theory]
    [InlineData(Coloring, "sat3backtrackingsolver")]
    [InlineData(Ilp, "graphcoloringbruteforce")]
    [InlineData(Sat3, "nosuchsolver")]
    public async Task SolverThatDoesNotSolveTheTarget_Is400(string reduction, string solver) {
        var (status, body) = await Post($"reductionView?reduction={reduction}&solver={solver}", DefaultInstance(reduction));
        Assert.True(status == HttpStatusCode.BadRequest, body);
        Assert.Contains(JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), new[] { "solver_mismatch", "unknown_solver" });
    }
}

// The Reduction View of the 3SAT reductions with a 50 ms solve budget: the default formula's 3-coloring (27 nodes) and
// 3-dimensional matching (135 triples) targets are far past what their brute forces finish, so the solve phase ends with its
// stopped frame and the map phase says there is nothing to map back. The view runs the backtracking solvers by default
// (#814), which finish, so the brute forces are asked for by name.
public class ReductionView_Sat3_TimeLimit_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public ReductionView_Sat3_TimeLimit_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(ReductionView_Sat3_Tests.Coloring, "Graph Coloring", "GraphColoringBruteForce")]
    [InlineData(ReductionView_Sat3_Tests.Matching, "3-Dimensional Matching", "ThreeDimensionalMatchingBruteForce")]
    public async Task DefaultFormula_StoppedSolve_EndsWithTheStoppedFrame_AndSkipsTheMap(string reduction, string target, string bruteForce) {
        var content = new StringContent(JsonSerializer.Serialize(ReductionView_Sat3_Tests.DefaultInstance(reduction)), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reductionView?reduction={reduction}&solver={bruteForce}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var build = frames.Where(f => f.GetProperty("phase").GetString() == "build").ToList();
        // The build ends on a plain "target built" frame and the stopped solve on a Reject, neither with ok (#840).
        Assert.Equal("Accept", build[^1].GetProperty("event").GetString());
        Assert.False(build[^1].TryGetProperty("ok", out _));
        var solve = frames.Where(f => f.GetProperty("phase").GetString() == "solve").ToList();
        Assert.Equal("Reject", solve[^1].GetProperty("event").GetString());
        Assert.False(solve[^1].TryGetProperty("ok", out _));
        Assert.StartsWith("Stopped after 0.05 s and ", solve[^1].GetProperty("caption").GetString());
        var map = Assert.Single(frames, f => f.GetProperty("phase").GetString() == "map");
        Assert.False(map.GetProperty("ok").GetBoolean());
        Assert.Equal($"The solver had not finished, so there is no {target} answer to map back.", map.GetProperty("caption").GetString());
    }
}
