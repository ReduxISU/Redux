using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using SPADE;

namespace API.Interfaces.Schedule;

/// <summary>Which scheduling question a problem asks; it decides what runs along the time axis.</summary>
enum ScheduleRule {
    /// <summary>Jobs run one after another on one machine; the late jobs' penalties must add up to at most K.</summary>
    JobSequencing,
    /// <summary>Pumps fill a tank hour by hour for 24 hours; the tank stays between empty and full at the least cost.</summary>
    CostMinimization,
    /// <summary>As <see cref="CostMinimization"/>, with a minimum tank level, storing the most water within a budget.</summary>
    EmergencyResilience,
}

// ---- What a problem hands the Schedule picture ----

/// <summary>One job as the instance lists it: how long it runs, its deadline and the penalty if it finishes after it.</summary>
sealed record ScheduleJob(int Time, int Deadline, int Penalty);

/// <summary>A Job Sequencing instance: its jobs in instance order (two equal jobs are two jobs) and the penalty budget K.</summary>
sealed record JobScheduleSpec(IReadOnlyList<ScheduleJob> Jobs, int K);

/// <summary>One pump as the instance lists it: its name, its flow (gallons an hour), its power (kW) and its startup cost.</summary>
sealed record SchedulePump(string Name, double Flow, double Power, double StartupCost);

/// <summary>
/// A Pump Scheduling instance (either problem): the tank, the 24 hourly demands, the peak hours and tariffs, and the pumps in
/// instance order. <see cref="Minimum"/> is 0 for Cost Minimization. <see cref="Budget"/> is Emergency Resilience's budget
/// as the instance writes it (0 means 1.5 times the cheapest schedule), and null for Cost Minimization.
/// </summary>
sealed record PumpScheduleSpec(
    ScheduleRule Rule, double Capacity, double Level, double Minimum, IReadOnlyList<double> Demand, IReadOnlySet<int> PeakHours,
    double OnPeak, double OffPeak, IReadOnlyList<SchedulePump> Pumps) {
    public double? Budget { get; init; }
}

// ---- JSON shapes (explicit names so the contract does not depend on serializer naming options) ----

/// <summary>A job: its id (<c>J0</c>, by position) and its label (<c>Job 0</c>), both numbered from 0 as the certificate
/// counts (#846), and its time, deadline and penalty.</summary>
sealed record ScheduleJobDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("time")] int Time,
    [property: JsonPropertyName("deadline")] int Deadline,
    [property: JsonPropertyName("penalty")] int Penalty);

/// <summary>
/// The static part of a Job Sequencing picture. <c>k</c> is the most total penalty allowed. <c>span</c> is the length of the
/// time axis: every job run back to back, or the latest deadline if that is later.
/// </summary>
sealed record ScheduleJobsPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("jobs")] IReadOnlyList<ScheduleJobDto> Jobs,
    [property: JsonPropertyName("k")] int K,
    [property: JsonPropertyName("span")] int Span);

/// <summary>One hour of the day: its id (<c>t0</c>…<c>t23</c>), its label (<c>00:00</c>), its demand in gallons and whether it
/// is a peak-tariff hour.</summary>
sealed record ScheduleHourDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("demand")] double Demand,
    [property: JsonPropertyName("peak")] bool Peak);

/// <summary>A pump: its id (<c>P1</c>, by position), its name as the instance writes it, its flow (gallons an hour), its
/// power (kW) and its startup cost.</summary>
sealed record SchedulePumpDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("flow")] double Flow,
    [property: JsonPropertyName("power")] double Power,
    [property: JsonPropertyName("startupCost")] double StartupCost);

/// <summary>The tank: its capacity, its level at the start of hour 0, and the lowest level allowed (0 for Cost
/// Minimization), all in gallons.</summary>
sealed record ScheduleTankDto(
    [property: JsonPropertyName("capacity")] double Capacity,
    [property: JsonPropertyName("level")] double Level,
    [property: JsonPropertyName("minimum")] double Minimum);

/// <summary>The price of a kWh in a peak hour and in any other hour.</summary>
sealed record ScheduleRatesDto(
    [property: JsonPropertyName("onPeak")] double OnPeak,
    [property: JsonPropertyName("offPeak")] double OffPeak);

