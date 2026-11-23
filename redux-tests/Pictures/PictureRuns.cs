using System.Text.Json;
using API;
using API.Interfaces;

namespace redux_tests;
#pragma warning disable CS1591

// Every frames picture drawn from real runs: each solver of a picture's problem that the picture draws, on the problem's
// default instance and a broader corpus (the capture tool's: the snapshot tables' instances for the problem and a few seeded
// random ones), each solver run once per instance and every picture built from that one run (as visualizeRun does). Built once
// per test process and shared by VerifierAgreement_Tests and the picture doctor.
internal static class PictureRuns {
    /// <summary>At most this many instances per problem (the default instance is always the first).</summary>
    internal const int MaxInstances = 16;

    /// <summary>A run longer than this is stopped, and its pictures are not judged (a stopped run has no answer to judge).</summary>
    internal static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(10);

    /// <summary>One picture of one run.</summary>
    internal sealed record PictureRun(
        PictureContract.Picture Picture, PictureContract.Solver Solver, string Instance, int InstanceIndex, string Answer,
        bool TimedOut, bool Verdict, JsonElement? Final, string? Error, string ResponseType = "", JsonElement? Payload = null) {
        public string? FinalEvent => Final?.GetProperty("event").GetString();
        public bool? FinalOk => Final is { } f && f.TryGetProperty("ok", out var ok) && ok.ValueKind is JsonValueKind.True or JsonValueKind.False ? ok.GetBoolean() : null;
        public int? FinalStep => Final is { } f && f.GetProperty("step").ValueKind == JsonValueKind.Number ? f.GetProperty("step").GetInt32() : null;

        /// <summary>The final frame's states, counted per top-level member: <c>"nodes:Solution" → 3</c>.</summary>
        public SortedDictionary<string, int> FinalStates => Final is { } f ? StatesOf(f) : new(StringComparer.Ordinal);

        /// <summary>How many pieces of the final frame are in <paramref name="state"/>, in any member.</summary>
        public int Count(string state) => FinalStates.Where(kv => kv.Key.EndsWith(":" + state, StringComparison.Ordinal)).Sum(kv => kv.Value);

        /// <summary>The states of the final frame's <paramref name="member"/> (a map of id → { state }, or a list of { state }).</summary>
        public Dictionary<string, string> StatesIn(string member) {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Final is not { } f || !f.TryGetProperty(member, out var m)) return result;
            if (m.ValueKind == JsonValueKind.Object) {
                foreach (var p in m.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("state", out var s)) result[p.Name] = s.GetString()!;
            } else if (m.ValueKind == JsonValueKind.Array) {
                int i = 0;
                foreach (var e in m.EnumerateArray()) {
                    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("state", out var s))
                        result[e.TryGetProperty("id", out var id) ? id.GetString()! : (i).ToString(System.Globalization.CultureInfo.InvariantCulture)] = s.GetString()!;
                    i++;
                }
            }
            return result;
        }

        public string Where => $"{Picture.Name} drawing {Solver.Name} on instance {InstanceIndex} ({Short(Instance)}), answer {Short(Answer)}";
    }

    private static readonly Lazy<IReadOnlyList<PictureRun>> Runs = new(() => Build(PictureContract.Pictures));

    /// <summary>Every picture of every run (built once).</summary>
    internal static IReadOnlyList<PictureRun> All() => Runs.Value;

    /// <summary>The runs of the given pictures only, built now (the picture doctor's one-picture mode).</summary>
    internal static IReadOnlyList<PictureRun> For(IEnumerable<PictureContract.Picture> pictures) => Build(pictures.ToList());

    /// <summary>
    /// The instances the runs of <paramref name="problem"/> use: its default instance first, then the capture tool's corpus, then
    /// any of <see cref="Extra"/>.
    /// </summary>
    internal static List<string> InstancesOf(Type problem) {
        var solvers = ProblemProvider.Solvers.Values.Distinct().Where(t => ProblemProvider.ProblemTypeOf(t, typeof(ISolver<>)) == problem).ToList();
        var instances = solvers.Count == 0 ? [PictureContract.DefaultInstance(problem)]
            : StepCapture_Tests.Instances(solvers[0], problem, solvers, Snapshots.Value, out _).Take(MaxInstances).ToList();
        foreach (string extra in Extra.GetValueOrDefault(problem.Name, []))
            if (!instances.Contains(extra)) instances.Add(extra);
        return instances;
    }

    // Instances the corpus lacks that tell a wrong Describe apart, by problem class name. Set Cover's corpus answers never
    // overlap, so a Set Cover picture told the exactly-once rule would pass on them; here the only cover within K overlaps on 2.
    private static readonly Dictionary<string, string[]> Extra = new(StringComparer.Ordinal) {
        ["SETCOVER"] = ["({1,2,3},{{1,2},{2,3}},2)"],
    };

    private static readonly Lazy<List<(string Key, string Instance)>> Snapshots = new(StepCapture_Tests.SnapshotInstances);

    private static List<PictureRun> Build(IReadOnlyList<PictureContract.Picture> pictures) {
        using var quantum = FakeQuantumServer.Install(out _);
        var result = new List<PictureRun>();
        foreach (var problemPictures in pictures.GroupBy(p => p.Problem)) {
            var instances = InstancesOf(problemPictures.Key);
            // Every solver any of these pictures draws, each run once per instance.
            var solvers = problemPictures.SelectMany(PictureContract.SolversOf).DistinctBy(s => s.Type).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
            foreach (var solver in solvers) {
                var drawing = problemPictures.Where(p => PictureContract.SolversOf(p).Any(s => s.Type == solver.Type)).ToList();
                for (int i = 0; i < instances.Count; i++) {
                    string instance = instances[i];
                    SolveRun run;
                    try {
                        run = PictureContract.NewSolver(solver.Type).Run(instance, withSteps: true, TimeLimit);
                    } catch (Exception e) {
                        // A solver that cannot run on this instance has nothing to draw; that is its own tests' business.
                        foreach (var picture in drawing)
                            result.Add(new(picture, solver, instance, i, "", false, false, null, "solver threw " + e.GetType().Name + ": " + e.Message));
                        continue;
                    }
                    bool verdict = !run.TimedOut && PictureContract.Verify(problemPictures.Key, instance, run.Answer);
                    foreach (var picture in drawing) {
                        // A picture that cannot draw the steps and cannot draw the answer alone is not offered for this run.
                        if (run.StepShape != picture.Visualization.StepShape && !picture.Visualization.DrawsAnswerAlone()) continue;
                        try {
                            var response = PictureContract.Json(picture.Visualization.BuildFrames(instance, run));
                            var frames = response.GetProperty("frames");
                            var final = frames.GetArrayLength() == 0 ? (JsonElement?)null : frames[frames.GetArrayLength() - 1].Clone();
                            result.Add(new(picture, solver, instance, i, run.TimedOut ? "" : run.Answer, run.TimedOut, verdict, final,
                                final == null ? "no frames" : null, response.GetProperty("type").GetString()!, response.GetProperty("payload").Clone()));
                        } catch (Exception e) {
                            result.Add(new(picture, solver, instance, i, run.TimedOut ? "" : run.Answer, run.TimedOut, verdict, null,
                                "BuildFrames threw " + e.GetType().Name + ": " + e.Message));
                        }
                    }
                }
            }
        }
        return result;
    }

    private static SortedDictionary<string, int> StatesOf(JsonElement frame) {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var member in frame.EnumerateObject()) Count(member.Name, member.Value, counts);
        return counts;
    }

    private static void Count(string member, JsonElement value, SortedDictionary<string, int> counts) {
        switch (value.ValueKind) {
            case JsonValueKind.Object:
                foreach (var p in value.EnumerateObject()) {
                    if (p.Name == "state" && p.Value.ValueKind == JsonValueKind.String) {
                        string key = member + ":" + p.Value.GetString();
                        counts[key] = counts.GetValueOrDefault(key) + 1;
                    } else {
                        Count(member, p.Value, counts);
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var e in value.EnumerateArray()) Count(member, e, counts);
                break;
        }
    }

    internal static string Short(string s) => s.Length > 80 ? s[..80] + "…" : s;
}
