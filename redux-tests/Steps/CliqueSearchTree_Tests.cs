using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search trees of Carraghan-Pardalos and Chiba-Nishizeki: they link up, a vertex the bound (or the count) rules out is
// pruned with its note, every vertex backed out fails, and the Done ends on the answer's path, whose vertices are a clique
// of size K, or fails the root.
public class CliqueSearchTree_Tests {
    private static ISolver<CLIQUE, NodeSet> Make(string solver) => solver == nameof(CarraghanPardalos) ? new CarraghanPardalos() : new ChibaNishizeki();

    // Small random graphs (seeded), with K from 0 to 4, and a few by hand, for both solvers.
    public static TheoryData<string, string> Instances() {
        var instances = new List<string>();
        var random = new Random(4473);
        for (int t = 0; t < 60; t++) {
            int n = random.Next(1, 7), k = random.Next(0, 5);
            var edges = new List<string>();
            for (int a = 1; a <= n; a++)
                for (int b = a + 1; b <= n; b++)
                    if (random.NextDouble() < 0.5) edges.Add($"{{{a},{b}}}");
            instances.Add($"(({{{string.Join(",", Enumerable.Range(1, n))}}},{{{string.Join(",", edges)}}}),{k})");
        }
        instances.Add(new CLIQUE().defaultInstance);
        instances.Add(Path);
        instances.Add(Square);
        var data = new TheoryData<string, string>();
        foreach (var solver in new[] { nameof(CarraghanPardalos), nameof(ChibaNishizeki) })
            foreach (var instance in instances) data.Add(solver, instance);
        return data;
    }

    // A path 1-2-3 has no triangle: every vertex is ruled out at once.
    public const string Path = "(({1,2,3},{{1,2},{2,3}}),3)";
    // A square 1-2-3-4 has no triangle either, but each vertex has two neighbours, so a vertex is added and backed out.
    public const string Square = "(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),3)";

    private static bool Verifies(CLIQUE problem, string answer) {
        try { return problem.defaultVerifier.verify(problem, answer); } catch { return false; }
    }

