using System.Diagnostics;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Job Sequencing and Pump Scheduling solvers record steps for the Schedule picture. Recording must never change an
// answer. Endpoints/Snapshots/schedule_solver_answers.tsv holds what each returned (solver, instance and answer as JSON
// strings; tab separated) BEFORE the picture existed (with the #700/#701 and #800 to #804 fixes in), on its default
// instance, hand-picked edge cases and seeded random instances.
public class ScheduleSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("schedule_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], JsonSerializer.Deserialize<string>(parts[1])!, JsonSerializer.Deserialize<string>(parts[2])!);
        });

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    // The visualization that draws each solver's steps.
    internal static readonly Dictionary<string, string> VisualizationOf = new() {
        ["jobseqbruteforce"] = "jobseqvisualization",
        ["pumpschedulingcmsolver"] = "pumpschedulingcmvisualization",
        ["pumpschedulingemsolver"] = "pumpschedulingemvisualization",
    };

    internal const string JobDefault = "((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)";
    internal const string NothingFits = "((4,4,4),(4,5,6),(5,5,5),4)";
    // Eight jobs that all take 3 and are due at 3: only the first runs on time, so seven are late (penalty 7 > K = 6) in
    // every one of the 40,320 orders.
    internal const string EightLate = "((3,3,3,3,3,3,3,3),(3,3,3,3,3,3,3,3),(1,1,1,1,1,1,1,1),6)";

    internal const string Demand = "(600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)";
    internal const string Pumps3 = "((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0))";
    internal const string CmDefault = "((10000,5000),(" + Demand + ",(8,9,10,11,17,18,19,20),(0.12,0.06))," + Pumps3 + ")";
    internal const string EmDefault = "((10000,5000,2000),(" + Demand + ",(8,9,10,11,17,18,19,20),(0.12,0.06))," + Pumps3 + ",0)";
    internal const string EmTinyBudget = "((10000,5000,2000),(" + Demand + ",(8,9,10,11,17,18,19,20),(0.12,0.06))," + Pumps3 + ",5)";
    // Demand of 900 to 1,500 gal an hour, and all three pumps give 1,050: the 1,500-gal tank runs dry at hour 20.
    internal const string CmDemandTooHigh =
        "((3000,1500),((900,900,900,900,900,900,900,900,1500,1500,1500,1500,900,900,900,900,900,1500,1500,1500,1500,1500,900,900),"
        + "(8,9,10,11,17,18,19,20),(0.12,0.06))," + Pumps3 + ")";
    // 0.4 gal of demand an hour and one 1-gal pump: the table rounds 0.6 gal up to 1 and never sees the tank run dry (#738).
    internal const string CmRounded =
        "((10,0),((0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4),(),(1,1)),((A,1,1,0)))";

    private static SolveRun Run(string solver, string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).Run(instance, withSteps: true);

    private static List<SolverStep<T>> Steps<T>(string solver, string instance) => Run(solver, instance).Steps.Cast<SolverStep<T>>().ToList();

    private static (object Payload, List<T> Frames) Picture<T>(string solver, string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solver]])!;
        var response = vis.BuildFrames(instance, Run(solver, instance));
        Assert.Equal("Schedule", response.Type);
        return (response.Payload, response.Frames.Cast<T>().ToList());
    }

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsFitThePicture(string solverName, string instance, string answer) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.False(on.TimedOut);
        Assert.Equal(IncrementalSolvers_Tests.Classification[solverName], on.Incremental);

        // The Done step's verdict is the verifier's.
        var problemType = ProblemProvider.Solvers[solverName].GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];
        var problem = (IProblem)Activator.CreateInstance(problemType, [instance])!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }

        if (solverName == "jobseqbruteforce") {
            var steps = on.Steps.Cast<SolverStep<JobOrder>>().ToList();
            CheckCommon(steps, verified);
            int jobs = ((API.Problems.NPComplete.NPC_JOBSEQ.JOBSEQ)problem).T.Count;
            var ids = Enumerable.Range(0, jobs).Select(ScheduleFrames.JobId).ToHashSet();
            // Every step but Done tries a whole order of every job.
            Assert.All(steps.SkipLast(1), s => Assert.Equal(ids.Order(), s.Partial.Order.Order()));
            Assert.All(steps, s => Assert.All(s.Focus, id => Assert.Contains(id, ids)));
            Assert.All(steps.SkipLast(1), s => Assert.True(s.Event is StepEvent.Reject or StepEvent.Accept));
            Assert.Equal(verified ? ScheduleFrames.ParseJobOrder(jobs, answer) : new JobOrder([]), steps[^1].Partial);
            var (_, frames) = Picture<ScheduleJobsFrame>(solverName, instance);
            Assert.Equal(steps.Count, frames.Count);
            Assert.Equal(verified, frames[^1].Ok);
        } else {
            var steps = on.Steps.Cast<SolverStep<PumpPlan>>().ToList();
            CheckCommon(steps, verified);
            bool em = solverName == "pumpschedulingemsolver";
            int pumps = em ? ((API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.PUMPSCHEDULINGEM)problem).Pumps.Count
                : ((API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.PUMPSCHEDULINGCM)problem).Pumps.Count;
            var pumpIds = Enumerable.Range(0, pumps).Select(ScheduleFrames.PumpId).ToHashSet();
            var hourIds = Enumerable.Range(0, 24).Select(ScheduleFrames.HourId).ToHashSet();
            Assert.All(steps, s => Assert.All(s.Focus, id => Assert.Contains(id, hourIds)));
            Assert.All(steps, s => Assert.All(s.Partial.On, hour => Assert.All(hour, id => Assert.Contains(id, pumpIds))));
            // The answer's schedule is the Done step's (Reached is the solver's own and not in the answer).
            var expected = ScheduleFrames.ParsePumpPlan(pumps, answer, em);
            Assert.Equal(expected.On.Select(h => string.Join(",", h)), steps[^1].Partial.On.Select(h => string.Join(",", h)));
            if (em && answer != "{}") Assert.Equal(expected.Budget, steps[^1].Partial.Budget);
            // A schedule is the forward pass, the trace back and 24 replay steps; no answer stops at an hour no state survives.
            if (answer == "{}") {
                Assert.DoesNotContain(steps, s => s.Event == StepEvent.Accept);
                Assert.Equal(StepEvent.Reject, steps[^2].Event);
            } else {
                Assert.Equal(24, steps.Count(s => s.Event == StepEvent.Try && s.Partial.On.Length == 0 && s.Focus.Length == 1));
                Assert.Single(steps, s => s.Event == StepEvent.Accept);
                Assert.Equal(Enumerable.Range(0, 24).Select(ScheduleFrames.HourId), steps.Where(s => s.Event == StepEvent.Try && s.Partial.On.Length == 24).Select(s => s.Focus.Single()));
                Assert.Equal(24, steps[^1].Partial.Reached.Length);
            }
            var (_, frames) = Picture<SchedulePumpsFrame>(solverName, instance);
            Assert.Equal(steps.Count, frames.Count);
            Assert.Equal(verified, frames[^1].Ok);
        }
    }

    private static void CheckCommon<T>(List<SolverStep<T>> steps, bool verified) {
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        // At most the cap, the last Accept kept past it, and Done.
        Assert.True(steps.Count <= StepRecorder<T>.DefaultCap + 2);
        Assert.Equal(verified, steps[^1].Ok);
    }

    // ---- Job Sequencing ----

    [Fact]
    public void JobSeq_Default_TriesOrdersUntilOneFits_AsTheMockupShows() {
        var steps = Steps<JobOrder>("jobseqbruteforce", JobDefault);
        Assert.Equal(152, steps.Count);
        // The mockup's frame: the fifth order tried.
        Assert.Equal((StepEvent.Reject, "Try (0,1,2,5,3,4): jobs 2, 3 and 4 finish late, penalty 10 > K = 4."), (steps[4].Event, steps[4].Caption));
        Assert.Equal(["J3", "J4", "J5"], steps[4].Focus);
        // The order that works is kept past the cap, then Done says how many were not shown.
        Assert.Equal((StepEvent.Accept, "Try (1,3,5,4,0,2): jobs 0 and 2 finish late, penalty 4, within K = 4. It works."), (steps[^2].Event, steps[^2].Caption));
        Assert.Equal("(1,3,5,4,0,2) is the first order that keeps the late penalty within K = 4, found after 191 orders. (40 not shown)", steps[^1].Caption);
        Assert.Equal(new JobOrder(["J2", "J4", "J6", "J5", "J1", "J3"]), steps[^1].Partial);
    }

    [Fact]
    public void JobSeq_WholeOrders_AreHighlighted_LateJobsRejected_AndTheAnswerIsSolution() {
        var (_, frames) = Picture<ScheduleJobsFrame>("jobseqbruteforce", JobDefault);
        Assert.Equal("J1:ElementHighlight J2:ElementHighlight J3:Rejected J4:Rejected J5:Rejected J6:ElementHighlight", States(frames[4].Jobs));
        Assert.Equal(10, frames[4].Penalty);
        Assert.Equal("J1:Rejected J2:Solution J3:Rejected J4:Solution J5:Solution J6:Solution", States(frames[^1].Jobs));
        Assert.Equal((4, true), (frames[^1].Penalty, frames[^1].Ok));
        Assert.Equal("J2 0-2 J4 2-11 J6 11-14 J5 14-18 J1 18-22! J3 22-27!", string.Join(" ", frames[^1].Runs.Select(r => $"{r.Job} {r.Start}-{r.End}{(r.Late ? "!" : "")}")));
    }

    [Fact]
    public void JobSeq_NothingFits_RejectsEveryOrder_ThenAnswersNothing() {
        var steps = Steps<JobOrder>("jobseqbruteforce", NothingFits);
        Assert.Equal([.. Enumerable.Repeat(StepEvent.Reject, 6), StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal("Try (0,1,2): jobs 1 and 2 finish late, penalty 10 > K = 4.", steps[0].Caption);
        Assert.Equal("Checked all 6 orders. None keeps the late penalty within K = 4.", steps[^1].Caption);
        Assert.Equal((new JobOrder([]), false), (steps[^1].Partial, steps[^1].Ok!.Value));
        var (_, frames) = Picture<ScheduleJobsFrame>("jobseqbruteforce", NothingFits);
        Assert.Empty(frames[^1].Jobs);
        Assert.Empty(frames[^1].Runs);
    }

    [Theory]
    [InlineData("((3),(2),(5),4)", "{}", "There is only one order, and its late penalty is more than K = 4.")]
    [InlineData("((3),(2),(5),5)", "(0)", "(0) is the first order that keeps the late penalty within K = 5, found after 1 order.")]
    public void JobSeq_TinyInstances_SayWhatHappened(string instance, string answer, string done) {
        var run = Run("jobseqbruteforce", instance);
        Assert.Equal(answer, run.Answer);
        Assert.Equal(done, ((SolverStep<JobOrder>)run.Steps[^1]).Caption);
    }

    [Fact]
    public void JobSeq_ALongRun_ShowsItsFirstOrders_AndSaysHowManyWereNotShown() {
        var steps = Steps<JobOrder>("jobseqbruteforce", EightLate);
        Assert.Equal(StepRecorder<JobOrder>.DefaultCap + 1, steps.Count);
        Assert.Equal("Checked all 40,320 orders. None keeps the late penalty within K = 6. (40170 not shown)", steps[^1].Caption);
        Assert.Equal("Try (0,1,2,3,4,5,6,7): jobs 1, 2, 3, 4, 5, 6 and 7 finish late, penalty 7 > K = 6.", steps[0].Caption);
    }

    // ---- Pump Scheduling ----

    [Fact]
    public void PumpCM_Default_ForwardPass_TraceBack_Replay_ThenDone() {
        var steps = Steps<PumpPlan>("pumpschedulingcmsolver", CmDefault);
        Assert.Equal(50, steps.Count);
        Assert.Equal("Forward pass, hour 0: 8 reachable (tank level, pumps on) states, levels 4,400 to 5,450 gal, cheapest so far $0.00.", steps[0].Caption);
        Assert.Equal(new ReachedHour(8, 4400, 5450, 0), steps[0].Partial.Reached.Single());
        Assert.Equal(["t0"], steps[0].Focus);
        Assert.Equal("Forward pass, hour 23: 1,564 reachable (tank level, pumps on) states, levels 0 to 10,000 gal, cheapest so far $32.12.", steps[23].Caption);
        Assert.Equal((StepEvent.Accept, "Trace back from the cheapest state at hour 24 ($32.12), following each state's parent, to recover the schedule."),
            (steps[24].Event, steps[24].Caption));
        Assert.Equal("Hour 0: PumpA and PumpB on. In 550, out 600 gal, so the tank drops to 4,950 gal. Hour cost $7.31, including $6.50 to start PumpA and PumpB.",
            steps[25].Caption);
        Assert.Equal("Hour 8 (peak): PumpA and PumpB on. In 550, out 1,000 gal, so the tank drops to 4,150 gal. Hour cost $1.62.", steps[33].Caption);
        Assert.Equal((true, "Cheapest schedule: $32.12 for the day. The tank stays between 0 and 5,000 gal."), (steps[^1].Ok!.Value, steps[^1].Caption));
        Assert.Null(steps[^1].Partial.Budget);
    }

    [Fact]
    public void PumpCM_Frames_HighlightTheHourAtHand_AndDrawTheScheduleAsSolution() {
        var (payload, frames) = Picture<SchedulePumpsFrame>("pumpschedulingcmsolver", CmDefault);
        var p = Assert.IsType<SchedulePumpsPayload>(payload);
        Assert.Equal(("costMinimization", 24, 3, null), (p.Rule, p.Hours.Count, p.Pumps.Count, p.Budget));
        Assert.Equal(["t8", "t9", "t10", "t11", "t17", "t18", "t19", "t20"], p.Hours.Where(h => h.Peak).Select(h => h.Id));
        // Forward pass: the hour being filled, no schedule yet.
        Assert.Equal((-1, "t5:ElementHighlight", 0, 6), (frames[5].UpTo, States(frames[5].Hours), frames[5].Cells.Count, frames[5].Reached.Count));
        // Trace back: the whole schedule is committed.
        Assert.Equal((23, ""), (frames[24].UpTo, States(frames[24].Hours)));
        Assert.Equal(47, frames[24].Cells.Count);
        Assert.All(frames[24].Cells, c => Assert.Equal(PictureState.Solution, c.Value.State));
        Assert.DoesNotContain(frames[24].Cells.Keys, id => id.StartsWith("P3"));
        // Replay: the hour at hand, drawn up to it.
        Assert.Equal((8, "t8:ElementHighlight"), (frames[33].UpTo, States(frames[33].Hours)));
        Assert.Equal("{\"hour\":\"t8\",\"on\":[\"P1\",\"P2\"],\"starts\":[],\"inflow\":550,\"level\":4150,\"energy\":1.62,\"startup\":0}",
            JsonSerializer.Serialize(frames[33].Schedule[8]));
        Assert.Equal(0, frames[^1].Schedule[23].Level);
    }

    [Fact]
    public void PumpCM_DemandTooHigh_RejectsTheHourNoStateSurvives_ThenAnswersNothing() {
        var steps = Steps<PumpPlan>("pumpschedulingcmsolver", CmDemandTooHigh);
        Assert.Equal(22, steps.Count);
        Assert.Equal((StepEvent.Reject, "Forward pass, hour 20: no state survives. Every pump mix either drains the tank below empty or overflows it."),
            (steps[20].Event, steps[20].Caption));
        Assert.Equal(["t20"], steps[20].Focus);
        Assert.Equal((false, "No schedule keeps the tank between empty and full for all 24 hours, so there is no answer."), (steps[^1].Ok!.Value, steps[^1].Caption));
        Assert.Equal(20, steps[^1].Partial.Reached.Length);
        var (_, frames) = Picture<SchedulePumpsFrame>("pumpschedulingcmsolver", CmDemandTooHigh);
        Assert.Equal("t20:Rejected", States(frames[20].Hours));
        Assert.Empty(frames[^1].Schedule);
    }

    [Fact]
    public void PumpCM_ARoundedSchedule_ShowsTheHoursTheTankRunsDry_AndIsNotOk() {
        var run = Run("pumpschedulingcmsolver", CmRounded);
        var last = (SolverStep<PumpPlan>)run.Steps[^1];
        Assert.Equal(false, last.Ok);
        Assert.Equal("The schedule costs $1.00, but the tank ends hour 2 at -0.2 gal, outside 0 to 10 gal: the solver rounds the tank level to whole gallons, so it did not see this.",
            last.Caption);
        Assert.Equal("Forward pass, hour 0: 1 reachable (tank level, pumps on) state, level 1 gal, cheapest so far $1.00.", ((SolverStep<PumpPlan>)run.Steps[0]).Caption);
        var (_, frames) = Picture<SchedulePumpsFrame>("pumpschedulingcmsolver", CmRounded);
        Assert.Equal(Enumerable.Range(2, 22).Select(h => $"t{h}").Order(StringComparer.Ordinal), frames[^1].Hours.Keys);
        Assert.All(frames[^1].Hours, h => Assert.Equal(PictureState.Rejected, h.Value.State));
        // While replaying hour 2, only that hour is drawn out of bounds.
        Assert.Equal("t2:Rejected", States(frames[24 + 1 + 2].Hours));
        Assert.Equal("Hour 2: no pump on. In 0, out 0.4 gal, so the tank drops to -0.2 gal. That is below empty. Hour cost $0.00.", frames[24 + 1 + 2].Caption);
    }

    [Fact]
    public void PumpEM_Default_FixesTheBudgetFirst_AndReplaysAgainstIt() {
        var steps = Steps<PumpPlan>("pumpschedulingemsolver", EmDefault);
        Assert.Equal(51, steps.Count);
        Assert.Equal("The budget is 0, which means 1.5 times the cheapest schedule that keeps the tank within its limits. That costs $37.24, so the budget is $55.86.",
            steps[0].Caption);
        Assert.Equal((55.86, 0, 0), (steps[0].Partial.Budget!.Value, steps[0].Partial.Reached.Length, steps[0].Partial.On.Length));
        Assert.All(steps, s => Assert.Equal(55.86, s.Partial.Budget));
        Assert.Equal("Forward pass, hour 0: 8 reachable (tank level, pumps on) states, levels 4,400 to 5,450 gal, most water stored so far 5,450 gal·h.", steps[1].Caption);
        Assert.Equal("Hour 0: PumpA, PumpB and PumpC on. In 1,050, out 600 gal, so the tank rises to 5,450 gal. Hour cost $14.03, including $12.50 to start PumpA, PumpB and PumpC. $14.03 of the $55.86 budget spent.",
            steps[26].Caption);
        Assert.Equal((true, "Most water stored within the $55.86 budget (1.5 times the cheapest schedule): 203,250 gal·h, for $55.55."), (steps[^1].Ok!.Value, steps[^1].Caption));
        var (payload, frames) = Picture<SchedulePumpsFrame>("pumpschedulingemsolver", EmDefault);
        Assert.Equal(("emergencyResilience", 2000.0, 0.0), (((SchedulePumpsPayload)payload).Rule, ((SchedulePumpsPayload)payload).Tank.Minimum, ((SchedulePumpsPayload)payload).Budget!.Value));
        Assert.All(frames, f => Assert.Equal(55.86, f.Budget));
    }

    [Fact]
    public void PumpEM_ATinyBudget_RunsOutAtAnHour_ThenAnswersNothing() {
        var steps = Steps<PumpPlan>("pumpschedulingemsolver", EmTinyBudget);
        Assert.Equal((StepEvent.Reject, "Forward pass, hour 7: no state survives. Every pump mix either breaks the tank's limits or the budget."),
            (steps[^2].Event, steps[^2].Caption));
        Assert.Equal("No schedule keeps the tank between 2,000 and 10,000 gal within the $5.00 budget for all 24 hours, so there is no answer.", steps[^1].Caption);
        Assert.All(steps, s => Assert.Equal(5, s.Partial.Budget));
    }

    [Theory]
    [InlineData("pumpschedulingcmsolver", CmDefault)]
    [InlineData("pumpschedulingemsolver", EmDefault)]
    [InlineData("jobseqbruteforce", JobDefault)]
    public void DefaultInstances_RecordWellWithinTheTimeBudget(string solver, string instance) {
        var sw = Stopwatch.StartNew();
        var run = Run(solver, instance);
        Assert.False(run.TimedOut);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(ProblemProvider.DefaultSolveTimeLimitSeconds / 2), $"took {sw.Elapsed}");
    }

    [Fact]
    public void PumpSolvers_RecordNothing_WhenRecordingIsOff() {
        foreach (var (solver, instance) in new[] { ("pumpschedulingcmsolver", CmDefault), ("pumpschedulingemsolver", EmDefault) }) {
            var off = ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).Run(instance, withSteps: false);
            Assert.Empty(off.Steps);
            Assert.Equal(typeof(PumpPlan), off.StepShape);
            Assert.True(off.Incremental);
        }
    }
}