/// <summary>
/// The static part of a Pump Scheduling picture: 24 hours, the pumps, the tank and the tariffs. <c>budget</c> (Emergency
/// Resilience only) is the budget as the instance writes it; 0 means 1.5 times the cheapest schedule, and the frames carry
/// the budget the solver settled on.
/// </summary>
sealed record SchedulePumpsPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("hours")] IReadOnlyList<ScheduleHourDto> Hours,
    [property: JsonPropertyName("pumps")] IReadOnlyList<SchedulePumpDto> Pumps,
    [property: JsonPropertyName("tank")] ScheduleTankDto Tank,
    [property: JsonPropertyName("rates")] ScheduleRatesDto Rates) {
    [JsonPropertyName("budget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Budget { get; init; }
}

/// <summary>When a placed job runs: from <c>start</c> to <c>end</c>, and whether it ends after its deadline.</summary>
sealed record ScheduleRunDto(
    [property: JsonPropertyName("job")] string Job,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("end")] int End,
    [property: JsonPropertyName("late")] bool Late);

/// <summary>
/// One picture of a Job Sequencing run. <c>order</c> is the job ids placed so far, in the order they run; <c>runs</c> gives
/// each its start and end; <c>penalty</c> adds up the penalties of the late ones. <c>jobs</c> lists only jobs whose state is
/// not <see cref="PictureState.Background"/>.
/// </summary>
sealed record ScheduleJobsFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("order")] IReadOnlyList<string> Order,
    [property: JsonPropertyName("runs")] IReadOnlyList<ScheduleRunDto> Runs,
    [property: JsonPropertyName("jobs")] IReadOnlyDictionary<string, StateRef> Jobs,
    [property: JsonPropertyName("penalty")] int Penalty) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// One hour of a schedule, simulated exactly: the pumps on, the pumps that start that hour (and pay their startup cost),
/// the water pumped in, the tank level at the end of the hour, and what the hour costs in energy and in startups.
/// </summary>
sealed record ScheduleHourRunDto(
    [property: JsonPropertyName("hour")] string Hour,
    [property: JsonPropertyName("on")] IReadOnlyList<string> On,
    [property: JsonPropertyName("starts")] IReadOnlyList<string> Starts,
    [property: JsonPropertyName("inflow")] double Inflow,
    [property: JsonPropertyName("level")] double Level,
    [property: JsonPropertyName("energy")] double Energy,
    [property: JsonPropertyName("startup")] double Startup);

