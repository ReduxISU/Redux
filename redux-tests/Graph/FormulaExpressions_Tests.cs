using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the payload-driven Formula's JSON shapes (#929): layouts, expression trees, typed variables, group notes and
// replaced expressions. Every field #929 added is optional and must be left out of the JSON when unset; the serializer is
// the frames endpoint's own (FramesResponse.JsonOptions, whose default ignore condition is Never), so only the fields'
// own attributes keep them out.
public class FormulaExpressions_Tests {
    private static readonly JsonSerializerOptions Options = new(FramesResponse.JsonOptions) {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Json(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);

    [Fact]
    public void TheOperatorTable_IsTheContractsNames() {
        Assert.Equal(["and", "eq", "ge", "gt", "le", "lt", "minus", "mul", "neg", "not", "or", "plus", "sum", "times"],
            FormulaOps.Names.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void AnExpression_WritesOnlyItsOwnKindsFields() {
        Assert.Equal("{\"id\":\"x\",\"var\":\"v:x1\"}", Json(FormulaExpr.VarRef("x", "v:x1")));
        Assert.Equal("{\"id\":\"n\",\"num\":7}", Json(FormulaExpr.Number("n", 7)));
        Assert.Equal("{\"id\":\"n\",\"num\":-2,\"variable\":\"v:x2\"}", Json(FormulaExpr.Number("n", -2, "v:x2")));
        Assert.Equal("{\"id\":\"rest\",\"label\":\"?\",\"variable\":\"v:rest\"}", Json(FormulaExpr.Placeholder("rest", "?", "v:rest")));
        Assert.Equal("{\"id\":\"s\",\"op\":\"plus\",\"args\":[{\"id\":\"a\",\"num\":1},{\"id\":\"b\",\"var\":\"v:b\"}]}",
            Json(FormulaExpr.Operator("s", FormulaOps.Plus, FormulaExpr.Number("a", 1), FormulaExpr.VarRef("b", "v:b"))));
        Assert.Equal("{\"op\":\"not\",\"args\":[{\"id\":\"x\",\"var\":\"v:x\"}]}",
            Json(new FormulaExpr { Op = FormulaOps.Not, Args = [FormulaExpr.VarRef("x", "v:x")] }));
    }

    [Fact]
    public void Pieces_AreEveryNodeDepthFirst() {
        var e = FormulaExpr.Operator("eq", FormulaOps.Eq, FormulaExpr.Operator("s", FormulaOps.Sum, FormulaExpr.Number("s0", 1), FormulaExpr.Number("s1", 2)), FormulaExpr.Number("T", 3));
        Assert.Equal(["eq", "s", "s0", "s1", "T"], e.Pieces().Select(p => p.Id));
    }

    [Fact]
    public void ARowsPayload_WritesItsLayoutFields_AndExpressionGroups_WithoutNodes() {
        var payload = new FormulaPayload("formula", FormulaLayouts.Rows, "assignment", "constraints",
            [new FormulaGroupDto("r0", "R1") {
                Expr = FormulaExpr.Operator("r0-rel", FormulaOps.Le,
                    FormulaExpr.Operator("r0-s0", FormulaOps.Plus,
                        FormulaExpr.Operator("r0-m0", FormulaOps.Mul, FormulaExpr.Number("r0-c0", 2, "v:x1"), FormulaExpr.VarRef("r0-x0", "v:x1")),
                        FormulaExpr.VarRef("r0-x1", "v:x2")),
                    FormulaExpr.Number("r0-d", 5)),
            }],
            null,
            [FormulaVariableDto.Integer("v:x1", "x1", 0, 1), FormulaVariableDto.Integer("v:x2", "x2", 0, 1)]) {
            Outer = FormulaOps.And, Arrange = FormulaLayouts.Stack, Noun = "constraint",
        };
        Assert.Equal(
            "{\"kind\":\"formula\",\"layout\":\"rows\",\"shape\":\"assignment\",\"rule\":\"constraints\"," +
            "\"groups\":[{\"id\":\"r0\",\"label\":\"R1\",\"expr\":{\"id\":\"r0-rel\",\"op\":\"le\",\"args\":[" +
            "{\"id\":\"r0-s0\",\"op\":\"plus\",\"args\":[{\"id\":\"r0-m0\",\"op\":\"mul\",\"args\":[{\"id\":\"r0-c0\",\"num\":2,\"variable\":\"v:x1\"},{\"id\":\"r0-x0\",\"var\":\"v:x1\"}]}," +
            "{\"id\":\"r0-x1\",\"var\":\"v:x2\"}]},{\"id\":\"r0-d\",\"num\":5}]}}]," +
            "\"variables\":[{\"id\":\"v:x1\",\"label\":\"x1\",\"type\":\"int\",\"range\":[0,1]},{\"id\":\"v:x2\",\"label\":\"x2\",\"type\":\"int\",\"range\":[0,1]}]," +
            "\"outer\":\"and\",\"arrange\":\"stack\",\"noun\":\"constraint\"}",
            Json(payload));
    }

    [Fact]
    public void ACnfPayload_WritesNoneOfTheNewFields() {
        string json = Json(FormulaFrames.Payload(GroupedGraphSpec.FromLiterals([["x1", "!x2"], ["x2"]])));
        foreach (var field in new[] { "outer", "inner", "arrange", "noun", "expr", "type", "range", "exprs", "note" })
            Assert.DoesNotContain($"\"{field}\"", json);
    }

    [Fact]
    public void AnIntegerVariableWithoutARange_WritesItsTypeOnly() {
        Assert.Equal("{\"id\":\"v:i\",\"label\":\"i\",\"type\":\"int\"}", Json(FormulaVariableDto.Integer("v:i", "i")));
    }

    [Fact]
    public void AFrame_WritesNotesNumbersAndReplacedExpressions_AndLeavesThemOutWhenUnset() {
        var frame = new FormulaFrameDraft()
            .Node("s0", PictureState.ElementHighlight).Node("s1", PictureState.False).Node("s0", PictureState.False)
            .Group("e0", PictureState.Rejected, "sum 1 ≠ 28")
            .Value("v:x1", 1).Value("v:x2", 0).Value("v:b", true)
            .Var("v:x1", PictureState.ElementHighlight).Var("v:x2", PictureState.False)
            .Expr("e0", FormulaExpr.Number("n", 60))
            .ToFrame(StepEvent.Reject, "Try {1}", ["s0"]);
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"Try {1}\",\"focus\":[\"s0\"]," +
            "\"nodes\":{\"s0\":{\"state\":\"ElementHighlight\"},\"s1\":{\"state\":\"False\"}}," +
            "\"groups\":{\"e0\":{\"state\":\"Rejected\",\"note\":\"sum 1 ≠ 28\"}}," +
            "\"assignment\":{\"v:b\":true,\"v:x1\":1,\"v:x2\":0}," +
            "\"variables\":{\"v:x1\":{\"state\":\"ElementHighlight\"},\"v:x2\":{\"state\":\"False\"}}," +
            "\"step\":null,\"exprs\":{\"e0\":{\"id\":\"n\",\"num\":60}}}",
            Json(frame));
        var plain = new FormulaFrameDraft().Group("e0", PictureState.Covered).ToFrame(StepEvent.Done, "ok", [], ok: true);
        Assert.Equal("{\"event\":\"Done\",\"caption\":\"ok\",\"focus\":[],\"nodes\":{},\"groups\":{\"e0\":{\"state\":\"Covered\"}},\"assignment\":{},\"variables\":{},\"step\":null,\"ok\":true}",
            Json(plain));
    }

    [Fact]
    public void AFrameNotDone_NeverWritesOk() {
        Assert.Null(new FormulaFrameDraft().ToFrame(StepEvent.Accept, "x", [], ok: true).Ok);
    }

    // ---- the whole response ----

    private static readonly FormulaPayload Payload = new("formula", FormulaLayouts.Equation, "values", "test",
        [new FormulaGroupDto("e0", "") { Expr = FormulaExpr.Number("n", 1) }], null, []);

    private static FramesResponse Build(SolveRun run) => FormulaExprFrames.Build<Assignment>(Payload, run,
        (step, endsRun) => new FormulaFrameDraft().Group("e0", endsRun ? PictureState.Solution : PictureState.ElementHighlight).ToFrame(step.Event, step.Caption, step.Focus, step.Ok),
        answer => new FormulaFrameDraft().Group("e0", PictureState.Covered).ToFrame(StepEvent.Done, "answer " + answer, [], ok: true));

    private static SolverStep<Assignment> Step(StepEvent ev, bool? ok = null) => new(new Assignment([]), ev, [], ev.ToString()) { Ok = ok };

    [Fact]
    public void Build_DrawsEveryStep_MarksTheAcceptThatEndsTheRun_AndAddsNoSolvedFrameAfterADone() {
        var run = new SolveRun("x", [Step(StepEvent.Reject), Step(StepEvent.Accept), Step(StepEvent.Done, true)], typeof(Assignment));
        var response = Build(run);
        Assert.Equal("Formula", response.Type);
        Assert.Same(Payload, response.Payload);
        var frames = response.Frames.Cast<FormulaExprFrame>().ToList();
        Assert.Equal([0, 1, 2], frames.Select(f => f.Step!.Value));
        Assert.Equal(PictureState.Solution, frames[1].Groups["e0"].State);
        Assert.Equal(PictureState.ElementHighlight, frames[0].Groups["e0"].State);
    }

    [Fact]
    public void Build_ARunWithoutSteps_EndsOnTheSolvedFrame_WithNoStep() {
        var frame = Assert.IsType<FormulaExprFrame>(Assert.Single(Build(new SolveRun("(2,3)", [], typeof(Assignment))).Frames));
        Assert.Equal("answer (2,3)", frame.Caption);
        Assert.Null(frame.Step);
    }

    [Theory]
    [InlineData(true, "x")]
    [InlineData(false, "TIMEOUT")]
    public void Build_AStoppedRun_EndsOnABlankDone_AndNeverReadsTheAnswer(bool timedOut, string answer) {
        var run = new SolveRun(answer, [Step(StepEvent.Reject)], typeof(Assignment)) { TimedOut = timedOut };
        var frames = Build(run).Frames.Cast<FormulaExprFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var last = frames[^1];
        Assert.Equal(StepEvent.Done, last.Event);
        Assert.False(last.Ok);
        Assert.Empty(last.Groups);
        Assert.Empty(last.Assignment);
        Assert.Null(last.Exprs);
    }
}
