using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_DOMINATINGSET;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search trees of Forced-Vertex Branch-and-Reduce and Naive Inclusion-Exclusion: they link up, a pick or decision past
// K, or one that leaves vertices undominated, is pruned (and stays pruned when it is backed out), every other choice given
// up fails, and the Done ends on the answer's path, whose vertices are the answer, or fails the root.
public class DominatingSetSearchTree_Tests {
    private static ISolver<DOMINATINGSET, NodeSet> Make(string solver) =>
        solver == nameof(DominatingSetForcedVertex) ? new DominatingSetForcedVertex() : new DominatingSetNaiveInclusionExclusion();

    // Small random graphs (seeded, some with vertices that have no neighbours, so forced picks), and a few by hand, for both
    // solvers.
    public static TheoryData<string, string> Instances() {
        var instances = new List<string>();
        var random = new Random(4473);
        for (int t = 0; t < 60; t++) {
            int n = random.Next(0, 7), k = random.Next(0, 4);
            var edges = new List<string>();
            for (int a = 1; a <= n; a++)
                for (int b = a + 1; b <= n; b++)
                    if (random.NextDouble() < 0.35) edges.Add($"{{{a},{b}}}");
            instances.Add($"(({{{string.Join(",", Enumerable.Range(1, n))}}},{{{string.Join(",", edges)}}}),{k})");
        }
        instances.Add(new DOMINATINGSET().defaultInstance);
        instances.Add(DeadEnd);
        instances.Add(NaiveDeadEnd);
        instances.Add(PastK);
        var data = new TheoryData<string, string>();
        foreach (var solver in new[] { nameof(DominatingSetForcedVertex), nameof(DominatingSetNaiveInclusionExclusion) })
            foreach (var instance in instances) data.Add(solver, instance);
        return data;
    }

    // 5 has no neighbours, so it is a forced pick; one pick is left, and neither 1 nor 2 dominates 3 and 4 as well.
    public const string DeadEnd = "(({1,2,3,4,5},{{1,2},{3,4}}),2)";

    // With K = 0 every include is over K, and the empty set leaves all three vertices undominated.
    public const string NaiveDeadEnd = "(({1,2,3},{{1,2},{2,3}}),0)";

    // 2 has no neighbours, so it is a forced pick and uses up K = 1. Forced Vertex then stops at the first pick that
    // dominates the rest, past K, so the set found does not verify.
    public const string PastK = "(({1,2,3},{{1,3}}),1)";

    private static bool Verifies(DOMINATINGSET problem, string answer) {
        try { return problem.defaultVerifier.verify(problem, answer); } catch { return false; }
    }

