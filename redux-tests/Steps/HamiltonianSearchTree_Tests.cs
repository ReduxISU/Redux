using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_HAMILTONIAN;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search tree of Hamiltonian Brute Force: it links up, a node with no edge to it from the node before is pruned (and so is
// a whole tour at the first hop that is not an edge), every node given up fails, and the Done ends on the answer's path,
// the tour's nodes in order, or fails the root.
public class HamiltonianSearchTree_Tests {

    // Small random graphs (seeded), and a few by hand.
    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        var random = new Random(4473);
        for (int t = 0; t < 60; t++) {
            int n = random.Next(0, 6);
            var edges = new List<string>();
            for (int a = 1; a <= n; a++)
                for (int b = a + 1; b <= n; b++)
                    if (a != b && random.NextDouble() < 0.5) edges.Add($"{{{a},{b}}}");
            data.Add($"({{{string.Join(",", Enumerable.Range(1, n))}}},{{{string.Join(",", edges)}}})");
        }
        data.Add(new HAMILTONIAN().defaultInstance);
        data.Add(DeadEnd);
        return data;
    }

    // A path 1–2–3 has no cycle: the walk reaches every node, but never back to its start.
    public const string DeadEnd = "({1,2,3},{{1,2},{2,3}})";

    private static string Text(IReadOnlyList<SearchNode>? tree) =>
        string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string instance) {
        var problem = new HAMILTONIAN(instance);
        var rec = new StepRecorder<Tour>(cap: int.MaxValue);
        string answer = new HamiltonianBruteForce().Solve(problem, rec);
        Assert.Equal(new HamiltonianBruteForce().solve(problem), answer);
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Each step is about its last node: a Try's or an Accept's is explored (a walk's is the node in focus), or the
        // solution for the whole tour; a Reject's is pruned at a hop that is not an edge; a Backtrack's fails.
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event is StepEvent.Try or StepEvent.Accept) Assert.Equal(step.Focus[0], node.Label);
            if (step.Event == StepEvent.Try) Assert.Equal(SearchNodeState.Explored, node.State);
            if (step.Event == StepEvent.Accept) Assert.Contains(node.State, new[] { SearchNodeState.Explored, SearchNodeState.Solution });
            if (step.Event == StepEvent.Reject) {
                Assert.Equal(SearchNodeState.Pruned, node.State);
                Assert.Matches(@"^no edge \S+–\S+$", node.Bound);
            }
            if (step.Event == StepEvent.Backtrack) Assert.Equal(SearchNodeState.Failed, node.State);
        }
        // Only the whole tour the verifier agrees with, the last step before the Done, is the solution.
        Assert.All(rec.Steps.SkipLast(2), s => Assert.NotEqual(SearchNodeState.Solution, s.Tree![^1].State));
        var done = rec.Steps[^1];
        bool found = answer != "{}";
        Assert.Equal(found, done.Ok);
        if (problem.nodes.Count == 0) {
            Assert.Null(done.Tree);
        } else if (!found) {
            var root = done.Tree![^1];
            Assert.Equal((0, SearchNodeState.Failed), (root.NodeId, root.State));
        } else {
            // The path's nodes are the tour's, in order.
            var path = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
            Assert.Equal(answer.Trim('{', '}').Split(',').SkipLast(1), path);
        }
        // With every step kept, Done included: every node off the answer's path ends failed or pruned.
        var state = new Dictionary<int, SearchNodeState>();
        foreach (var node in rec.Steps.SelectMany(s => s.Tree ?? [])) state[node.NodeId] = node.State;
        var onPath = found ? done.Tree!.SkipWhile(n => n.ParentId != null).Select(n => n.NodeId).ToHashSet() : [];
        Assert.All(state.Where(kv => !onPath.Contains(kv.Key)), kv => Assert.True(kv.Value is SearchNodeState.Failed or SearchNodeState.Pruned, $"node {kv.Key} ends {kv.Value}"));
    }

    [Fact]
    public void ANodeWithNoEdgeToIt_IsPruned_AndANodeGivenUpFails() {
        var rec = new StepRecorder<Tour>();
        new HamiltonianBruteForce().Solve(new HAMILTONIAN(DeadEnd), rec);
        Assert.Equal(["0<-:start:Explored 1<0:1:Explored", "2<1:2:Explored", "3<2:3:Pruned(no edge 3–1)", "2<1:2:Failed", "4<1:3:Pruned(no edge 1–3)", "1<0:1:Failed"], rec.Steps.Take(6).Select(s => Text(s.Tree)));
        Assert.Equal("0<-:start:Failed", Text(rec.Steps[^1].Tree));
    }

    // The steps up to the cap carry their nodes, and the Done still names the path.
    [Fact]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath() {
        var problem = new HAMILTONIAN(new HAMILTONIAN().defaultInstance);
        var rec = new StepRecorder<Tour>(cap: 3);
        string answer = new HamiltonianBruteForce().Solve(problem, rec);
        Assert.NotEqual("{}", answer);
        Assert.All(rec.Steps.Take(3), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class HamiltonianSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public HamiltonianSearchTree_Endpoint_Tests(AppFactory factory) {
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
    [InlineData("({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})", true)]
    [InlineData(HamiltonianSearchTree_Tests.DeadEnd, false)]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenFound(string instance, bool found) {
        string body = await Visualize("visualization=HamiltonianSearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Hamiltonian", instance);
        Assert.Equal(found, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (found) Assert.NotEmpty(green);
        else Assert.Empty(green);
        if (!found) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
