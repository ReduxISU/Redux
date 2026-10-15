using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the SAT factor graph (a Bipartite Graph of variables and clauses): (clauses, assignment, event, focus) ->
// payload and named states. No endpoint, no solver.
public class FactorGraphFrames_Tests {
    // (x1 | !x2) & (!x1 | x3) & (x2 | x3 | !x3)
    private static GroupedGraphSpec Spec() => GroupedGraphSpec.FromLiterals([["x1", "!x2"], ["!x1", "x3"], ["x2", "x3", "!x3"]]);

    private static Assignment Values(params (string Name, bool Value)[] values) =>
        new(values.Select(v => new KeyValuePair<string, string>(v.Name, v.Value ? "True" : "False")));

    private static PictureState? Node(FactorGraphFrame f, string id) => f.Nodes.TryGetValue(id, out var s) ? s.State : null;
    private static PictureState? Edge(FactorGraphFrame f, string id) => f.Edges.TryGetValue(id, out var s) ? s.State : null;

    [Fact]
    public void Payload_HasVariablesLeft_ClausesRight_AndAnEdgePerLiteralWithItsPolarity() {
        var p = FactorGraphFrames.Payload(Spec());
        Assert.Equal("bipartiteGraph", p.Kind);
        Assert.Equal("columns", p.Layout);
        Assert.Equal("assignment", p.PickSide);
        Assert.Equal("satisfiability", p.Rule);
        Assert.Equal(["v:x1", "v:x2", "v:x3"], p.Elements.Select(e => e.Id));
        Assert.Equal(["c0", "c1", "c2"], p.Sets.Select(s => s.Id));
        Assert.Equal(["C1", "C2", "C3"], p.Sets.Select(s => s.Label));
        // x3 and !x3 in one clause: two edges, one member.
        Assert.Equal(["v:x2", "v:x3"], p.Sets[2].Members);
        Assert.Equal(["c0-0", "c0-1", "c1-0", "c1-1", "c2-0", "c2-1", "c2-2"], p.Edges.Select(e => e.Id));
        Assert.Equal([true, false, false, true, true, true, false], p.Edges.Select(e => e.Positive));
        Assert.Equal(("v:x3", "c2"), (p.Edges[^1].A, p.Edges[^1].B));
    }

    [Fact]
    public void Payload_Json_IsTheContract() {
        var spec = GroupedGraphSpec.FromLiterals([["x1", "!x2"]]);
        Assert.Equal(
            "{\"kind\":\"bipartiteGraph\",\"layout\":\"columns\",\"pickSide\":\"assignment\",\"rule\":\"satisfiability\"," +
            "\"elements\":[{\"id\":\"v:x1\",\"label\":\"x1\"},{\"id\":\"v:x2\",\"label\":\"x2\"}]," +
            "\"sets\":[{\"id\":\"c0\",\"label\":\"C1\",\"members\":[\"v:x1\",\"v:x2\"]}]," +
            "\"edges\":[{\"id\":\"c0-0\",\"a\":\"v:x1\",\"b\":\"c0\",\"positive\":true},{\"id\":\"c0-1\",\"a\":\"v:x2\",\"b\":\"c0\",\"positive\":false}]}",
            JsonSerializer.Serialize(FactorGraphFrames.Payload(spec)));
    }

    [Fact]
    public void AnAcceptedAssignment_TrueLiteralsSolution_FalseOnesFalse_UnassignedBackground() {
        // x1 = True, x2 = True; x3 unassigned. The Accept ends the run, so the assignment is the confirmed answer.
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x2", true)), StepEvent.Accept, [], "...", endsRun: true);
        Assert.Equal(PictureState.Solution, Node(f, "v:x1"));
        Assert.Equal(PictureState.Solution, Node(f, "v:x2"));
        Assert.Null(Node(f, "v:x3"));
        Assert.Equal(PictureState.Solution, Edge(f, "c0-0"));     // x1
        Assert.Equal(PictureState.False, Edge(f, "c0-1"));        // !x2
        Assert.Equal(PictureState.False, Edge(f, "c1-0"));        // !x1
        Assert.Null(Edge(f, "c1-1"));                             // x3, unassigned
        // An Accept the run goes on from keeps the assignment without confirming it (#886).
        var kept = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x2", true)), StepEvent.Accept, [], "...");
        Assert.Equal(PictureState.Committed, Node(kept, "v:x1"));
        Assert.Equal(PictureState.Committed, Edge(kept, "c0-0"));
        Assert.Equal(PictureState.Covered, Node(f, "c0"));
        Assert.Null(Node(f, "c1"));                               // x3 may still save it
        Assert.Equal(PictureState.Covered, Node(f, "c2"));
        Assert.Equal(new Dictionary<string, int> { ["c0"] = 1, ["c2"] = 1 }, f.Hits);
        Assert.Equal(new Dictionary<string, bool> { ["v:x1"] = true, ["v:x2"] = true }, f.Assignment);
    }