    private static string Text(IReadOnlyList<SearchNode>? tree) =>
        string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string solver, string instance) {
        var problem = new DOMINATINGSET(instance);
        var rec = new StepRecorder<NodeSet>(cap: int.MaxValue);
        string answer = Make(solver).Solve(problem, rec);
        Assert.Equal(Make(solver).solve(problem), answer);
        bool forced = solver == nameof(DominatingSetForcedVertex);
        var (parentOf, labelOf) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Forced Vertex labels a node with the vertex picked, Naive with the decision on a vertex.
        Assert.All(labelOf.Where(kv => kv.Key != 0), kv => Assert.Matches(forced ? @"^\S+( \(forced\))?$" : @"^\S+ (in|out)$", kv.Value));
        // Each step is about its last node: an Accept's or a Try's is explored (or the solution), a Reject's is pruned and
        // noted, a Backtrack's fails, or stays pruned with the note it was cut with. Forced Vertex's forced pick past K is an
        // Accept whose node is pruned.
        var cutWith = new Dictionary<int, string?>();
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event == StepEvent.Try) Assert.Equal(SearchNodeState.Explored, node.State);
            if (step.Event == StepEvent.Accept && node.State != SearchNodeState.Pruned)
                Assert.Contains(node.State, new[] { SearchNodeState.Explored, SearchNodeState.Solution });
            if (step.Event is StepEvent.Reject || node.State == SearchNodeState.Pruned && step.Event != StepEvent.Backtrack) {
                Assert.Equal(SearchNodeState.Pruned, node.State);
                Assert.Matches(@"^(over K = \d+|\d+ vertex not dominated|\d+ vertices not dominated)$", node.Bound);
                cutWith[node.NodeId] = node.Bound;
            }
            if (step.Event == StepEvent.Backtrack) {
                if (cutWith.TryGetValue(node.NodeId, out var note)) Assert.Equal((SearchNodeState.Pruned, note), (node.State, node.Bound));
                else Assert.Equal(SearchNodeState.Failed, node.State);
            }
        }
        var done = rec.Steps[^1];
        if (problem.nodes.Count == 0) {
            // A graph with no vertices is never searched.
            Assert.Null(done.Tree);
            return;
        }
        bool found = answer != "{}" && Verifies(problem, answer);
        Assert.Equal(found, done.Ok);
        if (!found) {
            var root = done.Tree![^1];
            Assert.Equal(0, root.NodeId);
            Assert.Equal(cutWith.ContainsKey(0) ? SearchNodeState.Pruned : SearchNodeState.Failed, root.State);
        } else {
            var path = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
            var picked = answer.Trim('{', '}').Split(',').ToHashSet();
            if (forced) {
                // The picks, in the order they were made, are the answer's vertices.
                Assert.Equal(picked.Order(), path.Select(l => l.Replace(" (forced)", "")).Order());
            } else {
                // A decision on every vertex, in order: in exactly for the answer's vertices.
                Assert.Equal(problem.nodes.Select(v => $"{v} {(picked.Contains(v) ? "in" : "out")}"), path);
            }
        }
        // With every step kept, Done included: every node off the answer's path ends failed or pruned.
        var state = new Dictionary<int, SearchNodeState>();
        foreach (var node in rec.Steps.SelectMany(s => s.Tree ?? [])) state[node.NodeId] = node.State;
        var onPath = found ? done.Tree!.SkipWhile(n => n.ParentId != null).Select(n => n.NodeId).ToHashSet() : [];
        Assert.All(state.Where(kv => !onPath.Contains(kv.Key)), kv => Assert.True(kv.Value is SearchNodeState.Failed or SearchNodeState.Pruned, $"node {kv.Key} ends {kv.Value}"));
    }

    [Fact]
    public void ForcedVertex_AForcedPickHasOneChild_AndFailsWhenNothingFollowsFromIt() {
        var rec = new StepRecorder<NodeSet>();
        new DominatingSetForcedVertex().Solve(new DOMINATINGSET(DeadEnd), rec);
        Assert.Equal(["0<-:start:Explored 1<0:5 (forced):Explored", "1<0:5 (forced):Explored",
            "2<1:2:Explored", "2<1:2:Pruned(2 vertices not dominated)", "2<1:2:Pruned(2 vertices not dominated)",
            "3<1:1:Explored", "3<1:1:Pruned(2 vertices not dominated)", "3<1:1:Pruned(2 vertices not dominated)",
            "1<0:5 (forced):Failed 0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void Naive_AnIncludeOverK_IsPrunedAndStaysPruned_AndAFinishedSetThatFailsIsPruned() {
        var rec = new StepRecorder<NodeSet>();
        new DominatingSetNaiveInclusionExclusion().Solve(new DOMINATINGSET(NaiveDeadEnd), rec);
        Assert.Equal(["0<-:start:Explored 1<0:1 in:Explored", "1<0:1 in:Pruned(over K = 0)", "1<0:1 in:Pruned(over K = 0)",
            "2<0:1 out:Explored", "3<2:2 in:Explored", "3<2:2 in:Pruned(over K = 0)", "3<2:2 in:Pruned(over K = 0)",
            "4<2:2 out:Explored", "5<4:3 in:Explored", "5<4:3 in:Pruned(over K = 0)", "5<4:3 in:Pruned(over K = 0)",
            "6<4:3 out:Explored", "6<4:3 out:Pruned(3 vertices not dominated)",
            "4<2:2 out:Failed 2<0:1 out:Failed 0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void ForcedVertex_ASetFoundPastK_IsCutOff_AndTheDoneFailsItsPicksAndTheRoot() {
        var rec = new StepRecorder<NodeSet>();
        Assert.Equal("{}", new DominatingSetForcedVertex().Solve(new DOMINATINGSET(PastK), rec));
        Assert.Equal(["0<-:start:Explored 1<0:2 (forced):Explored", "1<0:2 (forced):Explored", "2<1:3:Pruned(over K = 1)",
            "2<1:3:Pruned(over K = 1) 1<0:2 (forced):Failed 0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
        Assert.Equal("The set found did not verify.", rec.Steps[^1].Caption);
    }

    // The steps up to the cap carry their nodes, and the Done still names the answer's path.
    [Theory]
    [InlineData(nameof(DominatingSetForcedVertex))]
    [InlineData(nameof(DominatingSetNaiveInclusionExclusion))]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath(string solver) {
        var problem = new DOMINATINGSET(new DOMINATINGSET().defaultInstance);
        var rec = new StepRecorder<NodeSet>(cap: 3);
        string answer = Make(solver).Solve(problem, rec);
        Assert.True(Verifies(problem, answer));
        Assert.All(rec.Steps.Take(3), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output, for both solvers.
public class DominatingSetSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public DominatingSetSearchTree_Endpoint_Tests(AppFactory factory) {
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
        foreach (var solver in new[] { "", "&solver=DominatingSetNaiveInclusionExclusion" }) {
            data.Add(solver, new DOMINATINGSET().defaultInstance, true);
            data.Add(solver, DominatingSetSearchTree_Tests.DeadEnd, false);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenFound(string solver, string instance, bool found) {
        string body = await Visualize($"visualization=DominatingSetSearchTreeVisualization{solver}&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Dominating Set", instance);
        Assert.Equal(found, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (found) Assert.NotEmpty(green);
        else Assert.Empty(green);
        // A dead end is drawn dashed.
        if (!found) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
