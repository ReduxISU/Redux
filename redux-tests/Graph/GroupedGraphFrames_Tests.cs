using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the Grouped Graph picture (the clause graph of SAT and 3SAT): (clauses, partial Assignment, step event,
// focus) -> clusters, complement edges and named states. No endpoint, no solver.
public class GroupedGraphFrames_Tests {
    // (!x1 | x2 | x3) & (!x1 | !x2 | x4) & (x1 | !x3)
    //   c0-0 c0-1 c0-2      c1-0 c1-1 c1-2    c2-0 c2-1
    private static readonly GroupedGraphSpec Spec = new([[-1, 2, 3], [-1, -2, 4], [1, -3]], ["x1", "x2", "x3", "x4"]);

    private static Assignment Of(params (string Variable, bool Value)[] values) =>
        new(values.Select(v => new KeyValuePair<string, string>(v.Variable, v.Value ? "True" : "False")));

    private static GroupedGraphFrame Frame(Assignment partial, StepEvent ev = StepEvent.Try, params string[] focus) =>
        GroupedGraphFrames.Frame(Spec, partial, ev, focus, "x");

    private static PictureState? Node(GroupedGraphFrame f, string id) => f.Nodes.TryGetValue(id, out var s) ? s.State : null;
    private static PictureState? Group(GroupedGraphFrame f, string id) => f.Groups.TryGetValue(id, out var s) ? s.State : null;

    // ---- the spec ----

    [Fact]
    public void FromLiterals_NumbersVariablesInNaturalOrder_NotInOrderOfAppearance() {
        var spec = GroupedGraphSpec.FromLiterals([["!x10", "x2"], ["x1", "!x2"]]);
        Assert.Equal(["x1", "x2", "x10"], spec.Variables);
        Assert.Equal([[-3, 2], [1, -2]], spec.Clauses);
    }

    [Fact]
    public void FromLiterals_HandlesNamesThatAreNotNumbered() {
        var spec = GroupedGraphSpec.FromLiterals([["q", "!p"], ["p"]]);
        Assert.Equal(["p", "q"], spec.Variables);
        Assert.Equal([[2, -1], [1]], spec.Clauses);
    }

    // ---- ids and payload ----

    [Fact]
    public void Payload_FollowsTheIdContract() {
        var p = GroupedGraphFrames.Payload(Spec);
        Assert.Equal("groupedGraph", p.Kind);
        Assert.Equal("ring", p.Layout);
        Assert.Equal("assignment", p.Shape);
        Assert.Equal("satisfiability", p.Rule);

        Assert.Equal(["c0", "c1", "c2"], p.Groups.Select(g => g.Id));
        Assert.Equal(["C1", "C2", "C3"], p.Groups.Select(g => g.Label));
        Assert.Equal(["c0-0", "c0-1", "c0-2"], p.Groups[0].Members);
        Assert.Equal(["c2-0", "c2-1"], p.Groups[2].Members);

        Assert.Equal(["c0-0", "c0-1", "c0-2", "c1-0", "c1-1", "c1-2", "c2-0", "c2-1"], p.Nodes.Select(n => n.Id));
        var negated = p.Nodes[0];
        Assert.Equal(("¬x1", "v:x1", false, "c0"), (negated.Label, negated.Variable, negated.Positive, negated.Group));
        var positive = p.Nodes[1];
        Assert.Equal(("x2", "v:x2", true, "c0"), (positive.Label, positive.Variable, positive.Positive, positive.Group));
        Assert.Equal(["v:x1", "v:x2", "v:x3", "v:x4"], p.Variables.Select(v => v.Id));
        Assert.Equal(["x1", "x2", "x3", "x4"], p.Variables.Select(v => v.Label));
    }

    [Fact]
    public void Payload_ComplementEdgesJoinALiteralToItsNegationInAnotherClause() {
        var p = GroupedGraphFrames.Payload(Spec);
        // x1 is negated in c0 and c1 and positive in c2; x2 is positive in c0 and negated in c1; x3 is positive in c0 and negated in c2.
        Assert.Equal(["c0-0~c2-0", "c0-1~c1-1", "c0-2~c2-1", "c1-0~c2-0"], p.Edges.Select(e => e.Id));
        Assert.All(p.Edges, e => Assert.Equal(e.Id, $"{e.A}~{e.B}"));
        // Never within one clause, always opposite polarity of one variable.
        var byId = p.Nodes.ToDictionary(n => n.Id);
        Assert.All(p.Edges, e => {
            Assert.NotEqual(byId[e.A].Group, byId[e.B].Group);
            Assert.Equal(byId[e.A].Variable, byId[e.B].Variable);
            Assert.NotEqual(byId[e.A].Positive, byId[e.B].Positive);
        });
    }

    [Fact]
    public void Payload_ClauseEdgesJoinEveryPairInsideOneClause() {
        var p = GroupedGraphFrames.Payload(Spec);
        Assert.Equal(["c0-0~c0-1", "c0-0~c0-2", "c0-1~c0-2", "c1-0~c1-1", "c1-0~c1-2", "c1-1~c1-2", "c2-0~c2-1"], p.ClauseEdges.Select(e => e.Id));
        var byId = p.Nodes.ToDictionary(n => n.Id);
        Assert.All(p.ClauseEdges, e => Assert.Equal(byId[e.A].Group, byId[e.B].Group));
        // Together with the complement pairs this is the 3SAT to Independent Set graph, and no id is used twice.
        Assert.Equal(p.Edges.Count + p.ClauseEdges.Count, p.Edges.Concat(p.ClauseEdges).Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void EdgeIds_AreSplitBackIntoTheirLiteralIdsByTheSeparatorAlone() {
        // Ids contain '-', so '-' cannot be the separator; '~' appears in no clause, literal or variable id.
        var p = GroupedGraphFrames.Payload(Spec);
        var ids = p.Nodes.Select(n => n.Id).Concat(p.Groups.Select(g => g.Id)).Concat(p.Variables.Select(v => v.Id)).ToList();
        Assert.DoesNotContain(ids, id => id.Contains('~'));
        Assert.All(p.Edges.Concat(p.ClauseEdges), e => Assert.Equal([e.A, e.B], e.Id.Split('~')));
    }

    [Fact]
    public void EdgeIds_WriteTheEarlierOccurrenceFirst_ComparingClauseIndexesAsNumbers() {
        // c2 comes before c10 although "c10" < "c2" as text.
        var clauses = Enumerable.Range(0, 11).Select(i => i == 2 ? new[] { 1 } : i == 10 ? new[] { -1 } : new[] { 2 }).ToList();
        var p = GroupedGraphFrames.Payload(new GroupedGraphSpec(clauses, ["x1", "x2"]));
        Assert.Contains(p.Edges, e => e.Id == "c2-0~c10-0");
        Assert.DoesNotContain(p.Edges, e => e.Id == "c10-0~c2-0");
    }

    [Fact]
    public void Payload_ARepeatedLiteralIsTwoNodes_AndATautologyHasNoComplementEdge() {
        // (x1 | x1) & (x1 | !x1)
        var p = GroupedGraphFrames.Payload(new GroupedGraphSpec([[1, 1], [1, -1]], ["x1"]));
        Assert.Equal(["c0-0", "c0-1", "c1-0", "c1-1"], p.Nodes.Select(n => n.Id));
        Assert.Equal(["c0-0~c1-1", "c0-1~c1-1"], p.Edges.Select(e => e.Id));   // x1 in c0 against !x1 in c1; not c1-0 against c1-1
        Assert.Equal(["c0-0~c0-1", "c1-0~c1-1"], p.ClauseEdges.Select(e => e.Id));
    }

    [Fact]
    public void Payload_SerializesWithTheContractsNames_AndNoStates() {
        string json = JsonSerializer.Serialize(GroupedGraphFrames.Payload(Spec));
        Assert.StartsWith("{\"kind\":\"groupedGraph\",\"layout\":\"ring\"", json);
        foreach (var name in new[] { "\"groups\"", "\"nodes\"", "\"variables\"", "\"edges\"", "\"clauseEdges\"", "\"positive\"", "\"members\"", "\"rule\"" })
            Assert.Contains(name, json);
        Assert.DoesNotContain("\"state\"", json);
        Assert.DoesNotContain("\"color\"", json);
    }

    // ---- names that contain the separators ----

    // Names SAT accepts (a letter, then anything but space, parentheses, '&' and '|') that hold this picture's separators.
    private static readonly string[] Awkward = ["a-b", "a~b", "a:b", @"a\b", @"a\-b", "a", "b", "a-", @"a\", "av:a", "c0-1", "a~c0-1"];

    // Reads an escaped name back: a backslash takes the next character literally; a bare separator is not allowed.
    private static string Unescape(string written) {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < written.Length; i++) {
            if (written[i] == '\\') { Assert.True(i + 1 < written.Length, written); sb.Append(written[++i]); continue; }
            Assert.DoesNotContain(written[i], "~-:");
            sb.Append(written[i]);
        }
        return sb.ToString();
    }

    [Fact]
    public void EscapeName_PutsABackslashBeforeEachSeparatorAndBackslash_AndLeavesOrdinaryNamesAlone() {
        Assert.Equal("x1", GroupedGraphFrames.EscapeName("x1"));
        Assert.Equal(@"a\-b", GroupedGraphFrames.EscapeName("a-b"));
        Assert.Equal(@"a\~b", GroupedGraphFrames.EscapeName("a~b"));
        Assert.Equal(@"a\:b", GroupedGraphFrames.EscapeName("a:b"));
        Assert.Equal(@"a\\b", GroupedGraphFrames.EscapeName(@"a\b"));
        Assert.Equal(@"a\\\-b", GroupedGraphFrames.EscapeName(@"a\-b"));
        Assert.Equal(@"v:a\-b", GroupedGraphFrames.VariableId("a-b"));
        // Every name reads back from its id, so no two names share one.
        foreach (var name in Awkward) Assert.Equal(name, Unescape(GroupedGraphFrames.VariableId(name)["v:".Length..]));
        Assert.Equal(Awkward.Length, Awkward.Select(GroupedGraphFrames.VariableId).Distinct().Count());
    }

    [Fact]
    public void Payload_WithAwkwardNames_KeepsEveryIdUnique_AndGivesEdgeEndsAndVariablesExplicitly() {
        // Each awkward name positive in one clause and negated in the next, so every name has a complement edge.
        var clauses = Awkward.Select((n, i) => new[] { n, "!" + Awkward[(i + 1) % Awkward.Length] }).ToList();
        var spec = GroupedGraphSpec.FromLiterals(clauses);
        Assert.Equal(Awkward.Order(StringComparer.Ordinal), spec.Variables.Order(StringComparer.Ordinal));
        var p = GroupedGraphFrames.Payload(spec);

        var ids = p.Groups.Select(g => g.Id).Concat(p.Nodes.Select(n => n.Id)).Concat(p.Variables.Select(v => v.Id))
            .Concat(p.Edges.Select(e => e.Id)).Concat(p.ClauseEdges.Select(e => e.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        // A node names its variable by the variables list's id; the label is the name as written.
        var labels = p.Variables.ToDictionary(v => v.Id, v => v.Label);
        foreach (var n in p.Nodes) {
            Assert.True(labels.ContainsKey(n.Variable), n.Variable);
            Assert.Equal((n.Positive ? "" : "¬") + labels[n.Variable], n.Label);
        }
        Assert.All(p.Variables, v => Assert.Equal(v.Label, Unescape(v.Id["v:".Length..])));
        // Every edge gives both ends, and they are node ids; a node id never holds a name, so it never holds '~'.
        var nodeIds = p.Nodes.Select(n => n.Id).ToHashSet();
        Assert.All(p.Edges.Concat(p.ClauseEdges), e => {
            Assert.Contains(e.A, nodeIds);
            Assert.Contains(e.B, nodeIds);
            Assert.Equal($"{e.A}~{e.B}", e.Id);
        });
        Assert.Equal(Awkward.Length, p.Edges.Count);   // one complement pair per name
        Assert.DoesNotContain(nodeIds, id => id.Contains('~'));
    }

    [Fact]
    public void Frame_WithAwkwardNames_KeysTheAssignmentAndFocusByTheEscapedId() {
        var spec = GroupedGraphSpec.FromLiterals([["a-b", "!a:b"], ["!a-b", @"a\b"]]);
        var f = GroupedGraphFrames.Frame(spec, new Assignment([new("a-b", "True"), new("a:b", "True")]), StepEvent.Accept,
            [GroupedGraphFrames.VariableId(@"a\b")], "x");
        Assert.Equal(new Dictionary<string, bool> { [@"v:a\-b"] = true, [@"v:a\:b"] = true }, f.Assignment);
        Assert.Equal(PictureState.Solution, f.Nodes["c0-0"].State);   // a-b
        Assert.Equal(PictureState.False, f.Nodes["c0-1"].State);      // !a:b
        Assert.Equal(PictureState.False, f.Nodes["c1-0"].State);      // !a-b
        Assert.Equal(PictureState.ElementHighlight, f.Nodes["c1-1"].State);   // a\b, in the focus by its escaped id
        // The raw name is not an id: focusing it highlights nothing.
        var raw = GroupedGraphFrames.Frame(spec, new Assignment([]), StepEvent.Try, ["v:a-b"], "x");
        Assert.Empty(raw.Nodes);
    }

    // ---- states ----

    [Fact]
    public void Frame_NothingAssigned_IsAllBackground() {
        var f = Frame(Of());
        Assert.Empty(f.Nodes);
        Assert.Empty(f.Groups);
        Assert.Empty(f.Assignment);
    }

    [Fact]
    public void Frame_ATrueLiteralIsSolution_AndItsClauseIsCovered_AFalseLiteralIsFalse_AnUnassignedOneIsBackground() {
        // x1 = True, kept (Accept): x1 in c2 is true; !x1 in c0 and c1 are false; x2, x3, x4 are unassigned.
        var f = Frame(Of(("x1", true)), StepEvent.Accept);
        Assert.Equal(["c0-0", "c1-0", "c2-0"], f.Nodes.Keys);
        Assert.Equal(PictureState.Solution, Node(f, "c2-0"));
        Assert.Equal(PictureState.False, Node(f, "c0-0"));
        Assert.Equal(PictureState.False, Node(f, "c1-0"));
        // A literal of an unassigned variable is Background (absent), whatever the event: nothing is decided about it.
        foreach (var ev in Enum.GetValues<StepEvent>())
            Assert.All(new[] { "c0-1", "c0-2", "c1-1", "c1-2", "c2-1" }, id => Assert.Null(Node(Frame(Of(("x1", true)), ev), id)));
        Assert.Equal(["c2"], f.Groups.Keys);
        Assert.Equal(PictureState.Covered, Group(f, "c2"));
        Assert.Equal(new Dictionary<string, bool> { ["v:x1"] = true }, f.Assignment);
    }

    [Theory]
    [InlineData("Try")]
    [InlineData("Reject")]
    public void Frame_ACandidateThatIsOnlyTried_IsNeverSolution_ItsTrueLiteralsAreElementHighlight(string eventName) {
        var ev = Enum.Parse<StepEvent>(eventName);
        // x1 = False, x2 = True, x3 = False, x4 = False satisfies every clause, but a Try or Reject only tries it.
        var f = Frame(Of(("x1", false), ("x2", true), ("x3", false), ("x4", false)), ev);
        Assert.DoesNotContain(f.Nodes.Values, s => s.State == PictureState.Solution);
        Assert.DoesNotContain(f.Groups.Values, s => s.State == PictureState.Solution);
        // c0 = (!x1 | x2 | x3): !x1 and x2 are true, x3 is false.
        Assert.Equal(PictureState.ElementHighlight, Node(f, "c0-0"));
        Assert.Equal(PictureState.ElementHighlight, Node(f, "c0-1"));
        Assert.Equal(PictureState.False, Node(f, "c0-2"));
        Assert.All(f.Groups.Values, g => Assert.Equal(PictureState.Covered, g.State));
    }

    [Theory]
    [InlineData("Accept")]
    [InlineData("Backtrack")]
    [InlineData("Done")]
    public void Frame_AnAssignmentTheSolverKeeps_DrawsItsTrueLiteralsAsSolution(string eventName) {
        var ev = Enum.Parse<StepEvent>(eventName);
        var f = Frame(Of(("x1", false), ("x2", true), ("x3", false), ("x4", false)), ev);
        Assert.Equal(PictureState.Solution, Node(f, "c0-0"));
        Assert.Equal(PictureState.Solution, Node(f, "c0-1"));
        Assert.Equal(PictureState.False, Node(f, "c0-2"));
    }

    [Fact]
    public void Frame_FalseShowsUnderTheHighlightAndTheRejection() {
        // x1 = True: !x1 at c0-0 is False; trying x1 highlights it instead.
        Assert.Equal(PictureState.ElementHighlight, Node(Frame(Of(("x1", true)), StepEvent.Try, "v:x1"), "c0-0"));
        // In a falsified clause every literal is Rejected, not False.
        var f = Frame(Of(("x1", true), ("x2", true), ("x4", false)), StepEvent.Accept);
        Assert.All(new[] { "c1-0", "c1-1", "c1-2" }, id => Assert.Equal(PictureState.Rejected, Node(f, id)));
        Assert.Equal(PictureState.False, Node(f, "c0-0"));   // !x1 in c0, whose clause x2 saves
        Assert.True(GraphSubsetFrames.Rank(PictureState.False) > GraphSubsetFrames.Rank(PictureState.Background));
        Assert.True(GraphSubsetFrames.Rank(PictureState.False) < GraphSubsetFrames.Rank(PictureState.Blocked));
    }

    [Fact]
    public void Frame_AClauseWithEveryLiteralFalseIsRejected_AndSoAreItsLiterals() {
        // x1 = True, x2 = True, x4 = False: c1 = (!x1 | !x2 | x4) is all false; c0 is true through x2, c2 through x1.
        var f = Frame(Of(("x1", true), ("x2", true), ("x4", false)));
        Assert.Equal(PictureState.Rejected, Group(f, "c1"));
        Assert.All(new[] { "c1-0", "c1-1", "c1-2" }, id => Assert.Equal(PictureState.Rejected, Node(f, id)));
        Assert.Equal(PictureState.Covered, Group(f, "c0"));   // x2 is true in c0
        Assert.Equal(PictureState.ElementHighlight, Node(f, "c0-1"));   // a Try: tried, not the answer
        // The same judgment on every event: a clash is a clash.
        foreach (var ev in new[] { StepEvent.Try, StepEvent.Accept, StepEvent.Backtrack, StepEvent.Done })
            Assert.Equal(PictureState.Rejected, Group(Frame(Of(("x1", true), ("x2", true), ("x4", false)), ev), "c1"));
    }

    [Fact]
    public void Frame_ASatisfiedFormula_HasOneSolutionLiteralPerCluster_AtLeast() {
        // x1 = False, x2 = True, x3 = False, x4 = False satisfies all three clauses.
        var f = Frame(Of(("x1", false), ("x2", true), ("x3", false), ("x4", false)), StepEvent.Done);
        Assert.Equal(["c0", "c1", "c2"], f.Groups.Keys);
        Assert.All(f.Groups.Values, g => Assert.Equal(PictureState.Covered, g.State));
        Assert.DoesNotContain(f.Nodes.Values, s => s.State == PictureState.Rejected);
        foreach (var group in new[] { "c0", "c1", "c2" })
            Assert.Contains(f.Nodes.Keys, id => id.StartsWith(group + "-"));
    }

    [Fact]
    public void Frame_TheVariableBeingTriedIsElementHighlight_OnEveryOccurrence_EvenWhenItsLiteralIsTrue() {
        var f = Frame(Of(("x1", true)), StepEvent.Try, "v:x1");
        // x1 occurs in c0-0, c1-0 and c2-0; c2-0 is true (Solution), which the highlight shows over.
        Assert.Equal(["c0-0", "c1-0", "c2-0"], f.Nodes.Keys);
        Assert.All(f.Nodes.Values, s => Assert.Equal(PictureState.ElementHighlight, s.State));
        Assert.Equal(PictureState.Covered, Group(f, "c2"));
        Assert.Equal(["v:x1"], f.Focus);
    }

    [Fact]
    public void Frame_AFailureIsNeverHiddenByTheHighlight() {
        // x4 is being tried (False) and its clause is all false: the literal is Rejected, not ElementHighlight.
        var f = Frame(Of(("x1", true), ("x2", true), ("x4", false)), StepEvent.Reject, "v:x4", "c1");
        Assert.Equal(PictureState.Rejected, Node(f, "c1-2"));
        Assert.Equal(PictureState.Rejected, Group(f, "c1"));
    }

    [Fact]
    public void Frame_AClauseInTheFocus_IsElementHighlight_OrRejectedInARejectStep() {
        var tried = Frame(Of(), StepEvent.Try, "c0");
        Assert.Equal(PictureState.ElementHighlight, Group(tried, "c0"));
        var rejected = Frame(Of(), StepEvent.Reject, "c0");
        Assert.Equal(PictureState.Rejected, Group(rejected, "c0"));
        Assert.Empty(rejected.Nodes);   // only what is falsified in the assignment is rejected in the nodes
    }

    [Fact]
    public void Frame_ALiteralInTheFocus_IsElementHighlight_AndUnknownIdsAreIgnored() {
        var f = Frame(Of(), StepEvent.Try, "c1-2", "c1-9", "c9", "c01", "v:nope", "zzz");
        Assert.Equal(["c1-2"], f.Nodes.Keys);
        Assert.Equal(PictureState.ElementHighlight, Node(f, "c1-2"));
        Assert.Empty(f.Groups);
    }

    [Fact]
    public void Frame_IgnoresVariablesTheFormulaDoesNotHave_AndValuesItCannotRead() {
        var partial = new Assignment([new("x1", "True"), new("zz", "True"), new("x2", "maybe")]);
        var f = Frame(partial);
        Assert.Equal(["v:x1"], f.Assignment.Keys);
    }

    [Fact]
    public void Frame_AssignmentIsKeyedByVariableId_InOrder_AndOkIsOmittedUnlessSet() {
        var f = Frame(Of(("x4", false), ("x1", true)));
        Assert.Equal(["v:x1", "v:x4"], f.Assignment.Keys);
        string json = JsonSerializer.Serialize(f);
        Assert.Contains("\"assignment\":{\"v:x1\":true,\"v:x4\":false}", json);
        Assert.DoesNotContain("\"ok\"", json);
        Assert.Contains("\"ok\":true", JsonSerializer.Serialize(GroupedGraphFrames.Frame(Spec, Of(), StepEvent.Done, [], "x", true)));
    }

    [Fact]
    public void Frame_SerializesStateNamesAsStrings_IncludingFalse() {
        // x1 = True, x2 = True, x4 = False: !x1 at c0-0 is false (False), x2 at c0-1 is true (tried: ElementHighlight).
        string json = JsonSerializer.Serialize(Frame(Of(("x1", true), ("x2", true), ("x4", false)), StepEvent.Reject, "v:x4", "c1"));
        Assert.Contains("\"state\":\"Rejected\"", json);
        Assert.Contains("\"state\":\"Covered\"", json);
        Assert.Contains("\"event\":\"Reject\"", json);
        Assert.Contains("\"c0-0\":{\"state\":\"False\"}", json);
        Assert.Contains("\"c0-1\":{\"state\":\"ElementHighlight\"}", json);
        Assert.DoesNotContain("\"state\":\"Solution\"", json);
    }

    [Fact]
    public void PictureState_FalseIsAddedAtTheEnd_AndSerializesAsItsName() {
        // Appended, so the numbers of the existing names do not move.
        Assert.Equal(["Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "False"], Enum.GetNames<PictureState>());
        Assert.Equal("\"False\"", JsonSerializer.Serialize(PictureState.False));
        Assert.Equal("{\"state\":\"False\"}", JsonSerializer.Serialize(new StateRef(PictureState.False)));
    }

    // ---- reading answers and captions ----

    [Theory]
    [InlineData("(x1:True,x2:False)", "x1=True;x2=False")]
    [InlineData("x1=T, x2 = F", "x1=True;x2=False")]
    [InlineData("(x1:True,x1:False)", "x1=True")]
    [InlineData("No Solution", "")]
    [InlineData("No solution exists", "")]
    [InlineData("timeout", "")]
    [InlineData("", "")]
    [InlineData("(a:b:True,c=d=False,a-b:T)", "a-b=True;a:b=True;c=d=False")]
    public void ParseAssignment_ReadsCertificates_AndGivesNothingForNoAnswer(string answer, string expected) {
        var parsed = GroupedGraphFrames.ParseAssignment(answer);
        Assert.Equal(expected, string.Join(";", parsed.Values.Select(p => $"{p.Key}={p.Value}")));
    }

    [Fact]
    public void FirstFalsifiedClause_FindsTheFirstClauseWithNoTrueLiteral_AndTreatsUnassignedAsSaving() {
        List<List<string>> clauses = [["!x1", "x2"], ["x1", "x3"], ["!x2"]];
        Assert.Equal(-1, GroupedGraphFrames.FirstFalsifiedClause(clauses, new Dictionary<string, bool>()));
        Assert.Equal(-1, GroupedGraphFrames.FirstFalsifiedClause(clauses, new Dictionary<string, bool> { ["x1"] = true }));
        Assert.Equal(0, GroupedGraphFrames.FirstFalsifiedClause(clauses, new Dictionary<string, bool> { ["x1"] = true, ["x2"] = false }));
        Assert.Equal(1, GroupedGraphFrames.FirstFalsifiedClause(clauses, new Dictionary<string, bool> { ["x1"] = false, ["x3"] = false }));
        Assert.Equal(2, GroupedGraphFrames.FirstFalsifiedClause(clauses, new Dictionary<string, bool> { ["x2"] = true }));
        Assert.Equal("(x1 | !x2)", GroupedGraphFrames.ClauseText(["x1", "!x2"]));
        Assert.Equal("x1 = True, x2 = False", GroupedGraphFrames.Assigned(new Dictionary<string, bool> { ["x1"] = true, ["x2"] = false }));
    }

    // ---- the whole response ----

    private static SolveRun RunOf(string answer, params object[] steps) => new(answer, steps, typeof(Assignment));

    [Fact]
    public void Build_WithoutSteps_EndsInASolvedFrameBuiltFromTheAnswer() {
        var r = GroupedGraphFrames.Build(Spec, new SolveRun("(x1:False,x2:True,x3:False,x4:False)", [], null), GroupedGraphFrames.ParseAssignment, _ => true);
        Assert.Equal("GroupedGraph", r.Type);
        Assert.IsType<GroupedGraphPayload>(r.Payload);
        var frame = Assert.IsType<GroupedGraphFrame>(Assert.Single(r.Frames));
        Assert.Equal(StepEvent.Done, frame.Event);
        Assert.True(frame.Ok);
        Assert.Equal("The answer is x1 = False, x2 = True, x3 = False, x4 = False.", frame.Caption);
        Assert.Equal(["c0", "c1", "c2"], frame.Groups.Keys);
    }

    [Fact]
    public void Build_WithNoAnswer_SaysSo_AndIsNotOk() {
        var r = GroupedGraphFrames.Build(Spec, new SolveRun("No Solution", [], null), GroupedGraphFrames.ParseAssignment, _ => false);
        var frame = Assert.IsType<GroupedGraphFrame>(Assert.Single(r.Frames));
        Assert.Equal("No answer was found.", frame.Caption);
        Assert.False(frame.Ok);
        Assert.Empty(frame.Assignment);
    }

    [Fact]
    public void Build_UsesTheSolversSteps_AndAddsNoSecondDone() {
        var steps = new object[] {
            new SolverStep<Assignment>(Of(("x1", true)), StepEvent.Try, ["v:x1"], "Try x1 = True."),
            new SolverStep<Assignment>(Of(("x1", true)), StepEvent.Done, [], "Done.") { Ok = true },
        };
        var r = GroupedGraphFrames.Build(Spec, RunOf("(x1:True)", steps), GroupedGraphFrames.ParseAssignment, _ => throw new InvalidOperationException("the Done step already says"));
        Assert.Equal(2, r.Frames.Count);
        Assert.Equal("Try x1 = True.", ((GroupedGraphFrame)r.Frames[0]).Caption);
    }

    [Fact]
    public void Build_DropsStepsOfAnotherShape_AndShowsTheSolvedFrameInstead() {
        object[] steps = [new SolverStep<NodeSet>(new NodeSet(["a"]), StepEvent.Done, [], "x") { Ok = true }];
        var r = GroupedGraphFrames.Build(Spec, new SolveRun("(x1:True)", steps, typeof(NodeSet)), GroupedGraphFrames.ParseAssignment, _ => true);
        Assert.Equal("The answer is x1 = True.", ((GroupedGraphFrame)Assert.Single(r.Frames)).Caption);
    }

    // ---- runs stopped by the time limit ----

    [Fact]
    public void Build_StoppedRun_EndsWithABlankNotOkDone_AndNeverReadsTheAnswer() {
        object[] steps = [
            new SolverStep<Assignment>(Of(("x1", true), ("x2", true), ("x3", false), ("x4", true)), StepEvent.Reject, [], "Try x1 = T, x2 = T, x3 = F, x4 = T."),
            new SolverStep<Assignment>(Of(("x1", true), ("x2", true), ("x3", false), ("x4", true)), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(Assignment)) { TimedOut = true };
        var response = GroupedGraphFrames.Build(Spec, run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<GroupedGraphFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Nodes);        // no literal is drawn true (Solution)
        Assert.Empty(done.Groups);
        Assert.Empty(done.Assignment);
        Assert.Contains("\"assignment\":{}", JsonSerializer.Serialize(done));   // every frame carries an assignment

        var none = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var only = Assert.IsType<GroupedGraphFrame>(Assert.Single(GroupedGraphFrames.Build(Spec, none,
            _ => throw new InvalidOperationException("not read"), _ => true).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, only.Caption);
        Assert.False(only.Ok);
    }

    [Fact]
    public void Build_ARunStoppedByTheSolversOwnTimer_EndsWithAStoppedDone_AndNeverReadsTimeoutAsAnAssignment() {
        // With its Done step (what the SAT brute force records): the step is drawn as the stopped frame.
        object[] steps = [
            new SolverStep<Assignment>(Of(("x1", true), ("x2", true), ("x3", false), ("x4", true)), StepEvent.Reject, ["c1"], "Try ..."),
            new SolverStep<Assignment>(Of(("x1", true)), StepEvent.Done, [], GroupedGraphFrames.SolverTimeoutCaption) { Ok = false },
        ];
        var response = GroupedGraphFrames.Build(Spec, RunOf(GroupedGraphFrames.SolverTimeoutAnswer, steps),
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        Assert.Equal(2, response.Frames.Count);
        var done = Assert.IsType<GroupedGraphFrame>(response.Frames[^1]);
        Assert.Equal((StepEvent.Done, (bool?)false, GroupedGraphFrames.SolverTimeoutCaption), (done.Event, done.Ok, done.Caption));
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Groups);
        Assert.Empty(done.Assignment);

        // Without steps (not recorded, or of another shape): a stopped Done is added, and "timeout" is never parsed.
        var only = Assert.IsType<GroupedGraphFrame>(Assert.Single(GroupedGraphFrames.Build(Spec, new SolveRun("timeout", [], null),
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified")).Frames));
        Assert.Equal((StepEvent.Done, (bool?)false, GroupedGraphFrames.SolverTimeoutCaption), (only.Event, only.Ok, only.Caption));
        Assert.Contains("stopped without finishing", only.Caption);
    }
}
