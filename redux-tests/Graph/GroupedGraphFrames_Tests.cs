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

    // ---- states ----

    [Fact]
    public void Frame_NothingAssigned_IsAllBackground() {
        var f = Frame(Of());
        Assert.Empty(f.Nodes);
        Assert.Empty(f.Groups);
        Assert.Empty(f.Assignment);
    }

    [Fact]
    public void Frame_ATrueLiteralIsSolution_AndItsClauseIsCovered_AFalseLiteralIsBackground() {
        // x1 = True: x1 in c2 is true; !x1 in c0 and c1 are false.
        var f = Frame(Of(("x1", true)));
        Assert.Equal(["c2-0"], f.Nodes.Keys);
        Assert.Equal(PictureState.Solution, Node(f, "c2-0"));
        Assert.Equal(["c2"], f.Groups.Keys);
        Assert.Equal(PictureState.Covered, Group(f, "c2"));
        Assert.Null(Node(f, "c0-0"));   // false: Background, not a state of its own
        Assert.Equal(new Dictionary<string, bool> { ["v:x1"] = true }, f.Assignment);
    }

    [Fact]
    public void Frame_AClauseWithEveryLiteralFalseIsRejected_AndSoAreItsLiterals() {
        // x1 = True, x2 = True, x4 = False: c1 = (!x1 | !x2 | x4) is all false; c0 is true through x2, c2 through x1.
        var f = Frame(Of(("x1", true), ("x2", true), ("x4", false)));
        Assert.Equal(PictureState.Rejected, Group(f, "c1"));
        Assert.All(new[] { "c1-0", "c1-1", "c1-2" }, id => Assert.Equal(PictureState.Rejected, Node(f, id)));
        Assert.Equal(PictureState.Covered, Group(f, "c0"));   // x2 is true in c0
        Assert.Equal(PictureState.Solution, Node(f, "c0-1"));
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
    public void Frame_SerializesStateNamesAsStrings_AndNeverFalse() {
        string json = JsonSerializer.Serialize(Frame(Of(("x1", true), ("x2", true), ("x4", false)), StepEvent.Reject, "v:x4", "c1"));
        Assert.Contains("\"state\":\"Rejected\"", json);
        Assert.Contains("\"state\":\"Covered\"", json);
        Assert.Contains("\"event\":\"Reject\"", json);
        Assert.DoesNotContain("\"state\":\"False\"", json);
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
}