    [Fact]
    public void AFailedDone_DrawsItsTrueLiteralsRejected() {
        // A run that ends without an answer never shows green (#886).
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x2", true)), StepEvent.Done, [], "...", ok: false);
        Assert.Equal(PictureState.Rejected, Node(f, "v:x1"));
        Assert.Equal(PictureState.Rejected, Edge(f, "c0-0"));
        Assert.Equal(PictureState.False, Edge(f, "c0-1"));
        Assert.DoesNotContain(f.Nodes.Values.Concat(f.Edges.Values), s => s.State == PictureState.Solution);
    }

    [Fact]
    public void AFalseVariable_IsFalse_AndAClauseWithEveryLiteralFalse_IsRejected() {
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x2", false), ("x3", false)), StepEvent.Accept, [], "...");
        Assert.Equal(PictureState.False, Node(f, "v:x2"));
        Assert.Equal(PictureState.Rejected, Node(f, "c1"));       // !x1 false, x3 false
        Assert.Equal(PictureState.Rejected, Edge(f, "c1-0"));     // its edges too, as the clause graph marks its literals
        Assert.Equal(PictureState.Rejected, Edge(f, "c1-1"));
        Assert.Equal(PictureState.Covered, Node(f, "c2"));        // !x3 is true
        Assert.Equal(PictureState.False, Edge(f, "c2-1"));        // x3, false in a covered clause
    }

    [Theory]
    [InlineData("Try", false)]
    [InlineData("Reject", false)]
    [InlineData("Accept", false)]
    [InlineData("Done", false)]
    [InlineData("Try", true)]
    [InlineData("Reject", true)]
    [InlineData("Backtrack", true)]
    public void AnAllFalseClause_HasRejectedEdges_WhateverTheEvent_EvenWhenItsVariableIsTheFocus(string ev, bool incremental) {
        // x1 = True, x3 = False: C2 = (!x1 | x3) has every literal false. The tried variable's highlight does not cover it.
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x3", false)), Enum.Parse<StepEvent>(ev), ["v:x3"], "...", incremental: incremental);
        Assert.Equal(PictureState.Rejected, Node(f, "c1"));
        Assert.Equal(PictureState.Rejected, Edge(f, "c1-0"));
        Assert.Equal(PictureState.Rejected, Edge(f, "c1-1"));
        // The variables themselves keep their values' states: x3 is the focus, x1 is true.
        Assert.Equal(PictureState.ElementHighlight, Node(f, "v:x3"));
        Assert.NotEqual(PictureState.Rejected, Node(f, "v:x1"));
    }

    [Fact]
    public void AClauseWithAnUnassignedLiteral_IsNotRejected_AndItsFalseEdgesStayFalse() {
        // x2 = True, x3 = False: C3 = (x2 | x3 | !x3) is covered; C1 = (x1 | !x2) has !x2 false but x1 unassigned.
        var f = FactorGraphFrames.Frame(Spec(), Values(("x2", true), ("x3", false)), StepEvent.Accept, [], "...");
        Assert.Null(Node(f, "c0"));
        Assert.Equal(PictureState.False, Edge(f, "c0-1"));
        Assert.Null(Edge(f, "c0-0"));
    }

    // The factor graph's edge c{i}-{j} and the clause graph's literal c{i}-{j} are the same literal, and must be in the
    // same state in every frame.
    public static TheoryData<string, string, bool, string> ParityCases => new() {
        { "x1=T,x2=F,x3=F", "Accept", false, "" },
        { "x1=T,x2=F,x3=F", "Reject", false, "" },
        { "x1=T,x2=F,x3=F", "Try", false, "" },
        { "x1=T,x2=F,x3=F", "Done", false, "" },
        { "x1=T,x3=F", "Reject", true, "v:x3,c1" },
        { "x1=T,x3=F", "Try", true, "v:x3" },
        { "x1=F,x2=T", "Backtrack", true, "v:x2" },
        { "x1=F,x2=T,x3=F", "Accept", true, "c1-1" },
        { "", "Try", true, "v:x1" },
    };

