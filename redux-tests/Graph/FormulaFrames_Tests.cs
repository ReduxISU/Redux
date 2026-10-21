using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the Formula picture of SAT and 3SAT (#925): (clauses, partial Assignment, step event, focus) -> the Grouped
// Graph's literal and clause states plus the variables' states. No endpoint, no solver.
public class FormulaFrames_Tests {
    // (!x1 | x2 | x3) & (!x1 | !x2 | x4) & (x1 | !x3)
    //   c0-0 c0-1 c0-2      c1-0 c1-1 c1-2    c2-0 c2-1
    private static readonly GroupedGraphSpec Spec = new([[-1, 2, 3], [-1, -2, 4], [1, -3]], ["x1", "x2", "x3", "x4"]);

    private static Assignment Of(params (string Variable, bool Value)[] values) =>
        new(values.Select(v => new KeyValuePair<string, string>(v.Variable, v.Value ? "True" : "False")));

    private static PictureState? Variable(FormulaFrame f, string id) => f.Variables.TryGetValue(id, out var s) ? s.State : null;

    private static Dictionary<string, PictureState> States(IReadOnlyDictionary<string, StateRef> map) => map.ToDictionary(p => p.Key, p => p.Value.State);

    // ---- the payload ----

    [Fact]
    public void Payload_IsTheGroupedGraphsGroupsNodesAndVariables_WithoutItsEdges() {
        var formula = FormulaFrames.Payload(Spec);
        var graph = GroupedGraphFrames.Payload(Spec);
        Assert.Equal("formula", formula.Kind);
        Assert.Equal("cnf", formula.Layout);
        Assert.Equal("assignment", formula.Shape);
        Assert.Equal("satisfiability", formula.Rule);
        Assert.Equal(JsonSerializer.Serialize(graph.Groups), JsonSerializer.Serialize(formula.Groups));
        Assert.Equal(JsonSerializer.Serialize(graph.Nodes), JsonSerializer.Serialize(formula.Nodes!));
        Assert.Equal(JsonSerializer.Serialize(graph.Variables), JsonSerializer.Serialize(formula.Variables));
    }

    [Fact]
    public void Payload_Json_IsTheContract() {
        var spec = GroupedGraphSpec.FromLiterals([["x1", "!x2"]]);
        Assert.Equal(
            "{\"kind\":\"formula\",\"layout\":\"cnf\",\"shape\":\"assignment\",\"rule\":\"satisfiability\"," +
            "\"groups\":[{\"id\":\"c0\",\"label\":\"C1\",\"members\":[\"c0-0\",\"c0-1\"]}]," +
            "\"nodes\":[{\"id\":\"c0-0\",\"label\":\"x1\",\"variable\":\"v:x1\",\"positive\":true,\"group\":\"c0\"}," +
            "{\"id\":\"c0-1\",\"label\":\"¬x2\",\"variable\":\"v:x2\",\"positive\":false,\"group\":\"c0\"}]," +
            "\"variables\":[{\"id\":\"v:x1\",\"label\":\"x1\"},{\"id\":\"v:x2\",\"label\":\"x2\"}]}",
            JsonSerializer.Serialize(FormulaFrames.Payload(spec), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public void Payload_ARepeatedLiteral_IsOnePillPerOccurrence() {
        var p = FormulaFrames.Payload(GroupedGraphSpec.FromLiterals([["x1", "x1", "x2"]]));
        Assert.Equal(["c0-0", "c0-1", "c0-2"], p.Nodes!.Select(n => n.Id));
        Assert.Equal(["v:x1", "v:x1", "v:x2"], p.Nodes!.Select(n => n.Variable));
    }

    // ---- frames: the Grouped Graph's states ----

    public static TheoryData<string, bool, bool, bool?> Steps => new() {
        { "Try", false, false, null },
        { "Reject", false, false, null },
        { "Accept", false, false, null },
        { "Accept", false, true, null },
        { "Try", true, false, null },
        { "Reject", true, false, null },
        { "Backtrack", true, false, null },
        { "Done", true, false, true },
        { "Done", false, false, false },
    };

    [Theory]
    [MemberData(nameof(Steps))]
    public void Frame_LiteralsClausesAndAssignment_AreExactlyTheGroupedGraphs(string eventName, bool incremental, bool endsRun, bool? ok) {
        var ev = Enum.Parse<StepEvent>(eventName);
        var partial = Of(("x1", true), ("x2", false), ("x3", false));
        string[] focus = ["v:x2", "c1"];
        var formula = FormulaFrames.Frame(Spec, partial, ev, focus, "caption", ok, incremental, endsRun);
        var graph = GroupedGraphFrames.Frame(Spec, partial, ev, focus, "caption", ok, incremental, endsRun);
        Assert.Equal((graph.Event, graph.Caption, graph.Focus, graph.Ok), (formula.Event, formula.Caption, formula.Focus, formula.Ok));
        Assert.Equal(States(graph.Nodes), States(formula.Nodes));
        Assert.Equal(States(graph.Groups), States(formula.Groups));
        Assert.Equal(graph.Assignment, formula.Assignment);
    }

    // ---- frames: the variables ----

    [Fact]
    public void Variables_ATriedCandidate_TrueOnesHighlighted_FalseOnesFalse_UnassignedAbsent() {
        var f = FormulaFrames.Frame(Spec, Of(("x1", true), ("x2", false)), StepEvent.Try, [], "x");
        Assert.Equal(PictureState.ElementHighlight, Variable(f, "v:x1"));
        Assert.Equal(PictureState.False, Variable(f, "v:x2"));
        Assert.Null(Variable(f, "v:x3"));
        Assert.Null(Variable(f, "v:x4"));
    }

    [Fact]
    public void Variables_AnIncrementalRun_KeepsItsTrueVariablesCommitted_AndHighlightsOnlyTheFocus() {
        var f = FormulaFrames.Frame(Spec, Of(("x1", true), ("x2", true), ("x3", false)), StepEvent.Try, ["v:x2"], "x", incremental: true);
        Assert.Equal(PictureState.Committed, Variable(f, "v:x1"));
        Assert.Equal(PictureState.ElementHighlight, Variable(f, "v:x2"));
        Assert.Equal(PictureState.False, Variable(f, "v:x3"));
    }

    [Fact]
    public void Variables_TheFocusIsHighlighted_EvenWhenItIsFalse_OrUnassigned() {
        var f = FormulaFrames.Frame(Spec, Of(("x1", false)), StepEvent.Try, ["v:x1", "v:x4"], "x", incremental: true);
        Assert.Equal(PictureState.ElementHighlight, Variable(f, "v:x1"));
        Assert.Equal(PictureState.ElementHighlight, Variable(f, "v:x4"));
        // Its false literals stay False: the strip highlights the variable, the formula keeps the literal's value.
        Assert.Equal(PictureState.False, f.Nodes["c2-0"].State);
    }

    [Fact]
    public void Variables_AFocusThatIsNoVariable_MarksNoChip() {
        var f = FormulaFrames.Frame(Spec, Of(), StepEvent.Reject, ["c1", "c0-1", "v:x9"], "x");
        Assert.Empty(f.Variables);
    }

    [Fact]
    public void Variables_TheConfirmedAnswer_IsSolution_ACommittedAcceptIsNot() {
        var answer = Of(("x1", true), ("x2", true), ("x3", true), ("x4", true));
        var done = FormulaFrames.Frame(Spec, answer, StepEvent.Done, [], "x", ok: true);
        Assert.All(["v:x1", "v:x2", "v:x3", "v:x4"], id => Assert.Equal(PictureState.Solution, Variable(done, id)));
        var endsRun = FormulaFrames.Frame(Spec, answer, StepEvent.Accept, [], "x", endsRun: true);
        Assert.Equal(PictureState.Solution, Variable(endsRun, "v:x1"));
        var midRun = FormulaFrames.Frame(Spec, answer, StepEvent.Accept, [], "x");
        Assert.Equal(PictureState.Committed, Variable(midRun, "v:x1"));
    }

    [Fact]
    public void Variables_ADoneThatFoundNothing_RejectsItsTrueVariables_NeverSolution() {
        var f = FormulaFrames.Frame(Spec, Of(("x1", true), ("x2", false)), StepEvent.Done, [], "x", ok: false);
        Assert.Equal(PictureState.Rejected, Variable(f, "v:x1"));
        Assert.Equal(PictureState.False, Variable(f, "v:x2"));
        Assert.DoesNotContain(PictureState.Solution, f.Nodes.Values.Select(s => s.State));
    }

    [Fact]
    public void Frame_Json_HasTheGroupedGraphFieldsThenVariables_StepAlwaysWritten() {
        var f = FormulaFrames.Frame(Spec, Of(("x1", true)), StepEvent.Accept, [], "x");
        var json = JsonDocument.Parse(JsonSerializer.Serialize(f, FramesResponse.JsonOptions)).RootElement;
        Assert.Equal(["event", "caption", "focus", "nodes", "groups", "assignment", "variables", "step"], json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("step").ValueKind);
        var done = JsonDocument.Parse(JsonSerializer.Serialize(f with { Ok = true, Step = 3 }, FramesResponse.JsonOptions)).RootElement;
        Assert.Equal(["event", "caption", "focus", "nodes", "groups", "assignment", "variables", "step", "ok"], done.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Committed", done.GetProperty("variables").GetProperty("v:x1").GetProperty("state").GetString());
    }

    // ---- the whole response ----

    private static SolverStep<Assignment> Step(Assignment partial, StepEvent ev, bool? ok = null, params string[] focus) =>
        new(partial, ev, focus, ev.ToString()) { Ok = ok };

    private static FramesResponse Build(SolveRun run) =>
        FormulaFrames.Build(Spec, run, GroupedGraphFrames.ParseAssignment, _ => true);

    [Fact]
    public void Build_OneFramePerStep_EachSayingItsStep_AndNoSolvedFrameAfterADone() {
        var answer = Of(("x1", true), ("x2", true), ("x3", true), ("x4", true));
        var run = new SolveRun("(x1:True,x2:True,x3:True,x4:True)", [
            Step(Of(("x1", true)), StepEvent.Try, null, "v:x1"),
            Step(Of(("x1", true)), StepEvent.Accept),
            Step(Of(("x1", true), ("x2", true)), StepEvent.Try, null, "v:x2"),
            Step(answer, StepEvent.Done, true),
        ], typeof(Assignment)) { Incremental = true };
        var response = Build(run);
        Assert.Equal("Formula", response.Type);
        Assert.IsType<FormulaPayload>(response.Payload);
        var frames = response.Frames.Cast<FormulaFrame>().ToList();
        Assert.Equal([0, 1, 2, 3], frames.Select(f => f.Step));
        Assert.Equal(PictureState.ElementHighlight, frames[0].Variables["v:x1"].State);
        Assert.Equal(PictureState.Committed, frames[1].Variables["v:x1"].State);
        Assert.Equal(PictureState.Committed, frames[2].Variables["v:x1"].State);
        Assert.Equal(PictureState.ElementHighlight, frames[2].Variables["v:x2"].State);
        Assert.True(frames[3].Ok);
        Assert.Equal(PictureState.Solution, frames[3].Variables["v:x4"].State);
    }

    [Fact]
    public void Build_StepsOfAnotherShape_DrawTheAnswerAlone() {
        var run = new SolveRun("(x1:True,x2:False,x3:False,x4:True)", [new object()], null);
        var frame = Assert.IsType<FormulaFrame>(Assert.Single(Build(run).Frames));
        Assert.Null(frame.Step);
        Assert.Equal(StepEvent.Done, frame.Event);
        Assert.True(frame.Ok);
        Assert.Equal(PictureState.Solution, frame.Variables["v:x1"].State);
        Assert.Equal(PictureState.False, frame.Variables["v:x2"].State);
    }

    [Fact]
    public void Build_ARunStoppedByTheTimeLimit_EndsOnABlankDone() {
        var run = new SolveRun(SolveRun.TimedOutAnswer, [Step(Of(("x1", true)), StepEvent.Try, null, "v:x1")], typeof(Assignment)) { TimedOut = true };
        var frames = Build(run).Frames.Cast<FormulaFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var last = frames[^1];
        Assert.Equal((StepEvent.Done, false, (int?)null), (last.Event, last.Ok, last.Step));
        Assert.Empty(last.Nodes);
        Assert.Empty(last.Groups);
        Assert.Empty(last.Assignment);
        Assert.Empty(last.Variables);
    }

    [Fact]
    public void Build_TheSolversOwnTimeout_IsNeverReadAsAnAssignment() {
        var run = new SolveRun(GroupedGraphFrames.SolverTimeoutAnswer, [], typeof(Assignment));
        var frame = Assert.IsType<FormulaFrame>(Assert.Single(Build(run).Frames));
        Assert.Equal(GroupedGraphFrames.SolverTimeoutCaption, frame.Caption);
        Assert.False(frame.Ok);
        Assert.Empty(frame.Variables);
    }
}
