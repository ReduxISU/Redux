using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Graph Coloring Backtracking (#814): it agrees with the brute force, its answers pass the verifier, its steps follow the
// incremental contract, and its search tree links up and ends on the answer's path.
public class GraphColoringBacktracking_Tests {

    // Small random graphs (seeded), with K from 1 to 4: few enough nodes for the brute force to finish.
    public static IEnumerable<string> RandomInstances() {
        var random = new Random(814);
        for (int t = 0; t < 120; t++) {
            int n = random.Next(1, 7), k = random.Next(1, 5);
            var nodes = Enumerable.Range(0, n).Select(i => $"n{i}").ToList();
            var edges = new List<string>();
            double p = random.NextDouble();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (random.NextDouble() < p) edges.Add($"{{{nodes[i]},{nodes[j]}}}");
            yield return $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),{k})";
        }
    }

    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        foreach (var i in RandomInstances()) data.Add(i);
        data.Add(new GRAPHCOLORING().defaultInstance);
        data.Add("(({a,b,c},{{a,b},{b,c},{a,c}}),2)");
        data.Add("(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        data.Add("(({a},{}),1)");
        return data;
    }

    private static bool Verifies(GRAPHCOLORING g, string answer) {
        try { return g.defaultVerifier.verify(g, answer); } catch { return false; }
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void AgreesWithTheBruteForce_AndItsAnswerPassesTheVerifier(string instance) {
        var g = new GRAPHCOLORING(instance);
        string brute = new GraphColoringBruteForce().solve(g);
        string back = new GraphColoringBacktracking().solve(g);
        bool bruteYes = Verifies(g, brute), backYes = Verifies(g, back);
        Assert.Equal(bruteYes, backYes);
        if (!backYes) Assert.Equal("{}", back);
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void SameAnswer_WithAndWithoutSteps(string instance) {
        var g = new GRAPHCOLORING(instance);
        var rec = new StepRecorder<Groups>(cap: int.MaxValue);
        Assert.Equal(new GraphColoringBacktracking().solve(g), new GraphColoringBacktracking().Solve(g, rec));
        Assert.Equal(StepEvent.Done, rec.Steps[^1].Event);
    }

    // Every patch's parent is the root or a node made earlier (in an earlier step or earlier in the same one); each node is
    // made once, under one parent; the Done step of a coloring names a path from the root whose labels are the answer.
    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string instance) {
        var g = new GRAPHCOLORING(instance);
        var rec = new StepRecorder<Groups>(cap: int.MaxValue);
        string answer = new GraphColoringBacktracking().Solve(g, rec);
        var parentOf = new Dictionary<int, int?>();
        var labelOf = new Dictionary<int, string>();
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            Assert.NotNull(step.Tree);
            foreach (var node in step.Tree!) {
                if (parentOf.TryGetValue(node.NodeId, out var parent)) {
                    Assert.Equal(parent, node.ParentId);
                    Assert.Equal(labelOf[node.NodeId], node.Label);
                    continue;
                }
                if (node.ParentId is int p) Assert.True(parentOf.ContainsKey(p), $"node {node.NodeId} names parent {p}, which does not exist yet");
                else Assert.Equal(0, node.NodeId);
                parentOf[node.NodeId] = node.ParentId;
                labelOf[node.NodeId] = node.Label;
            }
        }
        var done = rec.Steps[^1];
        if (!Verifies(g, answer)) {
            Assert.False(done.Ok);
            return;
        }
        Assert.True(done.Ok);
        var path = done.Tree!;
        Assert.Equal("start", path[0].Label);
        Assert.Null(path[0].ParentId);
        for (int i = 1; i < path.Count; i++) {
            Assert.Equal(path[i - 1].NodeId, path[i].ParentId);
            Assert.Equal(parentOf[path[i].NodeId], path[i].ParentId);
        }
        Assert.Equal(SearchNodeState.Solution, path[^1].State);
        // The path colors every node once, with the answer's color.
        var classes = GraphGroupFrames.ParseClasses(answer).Of;
        var onPath = path.Skip(1).Select(n => n.Label.Split('=')).ToDictionary(x => x[0], x => int.Parse(x[1]));
        Assert.Equal(g.nodes.Distinct().Order(StringComparer.Ordinal), onPath.Keys.Order(StringComparer.Ordinal));
        Assert.All(onPath, p => Assert.Equal(classes[p.Key], p.Value));
        Assert.Equal(new Groups(onPath), done.Partial);
    }

    // The incremental contract: an Accept's partial holds its node; a Reject's names the node and the clashing edge, which
    // the partial shows as a clash; a Backtrack's partial no longer holds its node.
    [Theory]
    [MemberData(nameof(Instances))]
    public void Steps_FollowTheIncrementalContract(string instance) {
        var g = new GRAPHCOLORING(instance);
        var rec = new StepRecorder<Groups>(cap: int.MaxValue);
        new GraphColoringBacktracking().Solve(g, rec);
        foreach (var step in rec.Steps) {
            switch (step.Event) {
                case StepEvent.Accept:
                    Assert.Single(step.Focus);
                    Assert.True(step.Partial.Of.ContainsKey(step.Focus[0]));
                    break;
                case StepEvent.Reject:
                    Assert.Equal(2, step.Focus.Length);
                    Assert.True(step.Partial.Of.ContainsKey(step.Focus[0]));
                    Assert.Equal(SearchNodeState.Pruned, step.Tree![^1].State);
                    break;
                case StepEvent.Backtrack:
                    Assert.Single(step.Focus);
                    Assert.False(step.Partial.Of.ContainsKey(step.Focus[0]));
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

    [Fact]
    public void IsIncremental_AndNotTheDefaultSolver() {
        Assert.True(new GraphColoringBacktracking() is IIncrementalSolver);
        Assert.IsType<GraphColoringBruteForce>(new GRAPHCOLORING().defaultSolver);
    }

    [Fact]
    public void StoppedRun_IsTimedOut_WithABlankDoneThatIsNotOk() {
        ISolver solver = new GraphColoringBacktracking();
        var run = solver.Run(new GRAPHCOLORING().defaultInstance, withSteps: true, TimeSpan.Zero);
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        var done = Assert.IsType<SolverStep<Groups>>(run.Steps[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Empty(done.Partial.Of);
    }

    [Fact]
    public void SolverTimer_StopsTheSearch_WithNoAnswer() {
        var solver = new GraphColoringBacktracking { timerHasExpired = true };
        Assert.Equal("{}", solver.solve(new GRAPHCOLORING()));
    }

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class Timing {
        // A graph whose brute force takes ages (12 nodes, K = 3: 4^12 candidates) is quick for the backtracking.
        [Fact]
        public void SolvesWhatTheBruteForceCannot_Quickly() {
            string instance = "(({a,b,c,d,e,f,g,h,i,j,k,l},{{a,b},{b,c},{c,d},{d,e},{e,f},{f,g},{g,h},{h,i},{i,j},{j,k},{k,l},{l,a},{a,g},{c,i},{e,k}}),3)";
            var g = new GRAPHCOLORING(instance);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string answer = new GraphColoringBacktracking().solve(g);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"{clock.Elapsed}");
            Assert.True(Verifies(g, answer), answer);
        }
    }
}

// The new solver and its Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class GraphColoringBacktracking_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GraphColoringBacktracking_Endpoint_Tests(AppFactory factory) {
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
    [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)", true)]
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),3)", false)]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenColored(string instance, bool colorable) {
        string body = await Visualize("visualization=GraphColoringSearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Graph Coloring", instance);
        Assert.Equal(colorable, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (colorable) Assert.NotEmpty(green);
        else Assert.Empty(green);
    }

    [Fact]
    public async Task GraphPicture_DrawsTheBacktracking_AsAnIncrementalSolver() {
        string instance = "(({a,b,c},{{a,b},{b,c},{a,c}}),3)";
        string body = await Visualize("visualization=GraphColoringDefaultVisualization&solver=GraphColoringBacktracking&format=frames", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Equal(3, frames[^1].GetProperty("groups").EnumerateObject().Count());
    }
}
