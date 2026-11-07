using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Vertex Cover Bounded Search Tree's search tree: it links up, a branch the budget cuts off is pruned and one whose
// branches all fail is failed, and the Done ends on the answer's path, whose ends are the cover.
public class VertexCoverBoundedSearchTree_Tests {

    // Small random graphs (seeded), with K from 0 to 4, and a few by hand.
    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        var random = new Random(4473);
        for (int t = 0; t < 80; t++) {
            int n = random.Next(1, 8), k = random.Next(0, 5);
            var nodes = Enumerable.Range(0, n).Select(i => $"v{i}").ToList();
            var edges = new List<string>();
            double p = random.NextDouble();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (random.NextDouble() < p) edges.Add($"{{{nodes[i]},{nodes[j]}}}");
            data.Add($"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),{k})");
        }
        data.Add(new VERTEXCOVER().defaultInstance);
        data.Add("(({1,2,3},{}),1)");
        data.Add("(({1,2,3},{{1,2}}),0)");
        return data;
    }

    private static bool Verifies(VERTEXCOVER g, string answer) {
        try { return g.defaultVerifier.verify(g, answer); } catch { return false; }
    }

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string instance) {
        var g = new VERTEXCOVER(instance);
        var rec = new StepRecorder<NodeSet>(cap: int.MaxValue);
        string answer = new VertexCoverBoundedSearchTree().Solve(g, rec);
        Assert.Equal(new VertexCoverBoundedSearchTree().solve(g), answer);
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Each step is about its last node: an Accept's is the end it takes, a Backtrack's the end it backs out (failed, or
        // pruned, with the note it was cut with, when the budget ran out under it), a Reject's the branch the budget cuts off
        // (pruned, its note the edges left).
        var cutWith = new Dictionary<int, string>();
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event == StepEvent.Accept) Assert.Equal(step.Focus[0], node.Label);
            if (step.Event == StepEvent.Backtrack) Assert.Contains(node.State, new[] { SearchNodeState.Failed, SearchNodeState.Pruned });
            if (step.Event == StepEvent.Backtrack && node.State == SearchNodeState.Pruned) Assert.Equal(cutWith[node.NodeId], node.Bound);
            if (step.Event == StepEvent.Reject) {
                Assert.Equal(SearchNodeState.Pruned, node.State);
                Assert.EndsWith(" uncovered", node.Bound);
                cutWith[node.NodeId] = node.Bound!;
            }
        }
        if (!Verifies(g, answer)) {
            // No cover: the Done marks the root failed, or leaves it pruned when the budget was 0 from the start.
            Assert.False(rec.Steps[^1].Ok);
            var root = Assert.Single(rec.Steps[^1].Tree!);
            Assert.Equal(0, root.NodeId);
            if (cutWith.TryGetValue(0, out var note)) Assert.Equal((SearchNodeState.Pruned, note), (root.State, root.Bound));
            else Assert.Equal((SearchNodeState.Failed, null), (root.State, root.Bound));
            return;
        }
        var ends = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
        Assert.Equal(API.Interfaces.Graphs.GraphParser.GraphParser.parseNodeListWithStringFunctions(answer).Where(v => v.Length > 0).Order(StringComparer.Ordinal), ends.Order(StringComparer.Ordinal));
    }

    // The steps up to the cap carry their nodes, and a node whose step fell past the cap is never named by a kept one,
    // except the Done's path.
    [Fact]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath() {
        var g = new VERTEXCOVER("(({a,b,c,d,e,f,g,h},{{a,b},{a,c},{a,d},{b,e},{c,f},{d,g},{e,h},{f,h},{g,h}}),4)");
        var rec = new StepRecorder<NodeSet>(cap: 5);
        string answer = new VertexCoverBoundedSearchTree().Solve(g, rec);
        Assert.True(Verifies(g, answer));
        Assert.True(rec.Steps.Count <= 7);
        Assert.All(rec.Steps.Take(5), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class VertexCoverBoundedSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public VertexCoverBoundedSearchTree_Endpoint_Tests(AppFactory factory) {
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
    [InlineData("(({a,b,c,d,e,f},{{a,b},{a,c},{b,c},{c,d},{d,e},{e,f}}),3)", true)]
    [InlineData("(({1,2,3,4,5},{{1,2},{1,3},{1,5},{2,3},{2,4},{3,4}}),2)", false)]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenCovered(string instance, bool covered) {
        string body = await Visualize("visualization=VertexCoverSearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Vertex Cover", instance);
        Assert.Equal(covered, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (covered) Assert.NotEmpty(green);
        else Assert.Empty(green);
        // A branch the budget cuts off is drawn dashed.
        if (!covered) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
