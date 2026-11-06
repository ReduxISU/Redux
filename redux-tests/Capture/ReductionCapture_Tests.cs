using System.Net;
using System.Text;
using System.Text.Json;
using API;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A behaviour capture of every reduction that records steps (construction steps, a backward map, or both), for checking
// that a refactor changes nothing. The reductions' half of StepCapture_Tests (which covers the solvers), opt-in the same
// way: it runs only when REDUX_CAPTURE_DIR names a folder, and both write into that folder, so one
//
//   REDUX_CAPTURE_DIR=C:\temp\capture\before dotnet test redux-tests --filter Category=Capture
//
// captures every solver and every reduction. It writes one JSON file per reduction (named after the reduction class) and
// _reductions_summary.txt, which lists what was captured and what could not be.
//
// What it runs, for each reduction on each instance:
//   - the plain build (recording off): the target instance and the gadgets, and what /reduce and /gadgets return;
//   - the build recorded at the default cap and at small caps (1, 2, 5), and uncapped (as /buildSteps and the Reduction
//     View record it), then folded by ConstructionSteps.Limit at the usual cap and at small ones; BuildRun with steps on
//     and off; and what /buildSteps returns;
//   - the backward map of "{}" (no answer), of a wrong answer for the reductions listed in WrongAnswers and, on the
//     instances the Reduction View tests use, of the answer the view's solver finds for the target: MapBackRun with steps
//     on and off, and MapBack with recorders at small caps;
//   - on those same instances, what /reductionView returns (randomized solvers with a fixed seed).
//
// The reductions: every reduction ProblemProvider lists that implements IRecordedReduction<T, U> or
// IReversibleReduction<T, U, TPartial>, found by reflection.
//
// The instances: the source problem's default instance (as the reduction's default constructor reduces it), the instances
// of the Reduction View and reduction-output snapshot tests for that reduction, and a few seeded random ones in the shape of
// the default instance (built and mapped back from "{}" only: their target can be too big to solve in a fixed time). The
// Reduction View and the solved backward map run only on the snapshot tests' instances, which those tests keep small enough
// to solve well within the view's time limit, so the capture does not depend on how fast the machine is.
public class ReductionCapture_Tests : IClassFixture<AppFactory> {
    private const string DirVariable = "REDUX_CAPTURE_DIR";
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, IncludeFields = true };
    private static readonly int?[] Caps = [null, 1, 2, 5];
    private static readonly int[] LimitCaps = [StepRecorder<BuildDelta>.DefaultCap, 1, 2, 5];
    private static readonly int?[] MapCaps = [1, 2, 5];
    private const int Seed = 4473;
    // The random instances made for a shape: the first three of StepCapture_Tests' (the third is past the usual cap of 150
    // construction steps for the CNF reductions, so the folded step is captured too).
    private const int RandomInstanceCount = 3;
    private readonly HttpClient _client;

    public ReductionCapture_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Capture")]
    public async Task CaptureEveryReduction() {
        string? dir = Environment.GetEnvironmentVariable(DirVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(dir), $"Set {DirVariable} to a folder to capture every reduction's steps.");
        Directory.CreateDirectory(dir!);

        var known = KnownInstances();
        var summary = new List<string>();
        int reductions = 0, totalRuns = 0;
        foreach (var (name, type) in ProblemProvider.Reductions.OrderBy(r => r.Key, StringComparer.Ordinal)) {
            bool builds = Implements(type, typeof(IRecordedReduction<,>));
            Type? partial = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReversibleReduction<,,>))
                ?.GetGenericArguments()[2];
            if (!builds && partial == null) continue;
            // The reductions the tests add for themselves (TestOnlyReductions) are not part of the app.
            if (type.Assembly != typeof(IReduction).Assembly) continue;

            var defaultReduction = (IReduction)Activator.CreateInstance(type)!;
            var solved = known.Where(k => k.Reduction == name).Select(k => k.Instance).Distinct().ToList();
            var instances = new List<(string Instance, bool Solve)> { (defaultReduction.reductionFrom.instance, solved.Contains(defaultReduction.reductionFrom.instance)) };
            foreach (string instance in solved)
                if (!instances.Any(i => i.Instance == instance)) instances.Add((instance, true));
            foreach (string instance in StepCapture_Tests.RandomInstances(defaultReduction.reductionFrom.defaultInstance).Take(RandomInstanceCount))
                if (!instances.Any(i => i.Instance == instance)) instances.Add((instance, false));

            var record = new Dictionary<string, object?>();
            int runs = 0;
            for (int i = 0; i < instances.Count; i++) {
                var (instance, solve) = instances[i];
                var runsOf = new Dictionary<string, object?> { ["instance"] = instance };
                runsOf["reduce off"] = Try(() => Built(type, instance));
                foreach (string endpoint in new[] { "reduce", "gadgets" })
                    runsOf[$"endpoint {endpoint}"] = await Post($"{endpoint}?reduction={type.Name}", instance);
                if (builds) Build(type, instance, runsOf);
                if (builds) runsOf["endpoint buildSteps"] = await Post($"buildSteps?reduction={type.Name}", instance);
                if (partial != null) {
                    var mapBack = typeof(ReductionCapture_Tests).GetMethod(nameof(MapBack), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .MakeGenericMethod(partial);
                    mapBack.Invoke(null, [type, instance, solve, runsOf]);
                    if (builds && solve)
                        runsOf["endpoint reductionView"] = await Post($"reductionView?reduction={type.Name}&seed={Seed}", instance);
                }
                runs += runsOf.Count - 1;
                record[$"instance {i:D2}"] = runsOf;
            }
            Write(dir!, type.Name, record);
            reductions++;
            totalRuns += runs;
            summary.Add($"{type.Name}\t{defaultReduction.reductionFrom.GetType().Name} -> {defaultReduction.reductionTo.GetType().Name}" +
                        $"\tinstances {instances.Count} (solved {instances.Count(i => i.Solve)})\truns {runs}" +
                        $"\t{(builds ? "build" : "no build steps")}\t{(partial != null ? "map back " + partial.Name : "no backward map")}");
        }
        summary.Insert(0, $"reductions {reductions}, runs {totalRuns}");
        File.WriteAllText(Path.Combine(dir!, "_reductions_summary.txt"), string.Join("\n", summary) + "\n", new UTF8Encoding(false));
    }

    private static bool Implements(Type type, Type generic) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic);

    // Every (reduction, instance) the Reduction View and reduction-output snapshot tests use, by the reduction's lower-case name.
    private static List<(string Reduction, string Instance)> KnownInstances() =>
        Reduction_Output_Snapshot_Tests.Cases()
            .Concat(ReductionView_Graph_Tests.Cases().Select(c => (c.Reduction, c.Instance)))
            .Concat(ReductionView_Sat3_Tests.Cases())
            .Concat(ReductionView_SetNumber_Tests.Cases().Select(c => (c.Reduction, c.Instance)))
            .Select(c => (c.Item1.ToLowerInvariant(), c.Item2))
            .ToList();

    private static IReduction New(Type type, string instance) => (IReduction)Activator.CreateInstance(type, instance)!;

    // The plain build: what reduce() made, and the gadgets.
    private static object Built(Type type, string instance) {
        var red = New(type, instance);
        return new { Target = red.reductionTo.instance, Gadgets = red.gadgets };
    }

    private static object Describe(ConstructionRun run) => new { Target = run.Target.instance, run.Gadgets, run.Steps };

    private static void Build(Type type, string instance, Dictionary<string, object?> runs) {
        foreach (int? cap in Caps) {
            runs[$"rebuild cap {cap?.ToString() ?? "default"}"] = Try(() => {
                var rec = cap is int c ? new StepRecorder<BuildDelta>(c) : new StepRecorder<BuildDelta>();
                var run = (ConstructionRun)type.GetMethod("Rebuild")!.Invoke(New(type, instance), [rec])!;
                return new { Run = Describe(run), rec.Total, rec.Hidden };
            });
        }
        // Uncapped, through the recording constructor, as /buildSteps and the Reduction View record it; then folded.
        IReadOnlyList<SolverStep<BuildDelta>>? all = null;
        runs["record uncapped"] = Try(() => {
            var rec = new StepRecorder<BuildDelta>(cap: int.MaxValue);
            var red = (IReduction)Activator.CreateInstance(type, instance, rec)!;
            all = rec.Steps;
            return new { Target = red.reductionTo.instance, Gadgets = red.gadgets, rec.Total, Steps = rec.Steps };
        });
        if (all != null)
            foreach (int cap in LimitCaps) {
                var steps = all;
                runs[$"limit {cap}"] = Try(() => cap == StepRecorder<BuildDelta>.DefaultCap
                    ? ConstructionSteps.Limit(steps) : ConstructionSteps.Limit(steps, cap));
            }
        runs["build run steps=false"] = Try(() => Describe(New(type, instance).BuildRun(false)!));
        runs["build run steps=true"] = Try(() => Describe(New(type, instance).BuildRun(true)!));
    }

    // The backward map of "{}" and, when `solve`, of the answer the Reduction View's solver finds for the target.
    private static void MapBack<TPartial>(Type type, string instance, bool solve, Dictionary<string, object?> runs) {
        var answers = new List<(string Name, string Answer)> { ("no answer", ReductionBack.NoAnswer) };
        if (solve) {
            string? answer = null;
            runs["target solve"] = Try(() => {
                var red = New(type, instance);
                ISolver solver = red.ReductionViewSolver() ?? red.reductionTo.defaultVisualization?.solver ?? red.reductionTo.defaultSolver;
                RandomizedSolvers.Apply(solver, Seed);
                answer = solver.solve(red.reductionTo.instance);
                return new { Solver = solver.GetType().Name, Answer = answer };
            });
            if (answer != null) answers.Add(("solved", answer));
        }
        if (WrongAnswers.TryGetValue(type.Name, out var wrong)) {
            string? answer = null;
            runs["wrong answer"] = Try(() => answer = wrong(New(type, instance)));
            if (answer != null) answers.Add(("wrong", answer));
        }
        foreach (var (name, answer) in answers) {
            foreach (bool steps in new[] { false, true }) {
                runs[$"map back {name} steps={steps}"] = Try(() => {
                    var run = New(type, instance).MapBackRun(answer, steps)!;
                    return new { run.Answer, Steps = run.Steps.ToList() };
                });
            }
            if (ReductionBack.IsNoAnswer(answer)) continue;
            foreach (int? cap in MapCaps) {
                runs[$"map back {name} cap {cap}"] = Try(() => {
                    var rec = new StepRecorder<TPartial>(cap!.Value);
                    var (mapped, partial) = ((string, TPartial))type.GetMethod("MapBack")!.Invoke(New(type, instance), [answer, rec])!;
                    return new { Answer = mapped, Partial = partial, Steps = rec.Steps, rec.Total };
                });
            }
        }
    }

    // A target answer that is not a real one, for the steps a backward map records only then (a node it ignores), by
    // reduction class: built from the target of the reduction on the instance.
    private static readonly Dictionary<string, Func<IReduction, string>> WrongAnswers = new() {
        // Every node of the clique graph: two nodes of one clause, and contradicting literals, so later nodes are ignored.
        ["SipserReduceToCliqueStandard"] = red => "{" + string.Join(",", ((API.Problems.NPComplete.NPC_CLIQUE.CLIQUE)red.reductionTo).nodes) + "}",
    };

    private async Task<string> Post(string path, string instance) {
        try {
            var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/ProblemProvider/" + path, content, TestContext.Current.CancellationToken);
            string body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n").Replace(@"\r\n", @"\n");
            return response.StatusCode == HttpStatusCode.OK ? body : $"{(int)response.StatusCode} {body}";
        } catch (Exception e) {
            return "THREW " + e.GetType().Name + ": " + e.Message;
        }
    }

    private static object? Try(Func<object> run) {
        try { return run(); } catch (Exception e) {
            if (e is System.Reflection.TargetInvocationException { InnerException: { } inner }) e = inner;
            return "THREW " + e.GetType().Name + ": " + e.Message;
        }
    }

    private static void Write(string dir, string name, object value) =>
        File.WriteAllText(Path.Combine(dir, name + ".json"), JsonSerializer.Serialize(value, Indented), new UTF8Encoding(false));
}
