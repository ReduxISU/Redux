using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Visualizations;

/// <summary>
/// Job Sequencing as the Schedule picture (format=frames): every job as a bar on one time axis with its deadline, the late
/// ones in red, order by order as the selected solver tries them. The list format is a table with one row per job: its
/// time, deadline and penalty and, once solved, when it runs and whether it is late. It is the problem's default
/// visualization.
/// </summary>
class JobSeqVisualization : IVisualization<JOBSEQ, API_TableJSON>, IJobScheduleVisualization<JOBSEQ> {
    public string visualizationName { get; } = "Job Sequencing Schedule Visualization";
    public string visualizationDefinition { get; } = "Draws the jobs on one time axis, run back to back in the order the solver tries, each with its deadline. A job that ends after its deadline is late and its penalty counts; an order works when the late penalties add up to at most K.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new JobSeqBruteForce();

    // The Schedule picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(JobOrder);
    JobScheduleSpec IJobScheduleVisualization<JOBSEQ>.Describe(JOBSEQ problem) => Spec(problem);
    JobOrder IJobScheduleVisualization<JOBSEQ>.ParseAnswer(JOBSEQ problem, string answer) => ScheduleFrames.ParseJobOrder(problem.T.Count, answer);

    /// <summary>The instance as the Schedule picture describes it: its jobs in instance order, and K.</summary>
    internal static JobScheduleSpec Spec(JOBSEQ problem) =>
        new(Enumerable.Range(0, problem.T.Count).Select(i => new ScheduleJob(problem.T[i], problem.D[i], problem.P[i])).ToList(), problem.K);

    public JobSeqVisualization() { }

    // The list format: one row per job, in instance order, with when it runs once there is an answer.
    private static API_TableJSON Table(JOBSEQ problem, JobOrder answer) {
        var spec = Spec(problem);
        var index = Enumerable.Range(0, spec.Jobs.Count).ToDictionary(ScheduleFrames.JobId, i => i, StringComparer.Ordinal);
        var runs = ScheduleFrames.Runs(spec, answer.Order.Select(id => index[id]).ToList()).ToDictionary(r => r.Job, StringComparer.Ordinal);
        var table = new API_TableJSON { title = $"{spec.Jobs.Count} jobs, at most {problem.K} total penalty for late jobs" };
        foreach (var (key, label) in new[] { ("job", "Job"), ("time", "Time"), ("deadline", "Deadline"), ("penalty", "Penalty"), ("runs", "Runs"), ("late", "Late") })
            table.columns.Add(new TableColumn { key = key, label = label });
        for (int i = 0; i < spec.Jobs.Count; i++) {
            var job = spec.Jobs[i];
            runs.TryGetValue(ScheduleFrames.JobId(i), out var run);
            table.rows.Add(new TableRow {
                id = ScheduleFrames.JobId(i),
                cells = new Dictionary<string, string> {
                    ["job"] = i.ToString(),
                    ["time"] = job.Time.ToString(),
                    ["deadline"] = job.Deadline.ToString(),
                    ["penalty"] = job.Penalty.ToString(),
                    ["runs"] = run == null ? "" : $"{run.Start} to {run.End}",
                    ["late"] = run == null ? "" : run.Late ? "yes" : "no",
                },
                color = run == null ? null : run.Late ? "Rejected" : "Solution",
            });
        }
        return table;
    }

    public API_TableJSON visualize(JOBSEQ problem) => Table(problem, new JobOrder([]));

    public API_JSON SolvedVisualization(JOBSEQ problem, string solution) => Table(problem, ScheduleFrames.ParseJobOrder(problem.T.Count, solution));
}
