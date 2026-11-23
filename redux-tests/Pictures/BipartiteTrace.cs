using API;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace redux_tests;
#pragma warning disable CS1591

// "Why is this piece this colour?" for the Bipartite picture (and the Nested Set, which draws the same states): builds a
// visualization's frames for one run exactly as BuildFrames does, and returns for every frame each piece's state and the
// BipartiteStates rule that set it. A developer's tool for tests and debugging; the API and its endpoints never expose it.
//
//   var trace = BipartiteTrace.Of("SetCoverDefaultVisualization", instance, typeof(SetCoverBruteForce));
//   trace[^1]["e:4"]   // (Covered, "hit")
internal static class BipartiteTrace {
    /// <summary>One frame's pieces: id → (state, rule).</summary>
    internal sealed class Frame : Dictionary<string, (PictureState State, string Rule)> {
        public Frame(IReadOnlyDictionary<string, (PictureState State, string Rule)> why) : base(StringComparer.Ordinal) {
            foreach (var (id, entry) in why) this[id] = entry;
        }

        /// <summary>The pieces not in Background, as "id: State (rule)" lines, for a failure message.</summary>
        public override string ToString() => string.Join("\n", this.Where(kv => kv.Value.State != PictureState.Background)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}: {kv.Value.State} ({kv.Value.Rule})"));
    }

    /// <summary>The trace of <paramref name="visualization"/> drawing a run of <paramref name="solver"/> on <paramref name="instance"/>.</summary>
    internal static List<Frame> Of(string visualization, string instance, Type solver) {
        var picture = PictureContract.Find(visualization) ?? throw new ArgumentException($"No frames visualization named {visualization}.");
        var run = PictureContract.NewSolver(solver).Run(instance, withSteps: true);
        return Of(picture.Visualization, instance, run);
    }

    /// <summary>The trace of a Bipartite or Nested Set visualization drawing <paramref name="run"/> on <paramref name="instance"/>.</summary>
    internal static List<Frame> Of(IFramesVisualization visualization, string instance, SolveRun run) {
        var face = visualization.GetType().GetInterfaces().FirstOrDefault(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IBipartiteVisualization<>) || i.GetGenericTypeDefinition() == typeof(INestedSetVisualization<>)))
            ?? throw new ArgumentException($"{visualization.GetType().Name} is not a Bipartite or Nested Set picture.");
        var trace = typeof(BipartiteTrace).GetMethod(nameof(Trace), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(face.GetGenericArguments()[0]);
        return (List<Frame>)trace.Invoke(null, [visualization, instance, run])!;
    }

    private static List<Frame> Trace<TProblem>(IFramesVisualization visualization, string instance, SolveRun run) where TProblem : IProblem {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        // The same spec, answer reader and verifier as the interface's BuildFrames.
        BipartiteSpec spec;
        Func<string, string[]?> parse;
        if (visualization is IBipartiteVisualization<TProblem> bipartite) {
            spec = bipartite.Describe(problem);
            parse = answer => bipartite.ParseAnswer(problem, answer);
        } else {
            var nested = (INestedSetVisualization<TProblem>)visualization;
            spec = nested.Describe(problem);
            parse = answer => nested.ParseAnswer(problem, answer);
        }
        var (_, why) = BipartiteFrames.BuildTraced(spec, run, parse, answer => {
            try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
        }, visualization);
        return why.Select(w => new Frame(w)).ToList();
    }
}
