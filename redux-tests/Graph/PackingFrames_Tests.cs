using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Packing picture's base on its own: payload, ids (equal items stay distinct), containers per rule, the states of each
// frame, the answer-only Done frame and a stopped run. The solvers' steps and the endpoint are tested in
// PackingSolvers_Tests and PackingFrames_Endpoint_Tests.
public class PackingFrames_Tests {
    private static PackingSpec Sizes(PackingRule rule, params int[] sizes) =>
        new(sizes.Select(s => new PackingItemInput(s, null, s.ToString())).ToList(), rule);

    private static PackingSpec Knapsack(int w, int v, params (int Weight, int Value)[] items) =>
        new(items.Select(i => new PackingItemInput(i.Weight, i.Value, $"({i.Weight},{i.Value})")).ToList(), PackingRule.Knapsack) { Limit = w, AtLeast = v };

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static Placement P(params int[] assign) => new(assign);

    // ---- Payload ----

    [Fact]
    public void Payload_Partition_HasTwoSidesThatMustEachHoldHalf() {
        var payload = PackingFrames.Payload(Sizes(PackingRule.Partition, 3, 5, 7));
        Assert.Equal(("packing", "partition", 15), (payload.Kind, payload.Rule, payload.Total));
        Assert.Equal(["I1 3 3", "I2 5 5", "I3 7 7"], payload.Items.Select(i => $"{i.Id} {i.Label} {i.Size}"));
        Assert.Equal(["C1 S1 7.5", "C2 S2 7.5"], payload.Containers.Select(c => $"{c.Id} {c.Label} {c.Target?.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        Assert.Null(payload.MaxBins);
    }

    [Fact]
    public void Payload_EachRule_HasItsOwnContainersAndLines() {
        var sum = PackingFrames.Payload(Sizes(PackingRule.SubsetSum, 1, 7) with { Target = 8 });
        Assert.Equal("C1 Chosen 8 - -", Describe(Assert.Single(sum.Containers)));
        var knapsack = PackingFrames.Payload(Knapsack(50, 220, (10, 60), (20, 100)));
        Assert.Equal("C1 Knapsack - 50 220", Describe(Assert.Single(knapsack.Containers)));
        Assert.Equal([60, 100], knapsack.Items.Select(i => i.Value!.Value));
        Assert.Equal(["(10,60)", "(20,100)"], knapsack.Items.Select(i => i.Label));
        var bins = PackingFrames.Payload(Sizes(PackingRule.BinPacking, 4, 7, 3) with { Limit = 10, Bins = 2 });
        Assert.Equal(["C1 Bin 1 - 10 -", "C2 Bin 2 - 10 -"], bins.Containers.Select(Describe));
        Assert.Equal(2, bins.MaxBins);
        static string Describe(PackingContainerDto c) => $"{c.Id} {c.Label} {c.Target?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} {c.Limit?.ToString() ?? "-"} {c.AtLeast?.ToString() ?? "-"}";
    }

    [Fact]
    public void Payload_BinPacking_DrawsNoMoreBinsThanItems_NorThanK() {
        Assert.Equal(3, PackingFrames.Payload(Sizes(PackingRule.BinPacking, 1, 2, 3) with { Limit = 5, Bins = 1000 }).Containers.Count);
        var none = PackingFrames.Payload(Sizes(PackingRule.BinPacking, 1, 2) with { Limit = 5, Bins = 0 });
        Assert.Empty(none.Containers);
        Assert.Equal(0, none.MaxBins);
        Assert.Empty(PackingFrames.Payload(Sizes(PackingRule.BinPacking) with { Limit = 5, Bins = 3 }).Containers);
    }

    [Fact]
    public void Ids_ArePositions_SoEqualItemsStayDistinct() {
        var payload = PackingFrames.Payload(Sizes(PackingRule.BinPacking, 4, 4, 4) with { Limit = 8, Bins = 2 });
        Assert.Equal(["I1", "I2", "I3"], payload.Items.Select(i => i.Id));
        Assert.Equal(["4", "4", "4"], payload.Items.Select(i => i.Label));
        var frame = PackingFrames.Frame(Sizes(PackingRule.BinPacking, 4, 4, 4) with { Limit = 8, Bins = 2 }, P(0, 1, 0), StepEvent.Accept, ["I3"], "c");
        Assert.Equal([0, 1, 0], frame.Assign);
        Assert.Equal([8, 4], frame.Totals.Select(t => t.Size));
        Assert.Equal("I1:Solution I2:Solution I3:ElementHighlight", States(frame.Items));
    }

    [Fact]
    public void Payload_Json_HasTheContractFieldsInOrder_AndOnlyTheLinesThatApply() {
        using var doc = JsonDocument.Parse(Json(PackingFrames.Payload(Knapsack(50, 220, (10, 60)))));
        Assert.Equal(["kind", "rule", "items", "containers", "total"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "label", "size", "value"], doc.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "label", "limit", "atLeast"], doc.RootElement.GetProperty("containers")[0].EnumerateObject().Select(p => p.Name));
        using var bins = JsonDocument.Parse(Json(PackingFrames.Payload(Sizes(PackingRule.BinPacking, 4) with { Limit = 10, Bins = 1 })));
        Assert.Equal(["kind", "rule", "items", "containers", "total", "maxBins"], bins.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "label", "size"], bins.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name));
        using var half = JsonDocument.Parse(Json(PackingFrames.Payload(Sizes(PackingRule.Partition, 3, 5, 7))));
        Assert.Equal(7.5, half.RootElement.GetProperty("containers")[0].GetProperty("target").GetDouble());
    }

    // ---- Frames ----

    [Fact]
    public void Frame_Json_HasTheContractFields_AndOkOnlyWhenSet() {
        var spec = Knapsack(50, 220, (10, 60), (20, 100));
        using var doc = JsonDocument.Parse(Json(PackingFrames.Frame(spec, P(0), StepEvent.Try, ["I1"], "c")));
        Assert.Equal(["event", "caption", "focus", "items", "containers", "assign", "totals"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[{\"size\":10,\"value\":60}]", JsonSerializer.Serialize(doc.RootElement.GetProperty("totals")));
        Assert.Equal("[0,-1]", JsonSerializer.Serialize(doc.RootElement.GetProperty("assign")));
        using var done = JsonDocument.Parse(Json(PackingFrames.Frame(spec, P(), StepEvent.Done, [], "c", ok: false)));
        Assert.False(done.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void WholeCandidateTry_HighlightsItsItems_AndJudgesItsContainers() {
        // Partition brute force tries S1 = {3}: 3 against 12.
        var spec = Sizes(PackingRule.Partition, 3, 5, 7);
        var frame = PackingFrames.Frame(spec, P(0, 1, 1), StepEvent.Reject, ["I1"], "c");
        Assert.Equal("I1:ElementHighlight I2:ElementHighlight I3:ElementHighlight", States(frame.Items));
        Assert.Equal("C1:Rejected C2:Rejected", States(frame.Containers));
        Assert.Equal([3, 12], frame.Totals.Select(t => t.Size));
    }

    [Fact]
    public void Accept_AndDone_DrawTheAnswerAsSolution_LeftOutItemsFaded_MetLinesCovered() {
        var spec = Sizes(PackingRule.SubsetSum, 1, 7, 12, 15) with { Target = 28 };
        var accept = PackingFrames.Frame(spec, P(0, -1, 0, 0), StepEvent.Accept, ["I1", "I3", "I4"], "c");
        Assert.Equal("I1:ElementHighlight I3:ElementHighlight I4:ElementHighlight", States(accept.Items));
        Assert.Equal("C1:Covered", States(accept.Containers));
        var done = PackingFrames.Frame(spec, P(0, -1, 0, 0), StepEvent.Done, [], "c", ok: true);
        Assert.Equal("I1:Solution I2:Untraveled I3:Solution I4:Solution", States(done.Items));
        Assert.Equal("C1:Covered", States(done.Containers));
    }

    [Fact]
    public void DoneWithNoAnswer_IsBlank() {
        var spec = Sizes(PackingRule.SubsetSum, 4, 6, 10) with { Target = 7 };
        var done = PackingFrames.Frame(spec, P(), StepEvent.Done, [], "c", ok: false);
        Assert.Empty(done.Items);
        Assert.Empty(done.Containers);
        Assert.Equal([-1, -1, -1], done.Assign);
    }

    [Fact]
    public void TheEmptySetAsACorrectAnswer_CoversTheTarget_AndFadesEveryItem() {
        // Subset Sum with T = 0: {} is the answer.
        var done = PackingFrames.Frame(Sizes(PackingRule.SubsetSum, 1, 2) with { Target = 0 }, P(), StepEvent.Done, [], "c", ok: true);
        Assert.Equal("I1:Untraveled I2:Untraveled", States(done.Items));
        Assert.Equal("C1:Covered", States(done.Containers));
    }

    [Fact]
    public void Knapsack_OverTheLimit_IsRejectedInEveryFrame_ShortOfTheGoal_OnlyWhenJudged() {
        var spec = Knapsack(50, 220, (10, 60), (20, 100), (30, 120));
        Assert.Equal("C1:Rejected", States(PackingFrames.Frame(spec, P(0, 0, 0), StepEvent.Accept, [], "c", incremental: true).Containers));
        Assert.Equal("C1:Rejected", States(PackingFrames.Frame(spec, P(0, 0), StepEvent.Reject, [], "c").Containers));
        Assert.Empty(PackingFrames.Frame(spec, P(0, 0), StepEvent.Accept, [], "c", incremental: true).Containers);
        Assert.Empty(PackingFrames.Frame(spec, P(0, 0), StepEvent.Try, [], "c", incremental: true).Containers);
        // An incremental run's Reject turns down one piece of an answer still being built (a backward map leaving an item
        // out): the goal not reached yet is not a miss.
        Assert.Empty(PackingFrames.Frame(spec, P(0, 0), StepEvent.Reject, ["I3"], "c", incremental: true).Containers);
        Assert.Equal("C1:Covered", States(PackingFrames.Frame(spec, P(-1, 0, 0), StepEvent.Accept, [], "c").Containers));
        // The empty candidate is not judged: nothing is placed.
        Assert.Empty(PackingFrames.Frame(spec, P(), StepEvent.Reject, [], "c").Containers);
    }

    [Fact]
    public void Bins_ThatHoldSomethingWithinC_AreCovered_EmptyBinsAreBackground_FullerThanCRejected() {
        var spec = Sizes(PackingRule.BinPacking, 6, 6, 3) with { Limit = 10, Bins = 3 };
        var frame = PackingFrames.Frame(spec, P(0, 0, 1), StepEvent.Try, [], "c");
        Assert.Equal("C1:Rejected C2:Covered", States(frame.Containers));
        Assert.Equal([12, 3, 0], frame.Totals.Select(t => t.Size));
        Assert.All(frame.Totals, t => Assert.Null(t.Value));
    }

    [Fact]
    public void IncrementalSteps_KeepWhatIsCommittedSolution_AndMarkOnlyTheFocus() {
        // Bin Packing brute force: 4 and 7 are placed, and no bin has room for 6 (C = 10, K = 2).
        var spec = Sizes(PackingRule.BinPacking, 4, 7, 6) with { Limit = 10, Bins = 2 };
        var reject = PackingFrames.Frame(spec, P(0, 1), StepEvent.Reject, ["I3"], "c", incremental: true);
        Assert.Equal("I1:Solution I2:Solution I3:Rejected", States(reject.Items));
        Assert.Equal("C1:Covered C2:Covered", States(reject.Containers));
        var accept = PackingFrames.Frame(spec, P(0, 1, 0), StepEvent.Accept, ["I3", "C1"], "c", incremental: true);
        Assert.Equal("I1:Solution I2:Solution I3:ElementHighlight", States(accept.Items));
        Assert.Equal("C1:ElementHighlight C2:Covered", States(accept.Containers));
        var back = PackingFrames.Frame(spec, P(0), StepEvent.Backtrack, ["I2"], "c", incremental: true);
        Assert.Equal("I1:Solution I2:ElementHighlight", States(back.Items));
        // A whole candidate's Reject only highlights its focus.
        Assert.Equal("ElementHighlight", PackingFrames.Frame(spec, P(0, 1), StepEvent.Reject, ["I3"], "c").Items["I3"].State.ToString());
    }

    [Fact]
    public void IdsThePictureDoesNotHave_AreIgnored() {
        var spec = Sizes(PackingRule.BinPacking, 4, 4) with { Limit = 10, Bins = 1 };
        // Container 1 does not exist (one bin), and I9 and C7 are not ids of the picture.
        var frame = PackingFrames.Frame(spec, P(0, 1, 0), StepEvent.Accept, ["I9", "C7", "x"], "c", incremental: true);
        Assert.Equal([0, -1], frame.Assign);
        Assert.Equal("I1:Solution", States(frame.Items));
        Assert.Equal("C1:Covered", States(frame.Containers));
    }

    [Fact]
    public void Placement_DropsTrailingLeftOuts_SoEqualPlacementsAreEqual() {
        Assert.Equal(P(), P(-1, -1));
        Assert.Equal(P(0, -1), P(0, -5, -1));
        Assert.NotEqual(P(0), P(1));
        Assert.Equal(-1, P(0).Of(4));
        Assert.Equal(new Placement([]), Shapes.EmptyAnswer(typeof(Placement)));
    }

    // ---- Reading answers ----

    [Fact]
    public void FromGroups_FillsRepeatedNumbersInOrder_AndSkipsUnknownOnes() {
        var placement = PackingFrames.FromGroups(["4", "6", "4", "2"], PackingFrames.Groups("((4,6),(4),(9))", nested: true));
        Assert.Equal([0, 0, 1], placement.Assign);
        Assert.Equal([0, -1, 0], PackingFrames.FromGroups(["(20,100)", "(10,60)", "(30,120)"], PackingFrames.Groups("{(30,120),(20,100)}", nested: false)).Assign);
        Assert.Empty(PackingFrames.FromGroups(["1"], PackingFrames.Groups("not an answer {", nested: true)).Assign);
        Assert.Empty(PackingFrames.Groups("{}", nested: true));
    }

    // ---- The whole response ----

    [Fact]
    public void Build_AnAnswerWithoutSteps_IsDrawnFromTheAnswer() {
        var spec = Sizes(PackingRule.SubsetSum, 1, 7, 12, 15) with { Target = 28 };
        var run = new SolveRun("{1,12,15}", [], null);
        var response = PackingFrames.Build(spec, run, _ => P(0, -1, 0, 0), _ => true);
        Assert.Equal("Packing", response.Type);
        var done = Assert.IsType<PackingFrame>(Assert.Single(response.Frames));
        Assert.Equal((StepEvent.Done, true, "The answer is {1,12,15}."), (done.Event, done.Ok, done.Caption));
        Assert.Equal([0, -1, 0, 0], done.Assign);

        var wrong = PackingFrames.Build(spec, new SolveRun("{7}", [], null), _ => P(-1, 0), _ => false);
        var blank = Assert.IsType<PackingFrame>(Assert.Single(wrong.Frames));
        Assert.Equal(("No answer was found.", false), (blank.Caption, blank.Ok));
        Assert.Empty(blank.Items);
    }

    [Fact]
    public void Build_AStoppedRun_EndsWithABlankNotOkDone_AndNeverReadsItsAnswer() {
        var spec = Sizes(PackingRule.Partition, 3, 5, 7);
        object[] steps = [
            new SolverStep<Placement>(P(0, 1, 1), StepEvent.Reject, ["I1"], "Try S1 = {3}."),
            new SolverStep<Placement>(new Placement([]), StepEvent.Done, [], "Stopped after 0.05 s and 1 step; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(Placement)) { TimedOut = true };
        var response = PackingFrames.Build(spec, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("verified"));
        var done = Assert.IsType<PackingFrame>(response.Frames[^1]);
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Items);
        Assert.Empty(done.Containers);
        Assert.Equal([-1, -1, -1], done.Assign);
        Assert.All(done.Totals, t => Assert.Equal(0, t.Size));

        // Steps of another shape cannot be drawn: the frame is added, with the generic caption.
        var other = new SolveRun(SolveRun.TimedOutAnswer, [new SolverStep<NodeSet>(new NodeSet([]), StepEvent.Done, [], "x") { Ok = false }], typeof(NodeSet)) { TimedOut = true };
        var added = Assert.IsType<PackingFrame>(Assert.Single(PackingFrames.Build(spec, other, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, added.Caption);
    }
}