/// <summary>
/// One picture of a Pump Scheduling run.
/// <list type="bullet">
/// <item><c>reached</c>: the dynamic program's forward pass so far, one entry per hour (the band of tank levels it can still
/// reach, and its best score).</item>
/// <item><c>schedule</c>: the schedule once the solver has one (24 hours), each hour simulated exactly; empty before.</item>
/// <item><c>upTo</c>: the last hour of the schedule drawn so far: -1 while there is none, the hour being replayed, or 23.</item>
/// <item><c>hours</c> (ids <c>t0</c>…) and <c>cells</c> (a pump in an hour, <c>P1t0</c>…) list only elements whose state is not
/// <see cref="PictureState.Background"/>.</item>
/// <item><c>budget</c>: the budget the schedule must fit, once the solver has fixed it (Emergency Resilience).</item>
/// </list>
/// </summary>
sealed record SchedulePumpsFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("reached")] IReadOnlyList<ReachedHour> Reached,
    [property: JsonPropertyName("schedule")] IReadOnlyList<ScheduleHourRunDto> Schedule,
    [property: JsonPropertyName("upTo")] int UpTo,
    [property: JsonPropertyName("hours")] IReadOnlyDictionary<string, StateRef> Hours,
    [property: JsonPropertyName("cells")] IReadOnlyDictionary<string, StateRef> Cells) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("budget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Budget { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Schedule picture (Job Sequencing, Pump Scheduling Cost Minimization and Emergency Resilience) from an instance
/// and a step's <see cref="JobOrder"/> or <see cref="PumpPlan"/>. Time runs left to right. Nothing here knows about a
/// particular solver.
/// <para>
/// <b>Ids</b> are positions, never numbers or names: job <c>i</c> (0-based) is <c>J{i}</c>, pump <c>p</c> is <c>P{p+1}</c>,
/// hour <c>h</c> is <c>t{h}</c>, and a pump in an hour is <c>P{p+1}t{h}</c>. Two equal jobs, or two pumps with one name, stay
/// apart, and no id needs escaping.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>; see
/// <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>A placed job, or a pump on in an hour of the schedule, is <c>ElementHighlight</c> while a whole candidate is only
/// tried (a Try or Reject of a run that is not <see cref="SolveRun.Incremental"/>), <c>Committed</c> while it is kept but not
/// confirmed (an Accept the run goes on from, and every step of an incremental run: the jobs scheduled so far; #886), and
/// <c>Solution</c> only in the confirmed answer: the Done, or an Accept that ends the run (<see cref="GraphSubsetFrames.Picked"/>).
/// In a Done with ok = false (the run found nothing) it is <c>Rejected</c>, never <c>Solution</c>.</item>
/// <item>A placed job that ends after its deadline is <c>Rejected</c> in every frame: its penalty counts.</item>
/// <item>An hour drawn so far (up to <c>upTo</c>) whose tank level ends below the minimum or above the capacity is
/// <c>Rejected</c>.</item>
/// <item>The focus is <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed (an hour no state
/// survives), so <c>Rejected</c> (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>A run stopped by its time limit ends with a blank Done frame, ok = false: nothing placed, no forward pass, no
/// schedule (<see cref="StoppedJobsFrame"/>, <see cref="StoppedPumpsFrame"/>).</item>
/// </list>
/// Covered, Blocked and Untraveled are not used.
/// </summary>
static class ScheduleFrames {
    public const string Kind = "schedule";
    public const int Hours = 24;

    // ---- Ids ----

    /// <summary><c>J{i}</c> for the job at 0-based position <paramref name="index"/>, numbered from 0 as the certificate and the
    /// captions count (#846).</summary>
    public static string JobId(int index) => $"J{index}";

    /// <summary><c>P{p+1}</c> for the pump at 0-based position <paramref name="index"/>.</summary>
    public static string PumpId(int index) => $"P{index + 1}";

    /// <summary><c>t{h}</c> for hour <paramref name="hour"/> (0 to 23).</summary>
    public static string HourId(int hour) => $"t{hour}";

    /// <summary><c>P{p+1}t{h}</c>: pump <paramref name="pump"/> in hour <paramref name="hour"/>.</summary>
    public static string CellId(int pump, int hour) => PumpId(pump) + HourId(hour);

    public static string RuleName(ScheduleRule rule) => rule switch {
        ScheduleRule.JobSequencing => "jobSequencing",
        ScheduleRule.CostMinimization => "costMinimization",
        ScheduleRule.EmergencyResilience => "emergencyResilience",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    // ==================================================================================================================
    // Job Sequencing
    // ==================================================================================================================

    /// <summary>The order of 0-based job indices as job ids.</summary>
    public static JobOrder Order(IEnumerable<int> indices) => new(indices.Select(JobId));

    public static ScheduleJobsPayload JobsPayload(JobScheduleSpec spec) =>
        new(Kind, RuleName(ScheduleRule.JobSequencing),
            spec.Jobs.Select((j, i) => new ScheduleJobDto(JobId(i), $"Job {i}", j.Time, j.Deadline, j.Penalty)).ToList(),
            spec.K,
            Math.Max(spec.Jobs.Sum(j => j.Time), spec.Jobs.Count == 0 ? 0 : spec.Jobs.Max(j => j.Deadline)));

    // The placed jobs as 0-based indices, in order; ids that are not jobs, and repeats, are dropped.
    private static List<int> Indices(JobScheduleSpec spec, JobOrder order) {
        var index = Enumerable.Range(0, spec.Jobs.Count).ToDictionary(JobId, i => i, StringComparer.Ordinal);
        var seen = new HashSet<int>();
        return order.Order.Where(index.ContainsKey).Select(id => index[id]).Where(seen.Add).ToList();
    }

    /// <summary>When each job of <paramref name="order"/> runs, back to back from time 0.</summary>
    public static List<ScheduleRunDto> Runs(JobScheduleSpec spec, IReadOnlyList<int> order) {
        var runs = new List<ScheduleRunDto>();
        int t = 0;
        foreach (int j in order) {
            int start = t;
            t += spec.Jobs[j].Time;
            // Late as the verifier counts it: finishing after the deadline.
            runs.Add(new ScheduleRunDto(JobId(j), start, t, t > spec.Jobs[j].Deadline));
        }
        return runs;
    }

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static ScheduleJobsFrame JobsFrame(JobScheduleSpec spec, JobOrder partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var order = Indices(spec, partial);
        var runs = Runs(spec, order);
        var states = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        foreach (var run in runs) {
            GraphSubsetFrames.Raise(states, run.Job, picked);
            if (run.Late) GraphSubsetFrames.Raise(states, run.Job, PictureState.Rejected);
        }
        var ids = new HashSet<string>(Enumerable.Range(0, spec.Jobs.Count).Select(JobId), StringComparer.Ordinal);
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus.Where(ids.Contains)) GraphSubsetFrames.Raise(states, id, mark);
        int penalty = order.Zip(runs).Where(p => p.Second.Late).Sum(p => spec.Jobs[p.First].Penalty);
        return new ScheduleJobsFrame(ev, caption, focus, runs.Select(r => r.Job).ToList(), runs, GraphSubsetFrames.ToRefs(states), penalty) { Ok = ok };
    }

    /// <summary>The blank Done frame, ok = false, that closes a run stopped by its time limit: nothing placed.</summary>
    public static ScheduleJobsFrame StoppedJobsFrame(string caption) =>
        new(StepEvent.Done, caption, [], [], [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal), 0) { Ok = false };

    /// <summary>
    /// A Job Sequencing certificate (<c>(1,3,5,4,0,2)</c>, 0-based job indices in run order) as a job order; anything that is
    /// not an order of every job (<c>{}</c>, no answer, included) is the empty order.
    /// </summary>
    public static JobOrder ParseJobOrder(int jobs, string answer) {
        string inner = answer.Trim();
        if (!inner.StartsWith('(') || !inner.EndsWith(')')) return new JobOrder([]);
        var parts = inner[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var indices = new List<int>();
        foreach (var part in parts) {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int i) || i >= jobs || indices.Contains(i))
                return new JobOrder([]);
            indices.Add(i);
        }
        return indices.Count == jobs ? Order(indices) : new JobOrder([]);
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last step
    /// already is the Done step. A run stopped by its time limit ends with <see cref="StoppedJobsFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse BuildJobs(JobScheduleSpec spec, SolveRun run, Func<string, JobOrder> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<JobOrder>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<JobOrder>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedJobsFrame(step.Caption)
                : JobsFrame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedJobsFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(JobsFrame(spec, ok ? parseAnswer(run.Answer) : new JobOrder([]), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.Schedule), JobsPayload(spec), frames);
    }

    // ==================================================================================================================
    // Pump Scheduling
    // ==================================================================================================================

    public static SchedulePumpsPayload PumpsPayload(PumpScheduleSpec spec) =>
        new(Kind, RuleName(spec.Rule),
            Enumerable.Range(0, Hours).Select(h => new ScheduleHourDto(HourId(h), $"{h:00}:00", spec.Demand[h], spec.PeakHours.Contains(h))).ToList(),
            spec.Pumps.Select((p, i) => new SchedulePumpDto(PumpId(i), p.Name, p.Flow, p.Power, p.StartupCost)).ToList(),
            new ScheduleTankDto(spec.Capacity, spec.Level, spec.Minimum),
            new ScheduleRatesDto(spec.OnPeak, spec.OffPeak)) {
            Budget = spec.Budget,
        };

    /// <summary>The ids of the pumps on in an hour, from the solver's bit mask (bit p is pump p).</summary>
    public static string[] OnFromMask(int mask, int pumps) =>
        Enumerable.Range(0, pumps).Where(p => (mask & (1 << p)) != 0).Select(PumpId).ToArray();

    /// <summary>
    /// The schedule simulated exactly, hour by hour, as the verifiers do: the tank starts at its level, gains each pump's flow
    /// and loses the hour's demand; a pump that is on and was off the hour before (or is on in hour 0) pays its startup cost.
    /// Ids that are not pumps are ignored.
    /// </summary>
    public static List<ScheduleHourRunDto> Simulate(PumpScheduleSpec spec, IReadOnlyList<IReadOnlyCollection<string>> on) {
        var index = Enumerable.Range(0, spec.Pumps.Count).ToDictionary(PumpId, p => p, StringComparer.Ordinal);
        var hours = new List<ScheduleHourRunDto>();
        decimal level = TankLevels.Exact(spec.Level);
        var before = new HashSet<int>();
        for (int h = 0; h < on.Count && h < Hours; h++) {
            var now = on[h].Where(index.ContainsKey).Select(id => index[id]).ToHashSet();
            var ordered = now.Order().ToList();
            double rate = spec.PeakHours.Contains(h) ? spec.OnPeak : spec.OffPeak;
            double inflow = ordered.Sum(p => spec.Pumps[p].Flow);
            double energy = ordered.Sum(p => spec.Pumps[p].Power) * rate;
            var starts = ordered.Where(p => !before.Contains(p)).ToList();
            double startup = starts.Sum(p => spec.Pumps[p].StartupCost);
            level = level + ordered.Sum(p => TankLevels.Exact(spec.Pumps[p].Flow)) - TankLevels.Exact(spec.Demand[h]);
            hours.Add(new ScheduleHourRunDto(HourId(h), ordered.Select(PumpId).ToList(), starts.Select(PumpId).ToList(),
                Round(inflow), Round((double)level), Round(energy), Round(startup)));
            before = now;
        }
        return hours;
    }

    /// <summary>
    /// Whether the tank level after an hour is outside what the problem allows, in exact decimals as the verifiers check it
    /// (#887).
    /// </summary>
    public static bool OutOfLimits(PumpScheduleSpec spec, decimal level) =>
        level < TankLevels.Exact(spec.Minimum) || level > TankLevels.Exact(spec.Capacity);

    // The tank level after hour h, exactly, in decimals as the verifiers simulate it (the DTO's level is rounded for JSON).
    private static List<decimal> ExactLevels(PumpScheduleSpec spec, IReadOnlyList<IReadOnlyCollection<string>> on) {
        var index = Enumerable.Range(0, spec.Pumps.Count).ToDictionary(PumpId, p => p, StringComparer.Ordinal);
        var levels = new List<decimal>();
        decimal level = TankLevels.Exact(spec.Level);
        for (int h = 0; h < on.Count && h < Hours; h++) {
            level = level + on[h].Where(index.ContainsKey).Distinct().Sum(id => TankLevels.Exact(spec.Pumps[index[id]].Flow)) - TankLevels.Exact(spec.Demand[h]);
            levels.Add(level);
        }
        return levels;
    }

    private static double Round(double v) => Math.Round(v, 4);

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static SchedulePumpsFrame PumpsFrame(PumpScheduleSpec spec, PumpPlan partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var on = partial.On.Take(Hours).Select(hour => (IReadOnlyCollection<string>)hour).ToList();
        var schedule = Simulate(spec, on);
        var levels = ExactLevels(spec, on);
        var hourIds = Enumerable.Range(0, Hours).Select(HourId).ToList();
        int focusHour = focus.Length == 1 ? hourIds.IndexOf(focus[0]) : -1;
        int upTo = schedule.Count == 0 ? -1 : ev == StepEvent.Try && focusHour >= 0 ? Math.Min(focusHour, schedule.Count - 1) : schedule.Count - 1;

        var hourStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var cellStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var pumpIndex = Enumerable.Range(0, spec.Pumps.Count).ToDictionary(PumpId, p => p, StringComparer.Ordinal);
        for (int h = 0; h < schedule.Count; h++) {
            foreach (var id in schedule[h].On) GraphSubsetFrames.Raise(cellStates, CellId(pumpIndex[id], h), picked);
            if (h <= upTo && OutOfLimits(spec, levels[h])) GraphSubsetFrames.Raise(hourStates, HourId(h), PictureState.Rejected);
        }
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        var cellIds = new HashSet<string>(Enumerable.Range(0, spec.Pumps.Count).SelectMany(p => Enumerable.Range(0, Hours).Select(h => CellId(p, h))), StringComparer.Ordinal);
        foreach (var id in focus) {
            if (hourIds.Contains(id)) GraphSubsetFrames.Raise(hourStates, id, mark);
            else if (cellIds.Contains(id)) GraphSubsetFrames.Raise(cellStates, id, mark);
        }

        var reached = partial.Reached.Select(r => new ReachedHour(r.States, Round(r.Low), Round(r.High), Round(r.Best))).ToList();
        return new SchedulePumpsFrame(ev, caption, focus, reached, schedule, upTo, GraphSubsetFrames.ToRefs(hourStates), GraphSubsetFrames.ToRefs(cellStates)) {
            Budget = partial.Budget is double b ? Math.Round(b, 2) : null,
            Ok = ok,
        };
    }

    /// <summary>The blank Done frame, ok = false, that closes a run stopped by its time limit: no forward pass, no schedule.</summary>
    public static SchedulePumpsFrame StoppedPumpsFrame(string caption) =>
        new(StepEvent.Done, caption, [], [], [], -1, new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    /// <summary>
    /// A Pump Scheduling certificate as a plan: Cost Minimization writes <c>(cost,((PumpA,h0,…,h23),…))</c>, Emergency
    /// Resilience <c>(budget,cost,((PumpA,h0,…,h23),…))</c> (<paramref name="withBudget"/>). Rows are read by position, one
    /// per pump, each a name and 24 bits. Anything else (<c>{}</c>, no answer, included) is the empty plan.
    /// </summary>
    public static PumpPlan ParsePumpPlan(int pumps, string answer, bool withBudget) {
        try {
            var top = new UtilCollection(answer).ToList();
            if (top.Count != (withBudget ? 3 : 2)) return new PumpPlan([], [], null);
            double? budget = withBudget ? double.Parse(top[0].ToString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture) : null;
            var rows = top[^1].ToList();
            if (rows.Count != pumps) return new PumpPlan([], [], null);
            var on = Enumerable.Range(0, Hours).Select(_ => new List<string>()).ToList();
            for (int p = 0; p < pumps; p++) {
                var parts = rows[p].ToList();
                if (parts.Count != Hours + 1) return new PumpPlan([], [], null);
                for (int h = 0; h < Hours; h++) {
                    string bit = parts[h + 1].ToString().Trim();
                    if (bit == "1") on[h].Add(PumpId(p));
                    else if (bit != "0") return new PumpPlan([], [], null);
                }
            }
            return new PumpPlan([], on, budget);
        } catch {
            return new PumpPlan([], [], null);
        }
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last step
    /// already is the Done step. A run stopped by its time limit ends with <see cref="StoppedPumpsFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse BuildPumps(PumpScheduleSpec spec, SolveRun run, Func<string, PumpPlan> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<PumpPlan>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<PumpPlan>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedPumpsFrame(step.Caption)
                : PumpsFrame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedPumpsFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(PumpsFrame(spec, ok ? parseAnswer(run.Answer) : new PumpPlan([], [], null), StepEvent.Done, [],
                ok ? "This is the solver's schedule." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.Schedule), PumpsPayload(spec), frames);
    }

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>Gallons as captions write them: 4,950, or 0.2 for a fraction.</summary>
    public static string Gallons(double v) => v.ToString("#,0.##", CultureInfo.InvariantCulture);

    /// <summary>Money as captions write it: $32.12.</summary>
    public static string Money(double v) => (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// The tank level after each hour of a schedule and what the whole day costs, both exactly (as the verifiers simulate
    /// it), for captions.
    /// </summary>
    public static (List<double> Levels, double Cost) Exact(PumpScheduleSpec spec, IReadOnlyList<IReadOnlyCollection<string>> on) {
        var levels = ExactLevels(spec, on);
        var hours = Simulate(spec, on);
        return (levels.Select(l => (double)l).ToList(), hours.Sum(h => h.Energy + h.Startup));
    }

    /// <summary>The first hour whose tank level leaves the limits, and that level; null when every hour stays within them.</summary>
    public static (int Hour, double Level)? FirstOutOfLimits(PumpScheduleSpec spec, IReadOnlyList<IReadOnlyCollection<string>> on) {
        var levels = ExactLevels(spec, on);
        for (int h = 0; h < levels.Count; h++)
            if (OutOfLimits(spec, levels[h])) return (h, (double)levels[h]);
        return null;
    }

    /// <summary>
    /// A replay caption for hour <paramref name="hour"/> of a schedule: "At hour 9 (peak), PumpA and PumpB are on. In 550,
    /// out 1,000 gal, so the tank drops to 4,100 gal. Hour cost $1.62." With a <paramref name="budget"/>, it also says how
    /// much of it the day has spent so far.
    /// <para>
    /// A whole caption rather than a phrase, because it describes one hour of the schedule picture from the schedule alone
    /// (its pumps, the water in and out, the tank level, the cost), not a solver's reasoning, and every clause of it branches
    /// on that hour. Both pump-scheduling solvers replay their schedule with it.
    /// </para>
    /// </summary>
    public static string ReplayCaption(PumpScheduleSpec spec, IReadOnlyList<IReadOnlyCollection<string>> on, int hour, double? budget) {
        var hours = Simulate(spec, on);
        var levels = ExactLevels(spec, on);
        var r = hours[hour];
        decimal before = hour == 0 ? TankLevels.Exact(spec.Level) : levels[hour - 1], after = levels[hour];
        var index = Enumerable.Range(0, spec.Pumps.Count).ToDictionary(PumpId, p => p, StringComparer.Ordinal);
        string Names(IEnumerable<string> ids) => Words.List(ids.Select(id => spec.Pumps[index[id]].Name).ToList());
        string pumps = r.On.Count switch { 0 => "no pump is on", 1 => $"{Names(r.On)} is on", _ => $"{Names(r.On)} are on" };
        string verb = after > before ? "rises to" : after < before ? "drops to" : "stays at";
        string text = $"At hour {hour}{(spec.PeakHours.Contains(hour) ? " (peak)" : "")}, {pumps}. In {Gallons(r.Inflow)}, out {Gallons(spec.Demand[hour])} gal, so the tank {verb} {Gallons((double)after)} gal.";
        if (after < TankLevels.Exact(spec.Minimum))
            text += spec.Minimum == 0 ? " That is below empty." : $" That is below the {Gallons(spec.Minimum)}-gal minimum.";
        else if (after > TankLevels.Exact(spec.Capacity))
            text += $" That is over the {Gallons(spec.Capacity)}-gal capacity.";
        text += $" Hour cost {Money(r.Energy + r.Startup)}";
        if (r.Startup > 0) text += $", including {Money(r.Startup)} to start {Names(r.Starts)}";
        text += ".";
        if (budget is double b) text += $" {Money(hours.Take(hour + 1).Sum(x => x.Energy + x.Startup))} of the {Money(b)} budget spent.";
        return text;
    }

    // Phrases of a forward-pass caption, which the pump-scheduling solvers share: $"At hour {h}, the forward pass holds
    // {ReachableStates(states)}, {LevelRange(low, high)}, cheapest so far {Money(cheapest)}." Each is a piece of the
    // sentence that would need a branch inside the why line (state or states, level or levels).

    /// <summary>"64 reachable (tank level, pumps on) states", "1 reachable (tank level, pumps on) state".</summary>
    public static string ReachableStates(int states) =>
        $"{Words.Number(states)} reachable (tank level, pumps on) {(states == 1 ? "state" : "states")}";

    /// <summary>"levels 3,800 to 5,900 gal", or "level 4,000 gal" when the lowest and highest are the same.</summary>
    public static string LevelRange(double low, double high) =>
        low == high ? $"level {Gallons(low)} gal" : $"levels {Gallons(low)} to {Gallons(high)} gal";
}

/// <summary>
/// A visualization for Job Sequencing drawn as the Schedule picture. The visualization declares only what the instance is
/// (<see cref="Describe"/>) and how to read an answer as a <see cref="JobOrder"/> (<see cref="ParseAnswer"/>); the frames hook
/// then comes for free. Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(JobOrder)</c>.
/// </summary>
interface IJobScheduleVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    JobScheduleSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a job order (used only when the run's steps cannot be drawn).</summary>
    JobOrder ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return ScheduleFrames.BuildJobs(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}

/// <summary>
/// A visualization for a Pump Scheduling problem drawn as the Schedule picture. The visualization declares only what the
/// instance is (<see cref="Describe"/>) and how to read an answer as a <see cref="PumpPlan"/> (<see cref="ParseAnswer"/>); the
/// frames hook then comes for free. Implement members explicitly so nothing new appears in /info. The class must also
/// declare <c>Type? IVisualization.StepShape =&gt; typeof(PumpPlan)</c>.
/// </summary>
interface IPumpScheduleVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    PumpScheduleSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a plan (used only when the run's steps cannot be drawn).</summary>
    PumpPlan ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return ScheduleFrames.BuildPumps(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
