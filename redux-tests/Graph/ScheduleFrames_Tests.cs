using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Schedule picture's base on its own: payloads, ids, the states of each frame, the exact pump simulation, reading
// answers, the blank Done frame of a stopped run and the solved frame of a run whose steps cannot be drawn. The solvers'
// steps and the endpoint are tested in ScheduleSolvers_Tests and ScheduleFrames_Endpoint_Tests.
public class ScheduleFrames_Tests {
    // Three jobs: J0 takes 4 and is due at 4, J1 takes 2 and is due at 3, J2 is J1 again (equal jobs stay two jobs).
    private static readonly JobScheduleSpec Jobs3 = new([new(4, 4, 5), new(2, 3, 1), new(2, 3, 1)], 2);

    // Two pumps and a 200-gallon tank at 50; 10 gallons of demand an hour, hours 1 and 2 at the peak tariff.
    private static readonly PumpScheduleSpec Tank = new(ScheduleRule.CostMinimization, 200, 50, 0,
        Enumerable.Repeat(10.0, 24).ToList(), new HashSet<int> { 1, 2 }, 0.5, 0.25,
        [new SchedulePump("A", 20, 2, 1), new SchedulePump("A", 60, 4, 3)]);

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static PumpPlan Plan(params string[][] on) => new([], on, null);

    // Every hour as given, the rest of the day with no pump on.
    private static string[][] Day(params string[][] first) => first.Concat(Enumerable.Repeat(Array.Empty<string>(), 24 - first.Length)).ToArray();

    // ---- Ids ----

    [Fact]
    public void Ids_ArePositions() {
        Assert.Equal(["J0", "J9", "P1", "P3", "t0", "t23", "P1t0", "P2t23"],
            [ScheduleFrames.JobId(0), ScheduleFrames.JobId(9), ScheduleFrames.PumpId(0), ScheduleFrames.PumpId(2),
             ScheduleFrames.HourId(0), ScheduleFrames.HourId(23), ScheduleFrames.CellId(0, 0), ScheduleFrames.CellId(1, 23)]);
        Assert.Equal(["jobSequencing", "costMinimization", "emergencyResilience"],
            Enum.GetValues<ScheduleRule>().Select(ScheduleFrames.RuleName));
    }

    // ---- Job Sequencing ----

    [Fact]
    public void JobsPayload_Json_HasTheContractFields_AndEqualJobsStayApart() {
        Assert.Equal(
            "{\"kind\":\"schedule\",\"rule\":\"jobSequencing\",\"jobs\":[{\"id\":\"J0\",\"label\":\"Job 0\",\"time\":4,\"deadline\":4,\"penalty\":5},"
            + "{\"id\":\"J1\",\"label\":\"Job 1\",\"time\":2,\"deadline\":3,\"penalty\":1},{\"id\":\"J2\",\"label\":\"Job 2\",\"time\":2,\"deadline\":3,\"penalty\":1}],"
            + "\"k\":2,\"span\":8}",
            JsonSerializer.Serialize(JsonDocument.Parse(Json(ScheduleFrames.JobsPayload(Jobs3))).RootElement));
        // The axis reaches the latest deadline when that is later than every job back to back; no jobs is an empty axis.
        Assert.Equal(20, ScheduleFrames.JobsPayload(new([new(1, 20, 1)], 0)).Span);
        Assert.Equal(0, ScheduleFrames.JobsPayload(new([], 0)).Span);
    }