    [Theory]
    [MemberData(nameof(ParityCases))]
    public void EveryEdge_IsInTheStateOfTheClauseGraphsLiteral(string values, string ev, bool incremental, string focus) {
        var assignment = Values(values.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=')).Select(p => (p[0], p[1] == "T")).ToArray());
        string[] ids = focus.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var factor = FactorGraphFrames.Frame(Spec(), assignment, Enum.Parse<StepEvent>(ev), ids, "...", incremental: incremental);
        var clause = GroupedGraphFrames.Frame(Spec(), assignment, Enum.Parse<StepEvent>(ev), ids, "...", incremental: incremental);
        foreach (var edge in FactorGraphFrames.Payload(Spec()).Edges)
            Assert.Equal(clause.Nodes.TryGetValue(edge.Id, out var s) ? s.State : (PictureState?)null, Edge(factor, edge.Id));
    }

    [Fact]
    public void AWholeAssignmentThatIsOnlyTried_IsHighlighted_NotSolution() {
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x2", true), ("x3", true)), StepEvent.Reject, [], "Try ...");
        Assert.Equal(PictureState.ElementHighlight, Node(f, "v:x1"));
        Assert.Equal(PictureState.ElementHighlight, Edge(f, "c0-0"));
        Assert.DoesNotContain(f.Nodes.Values.Concat(f.Edges.Values), s => s.State == PictureState.Solution);
    }

    [Fact]
    public void TheTriedVariable_IsHighlighted_WithItsTrueEdges_ButItsFalseEdgesStayFalse() {
        // An incremental run (3SAT backtracking) tries x1 = True with nothing else decided.
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true)), StepEvent.Try, ["v:x1"], "Try x1 = True.", incremental: true);
        Assert.Equal(PictureState.ElementHighlight, Node(f, "v:x1"));
        Assert.Equal(PictureState.ElementHighlight, Edge(f, "c0-0"));   // x1, true
        Assert.Equal(PictureState.False, Edge(f, "c1-0"));              // !x1, false: stays False
        Assert.Equal(PictureState.Covered, Node(f, "c0"));
    }

    [Fact]
    public void AClauseInTheFocusOfAReject_IsRejected() {
        var f = FactorGraphFrames.Frame(Spec(), Values(("x1", true), ("x3", false)), StepEvent.Reject, ["v:x3", "c1"], "C2 has no true literal left.", incremental: true);
        Assert.Equal(PictureState.Rejected, Node(f, "c1"));
        Assert.Equal(PictureState.ElementHighlight, Node(f, "v:x3"));   // the tried variable, whatever its value
        Assert.Equal(PictureState.Committed, Node(f, "v:x1"));          // decided earlier: kept
    }

    [Fact]
    public void Frame_Json_HasTheBipartiteFieldsPlusTheAssignment() {
        var f = FactorGraphFrames.Frame(GroupedGraphSpec.FromLiterals([["x1", "!x2"]]), Values(("x1", false)), StepEvent.Accept, ["v:x1"], "x1 = False.", incremental: true);
        Assert.Equal(
            "{\"event\":\"Accept\",\"caption\":\"x1 = False.\",\"focus\":[\"v:x1\"]," +
            "\"nodes\":{\"v:x1\":{\"state\":\"ElementHighlight\"}},\"edges\":{\"c0-0\":{\"state\":\"False\"}},\"hits\":{},\"assignment\":{\"v:x1\":false}}",
            JsonSerializer.Serialize(f));
    }

    [Fact]
    public void Build_UsesTheBipartiteType_AndEndsAStoppedRunWithABlankFrame() {
        object[] steps = [
            new SolverStep<Assignment>(Values(("x1", true)), StepEvent.Reject, [], "Try ..."),
            new SolverStep<Assignment>(Values(("x1", true)), StepEvent.Done, [], "Stopped after 0.05 s and 3 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(Assignment)) { TimedOut = true };
        var response = FactorGraphFrames.Build(Spec(), run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        Assert.Equal("BipartiteGraph", response.Type);
        Assert.IsType<FactorGraphPayload>(response.Payload);
        var done = Assert.IsType<FactorGraphFrame>(response.Frames[^1]);
        Assert.False(done.Ok);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);
        Assert.Empty(done.Hits);
        Assert.Empty(done.Assignment);
    }

    [Fact]
    public void Build_TheSolversOwnTimeout_IsAStoppedRunToo() {
        var response = FactorGraphFrames.Build(Spec(), new SolveRun("timeout", [], null), _ => throw new InvalidOperationException("not read"), _ => true);
        var done = Assert.IsType<FactorGraphFrame>(Assert.Single(response.Frames));
        Assert.False(done.Ok);
        Assert.Equal(GroupedGraphFrames.SolverTimeoutCaption, done.Caption);
    }

    [Fact]
    public void Build_WithoutSteps_DrawsTheAnswer() {
        var response = FactorGraphFrames.Build(Spec(), new SolveRun("(x1:True,x2:False,x3:True)", [], null), GroupedGraphFrames.ParseAssignment, _ => true);
        var done = Assert.IsType<FactorGraphFrame>(Assert.Single(response.Frames));
        Assert.True(done.Ok);
        Assert.Equal(PictureState.Solution, Node(done, "v:x1"));
        Assert.All(["c0", "c1", "c2"], c => Assert.Equal(PictureState.Covered, Node(done, c)));
    }
}
