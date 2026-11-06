using System.Text;
using System.Text.Json;
using API;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A behaviour capture of every solver that records typed steps, for checking that a refactor changes nothing.
//
// It is opt-in: it runs only when REDUX_CAPTURE_DIR names a folder, and is skipped otherwise, so the normal suite does not
// pay for it. Run it before a change and after, into two folders, and compare them:
//
//   REDUX_CAPTURE_DIR=C:\temp\capture\before dotnet test redux-tests --filter Category=Capture
//   (make the change)
//   REDUX_CAPTURE_DIR=C:\temp\capture\after dotnet test redux-tests --filter Category=Capture
//   diff -r C:\temp\capture\before C:\temp\capture\after
//
// No difference means every answer, every recorded step and every frame is the same. It writes one JSON file per solver
// (named after the solver class) and _summary.txt, which lists what was captured and what could not be.
//
// What it runs, for each solver on each instance: a plain solve (steps off), a run with steps at the default cap and at
// small caps (where Accept past the cap and the cap's last step matter), runs with a time limit on a fake clock (every
// clock read moves it on, so the stop lands on the same step every time) with steps on and off, and the frames of every
// visualization of the problem that draws the solver's step shape. A randomized solver runs with fixed seeds, and so does a
// random solver that takes its seed in a constructor.
//
// The solvers: every class implementing ISolver<TProblem, TPartial>, found by reflection, and the few that record typed
// steps through a Run of their own (listed in OwnRun, with how to call their recording solve).
//
// The instances: the problem's default instance, the instances the snapshot tests use for that problem's solvers or
// visualizations (Endpoints/Snapshots/*.tsv), and a few seeded random ones for the instance shapes it knows how to make (CNF formulas,
// undirected graphs, weighted tours). An instance the problem cannot parse is left out.
public class StepCapture_Tests {
    private const string DirVariable = "REDUX_CAPTURE_DIR";
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, IncludeFields = true };
    private static readonly int?[] Caps = [null, 1, 2, 5, 17];
    private static readonly int[] ClockReads = [2, 5, 13, 60];
    private static readonly int[] Seeds = [1, 7, 4473];
    private const int MaxSnapshotInstances = 12;

    [Fact]
    [Trait("Category", "Capture")]
    public void CaptureEverySolver() {
        string? dir = Environment.GetEnvironmentVariable(DirVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(dir), $"Set {DirVariable} to a folder to capture every solver's steps.");
        Directory.CreateDirectory(dir!);
        using var quantum = FakeQuantumServer.Install(out _);

        var summary = new List<string>();
        var notCaptured = new List<string>();
        var snapshots = SnapshotInstances();
        int totalRuns = 0, solvers = 0;
        var all = RecordingSolvers().ToList();
        foreach (var (solverType, problemType, partialType, direct) in all) {
            var instances = Instances(solverType, problemType, all.Where(s => s.Problem == problemType).Select(s => s.Solver), snapshots, out int unparsed);
            if (instances.Count == 0) {
                notCaptured.Add($"{solverType.Name}: no instance its problem can parse");
                continue;
            }
            var pictures = Pictures(problemType, partialType);
            var capture = typeof(StepCapture_Tests).GetMethod(nameof(CaptureSolver), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(problemType, partialType);
            var record = (Dictionary<string, object?>)capture.Invoke(null, [solverType, instances, pictures, direct])!;
            int runs = record.Values.OfType<Dictionary<string, object?>>().Sum(r => r.Count - 1);
            totalRuns += runs;
            solvers++;
            Write(dir!, solverType.Name, record);
            summary.Add($"{solverType.Name}\t{problemType.Name}\t{partialType.Name}\tinstances {instances.Count}" +
                        $"{(unparsed > 0 ? $" ({unparsed} unparsed)" : "")}\truns {runs}\tpictures {string.Join(",", pictures.Select(p => p.GetType().Name))}" +
                        $"{(typeof(IRandomizedSolver).IsAssignableFrom(solverType) ? "\trandomized" : SeedConstructor(solverType) != null ? "\tseeded" : "")}");
        }
        summary.Insert(0, $"solvers {solvers}, runs {totalRuns}");
        if (notCaptured.Count > 0) summary.Add("not captured:\n  " + string.Join("\n  ", notCaptured));
        File.WriteAllText(Path.Combine(dir!, "_summary.txt"), string.Join("\n", summary) + "\n", new UTF8Encoding(false));
    }

    // The solvers that record typed steps through a Run of their own instead of ISolver<TProblem, TPartial>: their problem,
    // their step shape, and their recording solve for an instance (for the runs on a fake clock).
    private static readonly Dictionary<Type, (Type Problem, Type Partial, object Direct)> OwnRun = new() {
        [typeof(SATBruteForceSolver)] = (typeof(SAT), typeof(Assignment), (Func<ISolver, string, Func<StepRecorder<Assignment>, string>>)((solver, instance) => {
            var formula = ParseGuard.CreateProblem<SAT>(instance).formula;
            return rec => ((SATBruteForceSolver)solver).Solve(formula, rec);
        })),
    };

    // Every concrete class implementing ISolver<TProblem, TPartial>, and those in OwnRun, with its problem, step shape and
    // recording solve, by name.
    internal static IEnumerable<(Type Solver, Type Problem, Type Partial, object Direct)> RecordingSolvers() =>
        typeof(ISolver).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) != null)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>))
                .Select(i => (t, i.GetGenericArguments()[0], i.GetGenericArguments()[1],
                    typeof(StepCapture_Tests).GetMethod(nameof(TypedSolve), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .MakeGenericMethod(i.GetGenericArguments()).Invoke(null, null)!)))
            .Concat(OwnRun.Select(o => (o.Key, o.Value.Problem, o.Value.Partial, o.Value.Direct)))
            .OrderBy(s => s.Item1.Name, StringComparer.Ordinal);

    private static Func<ISolver, string, Func<StepRecorder<TPartial>, string>> TypedSolve<TProblem, TPartial>() where TProblem : IProblem =>
        (solver, instance) => {
            TProblem parsed = ParseGuard.CreateProblem<TProblem>(instance);
            return rec => ((ISolver<TProblem, TPartial>)solver).Solve(parsed, rec);
        };

    // The frames visualizations of the problem that draw this step shape, by name.
    private static List<IFramesVisualization> Pictures(Type problemType, Type partialType) =>
        ProblemProvider.Visualizers.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Value)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVisualization<>) && i.GetGenericArguments()[0] == problemType))
            .Select(t => { try { return Activator.CreateInstance(t); } catch { return null; } })
            .OfType<IFramesVisualization>()
            .Where(v => v.StepShape == partialType)
            .ToList();

    private static Dictionary<string, object?> CaptureSolver<TProblem, TPartial>(Type solverType, List<string> instances, List<IFramesVisualization> pictures,
                                                                              Func<ISolver, string, Func<StepRecorder<TPartial>, string>> direct)
        where TProblem : IProblem {
        bool randomized = typeof(IRandomizedSolver).IsAssignableFrom(solverType);
        var seeded = SeedConstructor(solverType);
        var record = new Dictionary<string, object?>();
        for (int i = 0; i < instances.Count; i++) {
            string instance = instances[i];
            foreach (int? seed in randomized || seeded != null ? Seeds.Select(s => (int?)s).ToArray() : new int?[] { null }) {
                var runs = new Dictionary<string, object?> { ["instance"] = instance };
                ISolver NewSolver() {
                    var solver = seeded != null ? (ISolver)seeded.Invoke([seed!.Value]) : (ISolver)Activator.CreateInstance(solverType)!;
                    RandomizedSolvers.Apply(solver, seed);
                    return solver;
                }
                runs["off"] = Try(() => Describe(NewSolver().Run(instance, withSteps: false)));
                foreach (int? cap in Caps) {
                    string name = cap?.ToString() ?? "default";
                    SolveRun? run = null;
                    runs[$"steps cap {name}"] = Try(() => Describe(run = NewSolver().Run(instance, withSteps: true, null, cap)));
                    if (run != null && (cap is null || cap == 5))
                        foreach (var viz in DrawsSolver(pictures, solverType))
                            runs[$"frames cap {name} {viz.GetType().Name}"] = Frames(viz, instance, run);
                }
                foreach (int reads in ClockReads) {
                    foreach (bool steps in new[] { true, false }) {
                        SolveRun? run = null;
                        runs[$"timed {reads} steps={steps}"] = Try(() => {
                            var solver = NewSolver();
                            var solve = direct(solver, instance);
                            long t = 0;
                            run = SolveRun.Record<TPartial>(steps, TimeSpan.FromMilliseconds(reads), solve, steps ? 7 : null, null, () => t++)
                                with { Incremental = solver is IIncrementalSolver };
                            return Describe(run);
                        });
                        if (run != null && steps && reads == 13)
                            foreach (var viz in DrawsSolver(pictures, solverType))
                                runs[$"timed frames {viz.GetType().Name}"] = Frames(viz, instance, run);
                    }
                }
                record[seed is null ? $"instance {i:D2}" : $"instance {i:D2} seed {seed}"] = runs;
            }
        }
        return record;
    }

    // A random solver that is not an IRandomizedSolver but takes its seed in a constructor (int seed): it runs with fixed seeds
    // through that constructor, since its plain one would draw a new run each time.
    internal static System.Reflection.ConstructorInfo? SeedConstructor(Type solverType) =>
        typeof(IRandomizedSolver).IsAssignableFrom(solverType) ? null
            : solverType.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters() is [{ Name: "seed" } p] && p.ParameterType == typeof(int));

    private static IEnumerable<IFramesVisualization> DrawsSolver(List<IFramesVisualization> pictures, Type solverType) =>
        pictures.Where(p => p.DrawsSolver(solverType));

    private static object Describe(SolveRun run) => new {
        run.Answer, run.TimedOut, run.Incremental, run.Seed, Steps = run.Steps.ToList(),
    };

    private static object? Try(Func<object> run) {
        try { return run(); } catch (Exception e) { return "THREW " + e.GetType().Name + ": " + e.Message; }
    }

    private static string Frames(IFramesVisualization viz, string instance, SolveRun run) {
        try { return JsonSerializer.Serialize(viz.BuildFrames(instance, run), FramesResponse.JsonOptions); } catch (Exception e) { return "THREW " + e.GetType().Name + ": " + e.Message; }
    }

    private static void Write(string dir, string name, object value) =>
        File.WriteAllText(Path.Combine(dir, name + ".json"), JsonSerializer.Serialize(value, Indented), new UTF8Encoding(false));

    // ---- Instances ----

    private static List<string> Instances(Type solverType, Type problemType, IEnumerable<Type> problemSolvers, List<(string Key, string Instance)> snapshots, out int unparsed) {
        var problem = (IProblem)Activator.CreateInstance(problemType)!;
        string problemKey = problemType.Name.ToLowerInvariant();
        string solverKey = solverType.Name.ToLowerInvariant();
        var otherSolvers = problemSolvers.Select(t => t.Name.ToLowerInvariant()).Where(k => k != solverKey).ToHashSet();
        var problemVisualizations = ProblemProvider.Visualizers
            .Where(v => v.Value.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVisualization<>) && i.GetGenericArguments()[0] == problemType))
            .Select(v => v.Key).ToHashSet();

        var kept = new List<string>();
        int failed = 0;
        // Adds the instances the problem parses and that are not in yet, up to `limit` of them.
        void Add(IEnumerable<string> instances, int limit) {
            int added = 0;
            foreach (string instance in instances) {
                if (added == limit) break;
                if (kept.Contains(instance)) continue;
                try {
                    ParseGuard.CreateProblem(problemType, instance);
                    kept.Add(instance);
                    added++;
                } catch {
                    failed++;
                }
            }
        }
        Add([problem.defaultInstance], 1);
        Add(snapshots.Where(s => s.Key == solverKey).Select(s => s.Instance), MaxSnapshotInstances);
        Add(snapshots.Where(s => s.Key == problemKey || s.Key == problemKey + "solver" || problemVisualizations.Contains(s.Key) || otherSolvers.Contains(s.Key))
            .Select(s => s.Instance), MaxSnapshotInstances);
        Add(RandomInstances(problem.defaultInstance), int.MaxValue);
        unparsed = failed;
        return kept;
    }

    // Every (key, instance) row of the snapshot tables: the first column names a solver, a problem or a visualization, the
    // second is the instance (JSON-quoted in some tables).
    private static List<(string Key, string Instance)> SnapshotInstances() {
        string folder = Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots");
        var rows = new List<(string, string)>();
        foreach (string file in Directory.GetFiles(folder, "*.tsv").OrderBy(f => f, StringComparer.Ordinal)) {
            foreach (string line in File.ReadAllLines(file)) {
                string[] parts = line.Split('\t');
                if (parts.Length < 2 || parts[1].Length == 0) continue;
                string instance = parts[1];
                if (instance.StartsWith('"')) {
                    try { instance = JsonSerializer.Deserialize<string>(instance) ?? instance; } catch (JsonException) { }
                }
                rows.Add((parts[0], instance));
            }
        }
        return rows;
    }

    // A few seeded random instances in the shape of the default instance, for the shapes made here; none for the rest.
    private static IEnumerable<string> RandomInstances(string defaultInstance) {
        var rnd = new Random(defaultInstance.Length * 31 + 5573);
        string d = defaultInstance.Trim();
        if (d.Contains('&') && d.Contains('|')) {
            foreach (var (vars, clauses) in new[] { (4, 6), (5, 12), (6, 26), (7, 30) })
                yield return Cnf(rnd, vars, clauses);
        } else if (d.StartsWith("((") && d.Contains("},{(")) {
            foreach (int n in new[] { 4, 5, 6 })
                yield return WeightedTour(rnd, n);
        } else if (d.StartsWith("{{") && d.Contains("},{{")) {
            foreach (int n in new[] { 4, 5, 6 })
                yield return UndirectedGraph(rnd, n, d.EndsWith("}}") ? null : 1 + rnd.Next(n - 1));
        }
    }

    private static string Cnf(Random rnd, int vars, int clauses) =>
        string.Join(" & ", Enumerable.Range(0, clauses).Select(_ =>
            "(" + string.Join(" | ", Enumerable.Range(0, 3).Select(_ => (rnd.Next(2) == 0 ? "!" : "") + "x" + (rnd.Next(vars) + 1))) + ")"));

    private static string WeightedTour(Random rnd, int n) {
        var nodes = Enumerable.Range(1, n).Select(i => "c" + i).ToList();
        var edges = new List<string>();
        int total = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++) {
                int w = rnd.Next(1, 60);
                total += w;
                edges.Add($"({{{nodes[i]},{nodes[j]}}},{w})");
            }
        return $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),{total})";
    }

    private static string UndirectedGraph(Random rnd, int n, int? k) {
        var edges = new List<string>();
        for (int i = 1; i <= n; i++)
            for (int j = i + 1; j <= n; j++)
                if (rnd.Next(2) == 0) edges.Add($"{{{i},{j}}}");
        return $"{{{{{string.Join(",", Enumerable.Range(1, n))}}},{{{string.Join(",", edges)}}}{(k is int kk ? "," + kk : "")}}}";
    }
}