    [Fact]
    public void JobsFrame_Json_HasTheContractFieldsInOrder_AndOkOnlyOnDone() {
        var frame = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1, 0]), StepEvent.Reject, ["J0"], "Late.");
        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["event", "caption", "focus", "order", "runs", "jobs", "penalty", "step"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[{\"job\":\"J1\",\"start\":0,\"end\":2,\"late\":false},{\"job\":\"J0\",\"start\":2,\"end\":6,\"late\":true}]",
            JsonSerializer.Serialize(doc.RootElement.GetProperty("runs")));
        var done = ScheduleFrames.JobsFrame(Jobs3, new JobOrder([]), StepEvent.Done, [], "None.", ok: false);
        Assert.False(JsonDocument.Parse(Json(done)).RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void JobsFrame_AWholeOrderTried_IsHighlighted_AndItsLateJobsRejected() {
        // J1 then J0 then J2: J0 ends at 6 (due 4) and J2 at 8 (due 3), so both are late: penalty 5 + 1.
        var frame = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1, 0, 2]), StepEvent.Reject, ["J0", "J2"], "Late.");
        Assert.Equal("J0:Rejected J1:ElementHighlight J2:Rejected", States(frame.Jobs));
        Assert.Equal(["J1", "J0", "J2"], frame.Order);
        Assert.Equal(6, frame.Penalty);
        Assert.Null(frame.Ok);
        // The same order accepted: on-time jobs are kept (Committed, #886), or the answer when the Accept ends the run; a late
        // job is still Rejected (its penalty counts).
        var accepted = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1, 0, 2]), StepEvent.Accept, [], "Fits.");
        Assert.Equal("J0:Rejected J1:Committed J2:Rejected", States(accepted.Jobs));
        var answer = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1, 0, 2]), StepEvent.Accept, [], "Fits.", endsRun: true);
        Assert.Equal("J0:Rejected J1:Solution J2:Rejected", States(answer.Jobs));
        // A run that ends without an answer never shows green (#886).
        var failed = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1, 0, 2]), StepEvent.Done, [], "None.", ok: false);
        Assert.Equal("J0:Rejected J1:Rejected J2:Rejected", States(failed.Jobs));
    }

    [Fact]
    public void PumpsFrame_AFailedDone_DrawsItsPumpsRejected() {
        // A run that ends without an answer never shows green (#886).
        var done = ScheduleFrames.PumpsFrame(Tank, new PumpPlan([], Day(["P1"]), null), StepEvent.Done, [], "None.", ok: false, incremental: true);
        Assert.NotEmpty(done.Cells);
        Assert.All(done.Cells, c => Assert.Equal(PictureState.Rejected, c.Value.State));
    }

    [Fact]
    public void JobsFrame_AnIncrementalRun_KeepsWhatItPlacedCommitted_AndMarksOnlyTheFocus() {
        var tried = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([2, 1]), StepEvent.Try, ["J2"], "Try J2.", incremental: true);
        Assert.Equal("J1:Rejected J2:ElementHighlight", States(tried.Jobs));
        // A tried job that would end late shows Rejected: that outranks the highlight.
        var late = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1, 2]), StepEvent.Try, ["J2"], "Try J2.", incremental: true);
        Assert.Equal("J1:Committed J2:Rejected", States(late.Jobs));
        var rejected = ScheduleFrames.JobsFrame(Jobs3, ScheduleFrames.Order([1]), StepEvent.Reject, ["J0"], "No room.", incremental: true);
        Assert.Equal("J0:Rejected J1:Committed", States(rejected.Jobs));
    }

    [Fact]
    public void JobsFrame_DropsIdsThatAreNotJobs_AndRepeats() {
        var frame = ScheduleFrames.JobsFrame(Jobs3, new JobOrder(["J1", "J8", "J1", "x"]), StepEvent.Accept, ["J8"], "Odd.");
        Assert.Equal(["J1"], frame.Order);
        Assert.Equal("J1:Committed", States(frame.Jobs));
    }

    [Theory]
    [InlineData(3, "(1,0,2)", "J1 J0 J2")]
    [InlineData(3, " ( 2 , 1 , 0 ) ", "J2 J1 J0")]
    [InlineData(0, "()", "")]
    [InlineData(3, "{}", "")]
    [InlineData(3, "()", "")]
    [InlineData(3, "(0,1)", "")]          // not every job
    [InlineData(3, "(0,1,1)", "")]        // a repeat
    [InlineData(3, "(0,1,3)", "")]        // no job 3
    [InlineData(3, "(0,1,-2)", "")]
    [InlineData(3, "(a,b,c)", "")]
    public void ParseJobOrder_ReadsAnOrderOfEveryJob_ElseEmpty(int jobs, string answer, string expected) =>
        Assert.Equal(expected, string.Join(" ", ScheduleFrames.ParseJobOrder(jobs, answer).Order));

    [Fact]
    public void BuildJobs_AStoppedRun_EndsBlank_AndNeverReadsItsAnswer() {
        var steps = new List<object> {
            new SolverStep<JobOrder>(ScheduleFrames.Order([0, 1, 2]), StepEvent.Reject, ["J0"], "Try (0,1,2)."),
            new SolverStep<JobOrder>(new JobOrder([]), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        };
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(JobOrder)) { TimedOut = true };
        var response = ScheduleFrames.BuildJobs(Jobs3, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("verified"));
        Assert.Equal("Schedule", response.Type);
        var done = (ScheduleJobsFrame)response.Frames[^1];
        Assert.Equal(2, response.Frames.Count);
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.Empty(done.Order);
        Assert.Empty(done.Runs);
        Assert.Empty(done.Jobs);
        Assert.Equal(0, done.Penalty);
        Assert.StartsWith("Stopped after", done.Caption);

        // A stopped run whose steps cannot be drawn still ends blank.
        var other = new SolveRun(SolveRun.TimedOutAnswer, [], typeof(NodeSet)) { TimedOut = true };
        var only = Assert.Single(ScheduleFrames.BuildJobs(Jobs3, other, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).Frames);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, ((ScheduleJobsFrame)only).Caption);
    }

    [Fact]
    public void BuildJobs_ARunWithoutDrawableSteps_EndsWithTheVerifiedAnswer() {
        var legacy = new SolveRun("(1,2,0)", ["legacy"], null);
        var frames = ScheduleFrames.BuildJobs(Jobs3, legacy, a => ScheduleFrames.ParseJobOrder(3, a), _ => true).Frames;
        var done = (ScheduleJobsFrame)Assert.Single(frames);
        Assert.Equal((StepEvent.Done, true), (done.Event, done.Ok));
        Assert.Equal(["J1", "J2", "J0"], done.Order);
        Assert.Equal("J0:Rejected J1:Solution J2:Rejected", States(done.Jobs));
        // An answer the verifier refuses is drawn as no answer.
        var refused = (ScheduleJobsFrame)Assert.Single(ScheduleFrames.BuildJobs(Jobs3, new SolveRun("{}", [], null), a => ScheduleFrames.ParseJobOrder(3, a), _ => false).Frames);
        Assert.Equal((false, "No answer was found."), (refused.Ok!.Value, refused.Caption));
        Assert.Empty(refused.Order);
    }

    // ---- Pump Scheduling ----

    [Fact]
    public void PumpsPayload_Json_HasTheContractFields_AndABudgetOnlyForEmergencyResilience() {
        using var doc = JsonDocument.Parse(Json(ScheduleFrames.PumpsPayload(Tank)));
        var root = doc.RootElement;
        Assert.Equal(["kind", "rule", "hours", "pumps", "tank", "rates"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("schedule", "costMinimization"), (root.GetProperty("kind").GetString(), root.GetProperty("rule").GetString()));
        Assert.Equal(24, root.GetProperty("hours").GetArrayLength());
        Assert.Equal("{\"id\":\"t1\",\"label\":\"01:00\",\"demand\":10,\"peak\":true}", JsonSerializer.Serialize(root.GetProperty("hours")[1]));
        Assert.Equal("{\"id\":\"t23\",\"label\":\"23:00\",\"demand\":10,\"peak\":false}", JsonSerializer.Serialize(root.GetProperty("hours")[23]));
        Assert.Equal("[{\"id\":\"P1\",\"name\":\"A\",\"flow\":20,\"power\":2,\"startupCost\":1},{\"id\":\"P2\",\"name\":\"A\",\"flow\":60,\"power\":4,\"startupCost\":3}]",
            JsonSerializer.Serialize(root.GetProperty("pumps")));
        Assert.Equal("{\"capacity\":200,\"level\":50,\"minimum\":0}", JsonSerializer.Serialize(root.GetProperty("tank")));
        Assert.Equal("{\"onPeak\":0.5,\"offPeak\":0.25}", JsonSerializer.Serialize(root.GetProperty("rates")));

        var em = ScheduleFrames.PumpsPayload(Tank with { Rule = ScheduleRule.EmergencyResilience, Minimum = 20, Budget = 0 });
        using var emDoc = JsonDocument.Parse(Json(em));
        Assert.Equal(["kind", "rule", "hours", "pumps", "tank", "rates", "budget"], emDoc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, emDoc.RootElement.GetProperty("budget").GetDouble());
    }

    [Fact]
    public void Simulate_FollowsTheTankExactly_AndChargesAStartOnlyWhenAPumpTurnsOn() {
        // Hour 0: A on (off-peak). Hour 1: A and B on (peak). Hour 2: B on (peak). Hour 3: nothing.
        var hours = ScheduleFrames.Simulate(Tank, Day(["P1"], ["P1", "P2"], ["P2"], []));
        Assert.Equal(24, hours.Count);
        Assert.Equal("t0 P1 +P1 in 20 level 60 energy 0.5 startup 1", Line(hours[0]));
        Assert.Equal("t1 P1,P2 +P2 in 80 level 130 energy 3 startup 3", Line(hours[1]));
        Assert.Equal("t2 P2 + in 60 level 180 energy 2 startup 0", Line(hours[2]));
        Assert.Equal("t3  + in 0 level 170 energy 0 startup 0", Line(hours[3]));
        Assert.Equal(-30, hours[23].Level);

        static string Line(ScheduleHourRunDto h) =>
            $"{h.Hour} {string.Join(",", h.On)} +{string.Join(",", h.Starts)} in {h.Inflow} level {h.Level} energy {h.Energy} startup {h.Startup}";
    }

    [Fact]
    public void PumpsFrame_Json_HasTheContractFieldsInOrder() {
        var frame = ScheduleFrames.PumpsFrame(Tank, new PumpPlan([new ReachedHour(4, 40, 60, 0.5)], [], null), StepEvent.Try, ["t0"], "Hour 0.", incremental: true);
        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["event", "caption", "focus", "reached", "schedule", "upTo", "hours", "cells", "step"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[{\"states\":4,\"low\":40,\"high\":60,\"best\":0.5}]", JsonSerializer.Serialize(doc.RootElement.GetProperty("reached")));

        var done = ScheduleFrames.PumpsFrame(Tank, new PumpPlan([], Day(["P1"]), 12.3456), StepEvent.Done, [], "Done.", ok: true, incremental: true);
        using var doneDoc = JsonDocument.Parse(Json(done));
        Assert.Equal(["event", "caption", "focus", "reached", "schedule", "upTo", "hours", "cells", "step", "budget", "ok"], doneDoc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(12.35, doneDoc.RootElement.GetProperty("budget").GetDouble());
        Assert.Equal("{\"hour\":\"t0\",\"on\":[\"P1\"],\"starts\":[\"P1\"],\"inflow\":20,\"level\":60,\"energy\":0.5,\"startup\":1}",
            JsonSerializer.Serialize(doneDoc.RootElement.GetProperty("schedule")[0]));
    }

    [Fact]
    public void PumpsFrame_TheForwardPass_HighlightsItsHour_AndAnHourNoStateSurvivesIsRejected() {
        var tried = ScheduleFrames.PumpsFrame(Tank, new PumpPlan([new ReachedHour(2, 40, 60, 1)], [], null), StepEvent.Try, ["t0"], "Hour 0.", incremental: true);
        Assert.Equal((-1, "t0:ElementHighlight", ""), (tried.UpTo, States(tried.Hours), States(tried.Cells)));
        Assert.Empty(tried.Schedule);
        var dead = ScheduleFrames.PumpsFrame(Tank, new PumpPlan([new ReachedHour(2, 40, 60, 1)], [], null), StepEvent.Reject, ["t1"], "Hour 1.", incremental: true);
        Assert.Equal("t1:Rejected", States(dead.Hours));
    }

    [Fact]
    public void PumpsFrame_TheTracedSchedule_IsCommitted_AndTheReplayDrawsUpToItsHour() {
        // The day ends below empty (no pump after hour 2): hours 21 to 23 leave the tank's limits.
        var plan = Plan(Day(["P1"], ["P1", "P2"], ["P2"]));
        var traced = ScheduleFrames.PumpsFrame(Tank, plan, StepEvent.Accept, [], "Traced.", incremental: true);
        Assert.Equal(23, traced.UpTo);
        // Traced back but still being replayed: kept, not the answer yet (#886).
        Assert.Equal("P1t0:Committed P1t1:Committed P2t1:Committed P2t2:Committed", States(traced.Cells));
        Assert.Equal("t21:Rejected t22:Rejected t23:Rejected", States(traced.Hours));

        var replay = ScheduleFrames.PumpsFrame(Tank, plan, StepEvent.Try, ["t22"], "Hour 22.", incremental: true);
        Assert.Equal(22, replay.UpTo);
        Assert.Equal("t21:Rejected t22:Rejected", States(replay.Hours));
        var early = ScheduleFrames.PumpsFrame(Tank, plan, StepEvent.Try, ["t3"], "Hour 3.", incremental: true);
        Assert.Equal((3, "t3:ElementHighlight"), (early.UpTo, States(early.Hours)));
        Assert.Equal(24, early.Schedule.Count);

        // A whole schedule only tried (not incremental) is highlighted, not the answer.
        var candidate = ScheduleFrames.PumpsFrame(Tank, plan, StepEvent.Try, [], "Try it.");
        Assert.All(candidate.Cells, c => Assert.Equal(PictureState.ElementHighlight, c.Value.State));
    }

    [Theory]
    [InlineData("(1.5,((A,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1),(A,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1)))", false, null, "0:P1 1:P2 23:P1,P2")]
    [InlineData("(9.00,1.5,((A,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),(A,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", true, 9.0, "0:P1")]
    public void ParsePumpPlan_ReadsTheRowsByPosition(string answer, bool withBudget, double? budget, string expected) {
        var plan = ScheduleFrames.ParsePumpPlan(2, answer, withBudget);
        Assert.Equal(budget, plan.Budget);
        Assert.Equal(24, plan.On.Length);
        Assert.Equal(expected, string.Join(" ", plan.On.Select((h, i) => (h, i)).Where(p => p.h.Length > 0).Select(p => $"{p.i}:{string.Join(",", p.h)}")));
    }

    [Theory]
    [InlineData("{}", false)]
    [InlineData("", false)]
    [InlineData("(1.5,((A,1,0)))", false)]                                                                           // short row
    [InlineData("(1.5,((A,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),(A,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)] // not a bit
    [InlineData("(1.5,((A,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)]                             // one row for two pumps
    [InlineData("(x,1.5,((A,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),(A,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", true)]
    public void ParsePumpPlan_AnythingElse_IsTheEmptyPlan(string answer, bool withBudget) {
        var plan = ScheduleFrames.ParsePumpPlan(2, answer, withBudget);
        Assert.Equal(new PumpPlan([], [], null), plan);
    }

    [Fact]
    public void BuildPumps_AStoppedRun_EndsBlank_AndNeverReadsItsAnswer() {
        var steps = new List<object> {
            new SolverStep<PumpPlan>(new PumpPlan([new ReachedHour(3, 40, 60, 1)], [], null), StepEvent.Try, ["t0"], "Hour 0."),
            new SolverStep<PumpPlan>(new PumpPlan([], [], null), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        };
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(PumpPlan)) { TimedOut = true, Incremental = true };
        var response = ScheduleFrames.BuildPumps(Tank, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("verified"));
        Assert.Equal("Schedule", response.Type);
        Assert.IsType<SchedulePumpsPayload>(response.Payload);
        var done = (SchedulePumpsFrame)response.Frames[^1];
        Assert.Equal((StepEvent.Done, false, -1), (done.Event, done.Ok, done.UpTo));
        Assert.Empty(done.Reached);
        Assert.Empty(done.Schedule);
        Assert.Empty(done.Hours);
        Assert.Empty(done.Cells);
        Assert.Null(done.Budget);
    }

    [Fact]
    public void BuildPumps_ARunWithoutDrawableSteps_EndsWithTheVerifiedAnswer() {
        string answer = "(1.5,((A,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),(A,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))";
        var frames = ScheduleFrames.BuildPumps(Tank, new SolveRun(answer, [true], null), a => ScheduleFrames.ParsePumpPlan(2, a, false), _ => true).Frames;
        var done = (SchedulePumpsFrame)Assert.Single(frames);
        Assert.Equal((StepEvent.Done, true, 23), (done.Event, done.Ok, done.UpTo));
        Assert.Equal("P1t0:Solution", States(done.Cells));
        var refused = (SchedulePumpsFrame)Assert.Single(ScheduleFrames.BuildPumps(Tank, new SolveRun("{}", [], null), a => ScheduleFrames.ParsePumpPlan(2, a, false), _ => false).Frames);
        Assert.Equal((false, -1), (refused.Ok!.Value, refused.UpTo));
        Assert.Empty(refused.Schedule);
    }

    // ---- Captions ----

    [Fact]
    public void CaptionHelpers_WriteNumbersPlainly() {
        Assert.Equal(["1,000", "4,950", "0.2", "$32.12", "$0.50", "-$1.00", "", "a", "a and b", "a, b and c"],
            [Words.Number(1000), ScheduleFrames.Gallons(4950), ScheduleFrames.Gallons(0.2), ScheduleFrames.Money(32.12), ScheduleFrames.Money(0.5),
             ScheduleFrames.Money(-1), Words.List([]), Words.List(["a"]), Words.List(["a", "b"]), Words.List(["a", "b", "c"])]);
    }

    // ---- Shapes ----

    [Fact]
    public void Shapes_CompareByValue_AndHaveEmptyAnswers() {
        Assert.Equal(new JobOrder(["J1", "J0"]), new JobOrder(["J1", "J0"]));
        Assert.NotEqual(new JobOrder(["J0", "J1"]), new JobOrder(["J1", "J0"]));
        Assert.Equal(new PumpPlan([new ReachedHour(1, 2, 3, 4)], [["P1"], []], 5), new PumpPlan([new ReachedHour(1, 2, 3, 4)], [["P1"], []], 5));
        Assert.NotEqual(new PumpPlan([], [["P1"]], null), new PumpPlan([], [["P2"]], null));
        Assert.NotEqual(new PumpPlan([], [], 1), new PumpPlan([], [], null));
        Assert.Equal(new JobOrder([]), Shapes.EmptyAnswer(typeof(JobOrder)));
        Assert.Equal(new PumpPlan([], [], null), Shapes.EmptyAnswer(typeof(PumpPlan)));
        Assert.Equal("{\"order\":[\"J0\"]}", JsonSerializer.Serialize(new JobOrder(["J0"])));
        Assert.Equal("{\"reached\":[],\"on\":[[\"P1\"]]}", JsonSerializer.Serialize(new PumpPlan([], [["P1"]], null)));
        Assert.Equal("{\"reached\":[],\"on\":[],\"budget\":2.5}", JsonSerializer.Serialize(new PumpPlan([], [], 2.5)));
    }
}
