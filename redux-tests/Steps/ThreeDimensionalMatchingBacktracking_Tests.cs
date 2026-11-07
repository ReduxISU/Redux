using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Solvers;
using API.Problems.NPComplete.NPC_DM3.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// 3-Dimensional Matching Backtracking (#814): it agrees with the brute force, its answers pass the verifier, its steps follow
// the incremental contract, and its search tree links up and ends on the answer's path.
public class ThreeDimensionalMatchingBacktracking_Tests {

    // Small random instances (seeded): |X| = |Y| = |Z| from 1 to 4, or unequal sizes, and up to 10 triples, some planted to
    // form a matching. Few enough triples for the brute force to finish.
    public static IEnumerable<string> RandomInstances() {
        var random = new Random(8140);
        for (int t = 0; t < 120; t++) {
            int n = random.Next(1, 5);
            bool equal = random.Next(4) > 0;
            int ny = equal ? n : random.Next(1, 5), nz = equal ? n : random.Next(1, 5);
            var x = Enumerable.Range(0, n).Select(i => $"a{i}").ToList();
            var y = Enumerable.Range(0, ny).Select(i => $"b{i}").ToList();
            var z = Enumerable.Range(0, nz).Select(i => $"c{i}").ToList();
            var triples = new List<string>();
            if (random.Next(2) == 0 && ny >= n && nz >= n) {
                var py = y.OrderBy(_ => random.Next()).ToList();
                var pz = z.OrderBy(_ => random.Next()).ToList();
                for (int i = 0; i < n; i++) triples.Add($"{{{x[i]},{py[i]},{pz[i]}}}");
            }
            int extra = random.Next(0, 8);
            for (int i = 0; i < extra; i++) triples.Add($"{{{x[random.Next(n)]},{y[random.Next(ny)]},{z[random.Next(nz)]}}}");
            triples = triples.OrderBy(_ => random.Next()).Take(10).ToList();
            yield return $"{{{string.Join(",", x)}}}{{{string.Join(",", y)}}}{{{string.Join(",", z)}}}" + string.Concat(triples);
        }
    }

    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        foreach (var i in RandomInstances()) data.Add(i);
        data.Add(new DM3().defaultInstance);
        data.Add("{a}{b}{c}{a,b,c}");
        data.Add("{a,d}{b,e}{c,f}{a,b,c}{d,b,f}");
        data.Add("{a,d}{b,e}{c,f}{a,b,c}{d,e,f}{a,e,f}");
        data.Add("{a}{a}{b}{a,a,b}");
        data.Add("{a,d}{b,e}{c,f}{a,b,c}");
        return data;
    }

    private static bool Verifies(DM3 p, string answer) {
        try { return p.defaultVerifier.verify(p, answer); } catch { return false; }
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void AgreesWithTheBruteForce_AndItsAnswerPassesTheVerifier(string instance) {
        var p = new DM3(instance);
        bool bruteYes = Verifies(p, new ThreeDimensionalMatchingBruteForce().solve(p));
        string back = new ThreeDimensionalMatchingBacktracking().solve(p);
        Assert.Equal(bruteYes, Verifies(p, back));
        if (!bruteYes) Assert.Equal("{}", back);
        else Assert.Equal(p.X.Count, TripartiteFrames.ParseMatching(DM3Visualization.Spec(p), back).Triples.Length);
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void SameAnswer_WithAndWithoutSteps(string instance) {
        var p = new DM3(instance);
        var rec = new StepRecorder<ChosenTriples>(cap: int.MaxValue);
        Assert.Equal(new ThreeDimensionalMatchingBacktracking().solve(p), new ThreeDimensionalMatchingBacktracking().Solve(p, rec));
        Assert.Equal(StepEvent.Done, rec.Steps[^1].Event);
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string instance) {
        var p = new DM3(instance);
        var rec = new StepRecorder<ChosenTriples>(cap: int.MaxValue);
        string answer = new ThreeDimensionalMatchingBacktracking().Solve(p, rec);
        var parentOf = new Dictionary<int, int?>();
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            Assert.NotNull(step.Tree);
            foreach (var node in step.Tree!) {
                if (parentOf.TryGetValue(node.NodeId, out var parent)) {
                    Assert.Equal(parent, node.ParentId);
                    continue;
                }
                if (node.ParentId is int q) Assert.True(parentOf.ContainsKey(q), $"node {node.NodeId} names parent {q}, which does not exist yet");
                else Assert.Equal(0, node.NodeId);
                parentOf[node.NodeId] = node.ParentId;
            }
        }
        var done = rec.Steps[^1];
        if (!Verifies(p, answer)) {
            Assert.False(done.Ok);
            // A search that ran and found nothing marks the root failed; one that never started names no node.
            if (rec.Steps.Count > 1) Assert.Equal((0, SearchNodeState.Failed), (Assert.Single(done.Tree!).NodeId, done.Tree![0].State));
            else Assert.Null(done.Tree);
            return;
        }
        Assert.True(done.Ok);
        var path = done.Tree!;
        Assert.Equal("start", path[0].Label);
        for (int i = 1; i < path.Count; i++) {
            Assert.Equal(path[i - 1].NodeId, path[i].ParentId);
            Assert.Equal(parentOf[path[i].NodeId], path[i].ParentId);
        }
        Assert.Equal(SearchNodeState.Solution, path[^1].State);
        // The path's triples are the answer's.
        var answerTriples = TripartiteFrames.ParseMatching(DM3Visualization.Spec(p), answer).Triples;
        Assert.Equal(answerTriples, path.Skip(1).Select(n => n.Label).Order(StringComparer.Ordinal));
        Assert.Equal(answerTriples, done.Partial.Triples);
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void Steps_FollowTheIncrementalContract(string instance) {
        var p = new DM3(instance);
        var rec = new StepRecorder<ChosenTriples>(cap: int.MaxValue);
        new ThreeDimensionalMatchingBacktracking().Solve(p, rec);
        foreach (var step in rec.Steps) {
            switch (step.Event) {
                case StepEvent.Accept:
                    Assert.Single(step.Focus);
                    Assert.Contains(step.Focus[0], step.Partial.Triples);
                    break;
                case StepEvent.Reject:
                    Assert.Equal(2, step.Focus.Length);
                    Assert.Contains(step.Focus[0], step.Partial.Triples);
                    Assert.Equal(SearchNodeState.Pruned, step.Tree![^1].State);
                    break;
                case StepEvent.Backtrack:
                    Assert.Single(step.Focus);
                    Assert.DoesNotContain(step.Focus[0], step.Partial.Triples);
                    Assert.Equal(SearchNodeState.Failed, step.Tree![^1].State);
                    break;
                case StepEvent.Done:
                    break;
                default:
                    Assert.Fail($"unexpected {step.Event}");
                    break;
            }
        }
    }

    // An Accept says how many triples are left for the element, with the verb agreeing: "one triple left that uses",
    // "2 triples left that use" (#901).
    [Fact]
    public void AcceptCaptions_AgreeWithHowManyTriplesAreLeft() {
        var p = new DM3("{a,d}{b,e}{c,f}{a,b,c}{a,e,f}{d,e,f}{d,b,c}");
        var rec = new StepRecorder<ChosenTriples>(cap: int.MaxValue);
        new ThreeDimensionalMatchingBacktracking().Solve(p, rec);
        Assert.Equal(new[] {
            "a has 2 triples left that use no element twice. Choose T1 (a, b, c).",
            "d has one triple left that uses no element twice. Choose T3 (d, e, f).",
        }, rec.Steps.Where(s => s.Event == StepEvent.Accept).Select(s => s.Caption));
    }

    [Fact]
    public void IsIncremental_AndNotTheDefaultSolver() {
        Assert.True(new ThreeDimensionalMatchingBacktracking() is IIncrementalSolver);
        Assert.IsType<ThreeDimensionalMatchingBruteForce>(new DM3().defaultSolver);
    }

    [Fact]
    public void StoppedRun_IsTimedOut_WithABlankDoneThatIsNotOk() {
        ISolver solver = new ThreeDimensionalMatchingBacktracking();
        var run = solver.Run(new DM3().defaultInstance, withSteps: true, TimeSpan.Zero);
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        var done = Assert.IsType<SolverStep<ChosenTriples>>(run.Steps[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Empty(done.Partial.Triples);
    }

    [Fact]
    public void SolverTimer_StopsTheSearch_WithNoAnswer() {
        var solver = new ThreeDimensionalMatchingBacktracking { timerHasExpired = true };
        Assert.Equal("{}", solver.solve(new DM3()));
    }
}

// The new solver and its Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class ThreeDimensionalMatchingBacktracking_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ThreeDimensionalMatchingBacktracking_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<string> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return body;
    }

    [Theory]
    [InlineData("{Paul,Sally,Dave}{Madison,Austin,Bob}{Chloe,Frank,Jake}{Paul,Madison,Chloe}{Paul,Austin,Jake}{Sally,Bob,Chloe}{Sally,Madison,Frank}{Dave,Austin,Chloe}{Dave,Bob,Chloe}", true)]
    [InlineData("{a,d}{b,e}{c,f}{a,b,c}{d,b,f}", false)]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWithAMatching(string instance, bool matched) {
        string body = await Visualize("visualization=DM3SearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "3-Dimensional Matching", instance);
        Assert.Equal(matched, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (matched) Assert.NotEmpty(green);
        else Assert.Empty(green);
    }

    [Fact]
    public async Task TripartitePicture_DrawsTheBacktracking_AsAnIncrementalSolver() {
        string instance = new DM3().defaultInstance;
        string body = await Visualize("visualization=DM3Visualization&solver=ThreeDimensionalMatchingBacktracking&format=frames", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Equal(3, frames[^1].GetProperty("chosen").GetArrayLength());
    }
}
