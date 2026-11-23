using System.Text.Json;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Bipartite "why this colour" trace (BipartiteTrace): it explains the frames the picture really draws, and names the rule
// behind each state.
public class BipartiteTrace_Tests {
    [Fact]
    public void The_Trace_Has_The_Same_States_As_Every_Frame_Of_Every_Bipartite_And_Nested_Set_Run() {
        using var quantum = FakeQuantumServer.Install(out _);
        int checkedFrames = 0;
        foreach (var picture in PictureContract.Pictures.Where(p => p.Visualization.GetType().GetInterfaces().Any(i => i.IsGenericType &&
                     i.GetGenericTypeDefinition().Name is "IBipartiteVisualization`1" or "INestedSetVisualization`1"))) {
            foreach (string instance in PictureRuns.InstancesOf(picture.Problem))
                foreach (var solver in PictureContract.RecordersOf(picture)) {
                    var run = PictureContract.NewSolver(solver.Type).Run(instance, withSteps: true);
                    var frames = PictureContract.Json(picture.Visualization.BuildFrames(instance, run)).GetProperty("frames");
                    var trace = BipartiteTrace.Of(picture.Visualization, instance, run);
                    Assert.Equal(frames.GetArrayLength(), trace.Count);
                    for (int i = 0; i < trace.Count; i++) {
                        var drawn = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (string member in new[] { "nodes", "edges" })
                            if (frames[i].TryGetProperty(member, out var m))
                                foreach (var p in m.EnumerateObject()) drawn[p.Name] = p.Value.GetProperty("state").GetString()!;
                        // The Nested Set drops the edges (ids with '~'); compare the pieces it draws.
                        bool hasEdges = frames[i].TryGetProperty("edges", out _);
                        var traced = trace[i].Where(kv => kv.Value.State != PictureState.Background && (hasEdges || !kv.Key.Contains('~')))
                            .ToDictionary(kv => kv.Key, kv => kv.Value.State.ToString(), StringComparer.Ordinal);
                        Assert.True(drawn.OrderBy(kv => kv.Key).SequenceEqual(traced.OrderBy(kv => kv.Key)),
                            $"{picture.Name}, {solver.Name}, frame {i}: drawn {Show(drawn)} but traced {Show(traced)}");
                        checkedFrames++;
                    }
                }
        }
        Assert.True(checkedFrames > 100, $"Only {checkedFrames} frames were checked.");
    }

    [Fact]
    public void The_Solved_Frame_Names_Why_Each_Piece_Has_Its_Colour() {
        // Set Cover's default instance: U = {1,...,5}, S1 = {1,2,3}, S2 = {2,4}, S3 = {3,4}, S4 = {4,5}; the cover {S1, S4}.
        const string instance = "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)";
        var last = BipartiteTrace.Of("SetCoverDefaultVisualization", instance, typeof(SetCoverBruteForce))[^1];
        Assert.Equal((PictureState.Solution, "picked, in the confirmed answer"), last["S1"]);
        Assert.Equal((PictureState.Solution, "picked, in the confirmed answer"), last["e:1~S1"]);
        Assert.Equal((PictureState.Covered, "hit"), last["e:4"]);
        Assert.Equal((PictureState.Background, "nothing applies"), last["S2"]);
        Assert.Contains("e:5: Covered (hit)", last.ToString());
    }

    private static string Show(IEnumerable<KeyValuePair<string, string>> states) =>
        "{" + string.Join(", ", states.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}")) + "}";
}