    private static string Text(IReadOnlyList<SearchNode>? tree) =>
        string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string solver, string instance) {
        var problem = new CLIQUE(instance);
        var rec = new StepRecorder<NodeSet>(cap: int.MaxValue);
        string answer = Make(solver).Solve(problem, rec);
        Assert.Equal(Make(solver).solve(problem), answer);
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Each step is about its last node, the vertex in focus: a Try's or an Accept's is explored (or the solution), a
        // Reject's pruned and noted, a Backtrack's failed.
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            Assert.Equal(step.Focus[0], node.Label);
            if (step.Event is StepEvent.Try or StepEvent.Accept)
                Assert.Contains(node.State, new[] { SearchNodeState.Explored, SearchNodeState.Solution });
            if (step.Event == StepEvent.Reject) {
                Assert.Equal(SearchNodeState.Pruned, node.State);
                Assert.Matches(solver == nameof(CarraghanPardalos) ? $@"^at most \d+ < K = {problem.K}$" : @"^\d+ left, \d+ needed$", node.Bound);
            }
            if (step.Event == StepEvent.Backtrack) Assert.Equal(SearchNodeState.Failed, node.State);
        }
        var done = rec.Steps[^1];
        bool found = done.Ok == true;
        if (problem.K > 0) Assert.Equal(found, answer != "{}");
        if (!found) {
            var root = done.Tree![^1];
            Assert.Equal((0, SearchNodeState.Failed), (root.NodeId, root.State));
        } else {
            // The path's vertices are the answer's clique.
            var path = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
            Assert.Equal(problem.K, path.Count);
            if (path.Count > 0) Assert.True(Verifies(problem, "{" + string.Join(",", path) + "}"));
            Assert.Equal(answer.Trim('{', '}').Split(',', StringSplitOptions.RemoveEmptyEntries).Order(), path.Order());
        }
        // With every step kept, Done included: every node off the answer's path ends failed or pruned.
        var state = new Dictionary<int, SearchNodeState>();
        foreach (var node in rec.Steps.SelectMany(s => s.Tree ?? [])) state[node.NodeId] = node.State;
        var onPath = found ? done.Tree!.SkipWhile(n => n.ParentId != null).Select(n => n.NodeId).ToHashSet() : [];
        Assert.All(state.Where(kv => !onPath.Contains(kv.Key)), kv => Assert.True(kv.Value is SearchNodeState.Failed or SearchNodeState.Pruned, $"node {kv.Key} ends {kv.Value}"));
    }

    [Fact]
    public void CarraghanPardalos_PrunesWhatTheBoundRulesOut_AndFailsWhatItBacksOut() {
        var rec = new StepRecorder<NodeSet>();
        new CarraghanPardalos().Solve(new CLIQUE(Path), rec);
        Assert.Equal(["0<-:start:Explored 1<0:1:Pruned(at most 2 < K = 3)", "2<0:3:Pruned(at most 2 < K = 3)", "3<0:2:Pruned(at most 1 < K = 3)",
            "0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
        rec = new StepRecorder<NodeSet>();
        new CarraghanPardalos().Solve(new CLIQUE(Square), rec);
        Assert.Equal(["0<-:start:Explored 1<0:1:Explored", "2<1:2:Pruned(at most 2 < K = 3)", "3<1:4:Pruned(at most 2 < K = 3)", "1<0:1:Failed"],
            rec.Steps.Take(4).Select(s => Text(s.Tree)));
        Assert.Equal("0<-:start:Failed", Text(rec.Steps[^1].Tree));
    }

    [Fact]
    public void ChibaNishizeki_StartsEachBranchAtAVertex_AndPrunesWhatTheCountRulesOut() {
        var rec = new StepRecorder<NodeSet>();
        new ChibaNishizeki().Solve(new CLIQUE(Path), rec);
        Assert.Equal(["0<-:start:Explored 1<0:2:Explored", "2<1:1:Pruned(0 left, 1 needed)", "3<1:3:Pruned(0 left, 1 needed)", "1<0:2:Failed",
            "4<0:1:Explored", "4<0:1:Failed", "5<0:3:Explored", "5<0:3:Failed", "0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void ChibaNishizeki_WithKOne_TheStartVertexIsTheSolution() {
        var rec = new StepRecorder<NodeSet>();
        new ChibaNishizeki().Solve(new CLIQUE("(({1,2},{{1,2}}),1)"), rec);
        Assert.Equal(["0<-:start:Explored 1<0:1:Solution", "0<-:start:Explored 1<0:1:Solution"], rec.Steps.Select(s => Text(s.Tree)));
    }

    // The steps up to the cap carry their nodes, and the Done still names the path.
    [Theory]
    [InlineData(nameof(CarraghanPardalos))]
    [InlineData(nameof(ChibaNishizeki))]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath(string solver) {
        var problem = new CLIQUE(new CLIQUE().defaultInstance);
        var rec = new StepRecorder<NodeSet>(cap: 2);
        string answer = Make(solver).Solve(problem, rec);
        Assert.True(Verifies(problem, answer));
        Assert.All(rec.Steps.Take(2), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output, for both solvers.
public class CliqueSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public CliqueSearchTree_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<string> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return body;
    }

    public static TheoryData<string, string, bool> Runs() {
        var data = new TheoryData<string, string, bool>();
        foreach (var solver in new[] { "", "&solver=ChibaNishizeki" }) {
            data.Add(solver, new CLIQUE().defaultInstance, true);
            data.Add(solver, CliqueSearchTree_Tests.Path, false);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenFound(string solver, string instance, bool found) {
        string body = await Visualize($"visualization=CliqueSearchTreeVisualization{solver}&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Clique", instance);
        Assert.Equal(found, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (found) Assert.NotEmpty(green);
        else Assert.Empty(green);
        // A vertex ruled out is drawn dashed.
        if (!found) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
