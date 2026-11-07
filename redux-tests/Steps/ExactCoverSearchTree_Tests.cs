using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search trees of Knuth's Algorithm X and Exact Cover Recursive: they link up, a dead end is pruned (Algorithm X: the
// element no set left can cover; Exact Cover Recursive: the set taken last, which stays pruned when it is taken back), every
// set taken back otherwise fails, and the Done ends on the answer's path, whose sets are an exact cover, or fails the root.
public class ExactCoverSearchTree_Tests {
    private static ISolver<EXACTCOVER, ChosenSets> Make(string solver) => solver == nameof(DancingLinks) ? new DancingLinks() : new ExactCoverRecursive();

    // Small random instances (seeded), and a few by hand, for both solvers.
    public static TheoryData<string, string> Instances() {
        var instances = new List<string>();
        var random = new Random(5573);
        for (int t = 0; t < 60; t++) {
            int n = random.Next(1, 7), count = random.Next(0, 7);
            var sets = new List<string>();
            for (int s = 0; s < count; s++) {
                var set = Enumerable.Range(1, n).Where(_ => random.NextDouble() < 0.4).ToList();
                if (set.Count == 0) set.Add(random.Next(1, n + 1));
                sets.Add("{" + string.Join(",", set) + "}");
            }
            instances.Add($"({{{string.Join(",", Enumerable.Range(1, n))}}},{{{string.Join(",", sets)}}})");
        }
        instances.Add(new EXACTCOVER().defaultInstance);
        instances.Add(DeadEnd);
        instances.Add("({1,2,3},{{1,2,3}})");
        var data = new TheoryData<string, string>();
        foreach (var solver in new[] { nameof(DancingLinks), nameof(ExactCoverRecursive) })
            foreach (var instance in instances) data.Add(solver, instance);
        return data;
    }

    // Taking {1,2} leaves 3, which {2,3} cannot cover without doubling up 2; nor can anything follow {2,3}.
    public const string DeadEnd = "({1,2,3},{{1,2},{2,3}})";

    private static bool Verifies(EXACTCOVER problem, string answer) {
        try { return problem.defaultVerifier.verify(problem, answer); } catch { return false; }
    }

    private static string Text(IReadOnlyList<SearchNode>? tree) =>
        string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string solver, string instance) {
        var problem = new EXACTCOVER(instance);
        var rec = new StepRecorder<ChosenSets>(cap: int.MaxValue);
        string answer = Make(solver).Solve(problem, rec);
        Assert.Equal(Make(solver).solve(problem), answer);
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Each step is about its last node: an Accept's is the set it takes, a Reject's the dead end (pruned and noted), a
        // Backtrack's the set it takes back (failed, or still pruned, with the note it was cut with).
        var cutWith = new Dictionary<int, string?>();
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event == StepEvent.Accept) {
                Assert.Equal(step.Focus[0], node.Label);
                Assert.Contains(node.State, new[] { SearchNodeState.Explored, SearchNodeState.Solution });
            }
            if (step.Event == StepEvent.Reject) {
                Assert.Equal(SearchNodeState.Pruned, node.State);
                if (solver == nameof(DancingLinks)) Assert.Equal("no set left", node.Bound);
                else Assert.EndsWith(" uncovered", node.Bound);
                cutWith[node.NodeId] = node.Bound;
            }
            if (step.Event == StepEvent.Backtrack) {
                Assert.Equal(step.Focus[0], node.Label);
                if (cutWith.TryGetValue(node.NodeId, out var note)) Assert.Equal((SearchNodeState.Pruned, note), (node.State, node.Bound));
                else Assert.Equal(SearchNodeState.Failed, node.State);
            }
        }
        var done = rec.Steps[^1];
        bool covered = Verifies(problem, answer);
        Assert.Equal(covered, done.Ok);
        if (!covered) {
            var root = done.Tree![^1];
            Assert.Equal(0, root.NodeId);
            Assert.Equal(cutWith.ContainsKey(0) ? SearchNodeState.Pruned : SearchNodeState.Failed, root.State);
        } else {
            // The path's sets, by id, are an exact cover, as many as the answer's ("{{1,2},{3}}" names 2).
            var path = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
            var sets = path.Select(id => problem.S[int.Parse(id[1..]) - 1]).ToList();
            Assert.True(Verifies(problem, "{" + string.Join(",", sets.Select(s => "{" + string.Join(",", s) + "}")) + "}"));
            Assert.Equal(answer.Count(c => c == '{') - 1, sets.Count);
        }
        // With every step kept, Done included: every node off the answer's path ends failed or pruned.
        var state = new Dictionary<int, SearchNodeState>();
        foreach (var node in rec.Steps.SelectMany(s => s.Tree ?? [])) state[node.NodeId] = node.State;
        var onPath = covered ? done.Tree!.SkipWhile(n => n.ParentId != null).Select(n => n.NodeId).ToHashSet() : [];
        Assert.All(state.Where(kv => !onPath.Contains(kv.Key)), kv => Assert.True(kv.Value is SearchNodeState.Failed or SearchNodeState.Pruned, $"node {kv.Key} ends {kv.Value}"));
    }

    [Fact]
    public void AlgorithmX_ADeadEnd_IsAPrunedElementUnderTheSetBefore() {
        var rec = new StepRecorder<ChosenSets>();
        new DancingLinks().Solve(new EXACTCOVER(DeadEnd), rec);
        Assert.Equal(["0<-:start:Explored 1<0:S1:Explored", "2<1:3:Pruned(no set left)", "1<0:S1:Failed", "0<-:start:Failed"],
            rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void Recursive_ADeadEnd_PrunesTheSetTakenLast_WhichStaysPruned() {
        var rec = new StepRecorder<ChosenSets>();
        new ExactCoverRecursive().Solve(new EXACTCOVER(DeadEnd), rec);
        Assert.Equal(["0<-:start:Explored 1<0:S1:Explored", "1<0:S1:Pruned(1 element uncovered)", "1<0:S1:Pruned(1 element uncovered)",
            "2<0:S2:Explored", "2<0:S2:Pruned(1 element uncovered)", "2<0:S2:Pruned(1 element uncovered)", "0<-:start:Failed"],
            rec.Steps.Select(s => Text(s.Tree)));
    }

    // The steps up to the cap carry their nodes, and the Done still names the answer's path.
    [Theory]
    [InlineData(nameof(DancingLinks))]
    [InlineData(nameof(ExactCoverRecursive))]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath(string solver) {
        var problem = new EXACTCOVER("({1,2,3,4,5,6},{{1,2},{3,4},{1,3},{2,4},{5},{6},{5,6},{1,6}})");
        var rec = new StepRecorder<ChosenSets>(cap: 3);
        string answer = Make(solver).Solve(problem, rec);
        Assert.True(Verifies(problem, answer));
        Assert.All(rec.Steps.Take(3), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output, for both solvers.
public class ExactCoverSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ExactCoverSearchTree_Endpoint_Tests(AppFactory factory) {
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
        foreach (var solver in new[] { "", "&solver=ExactCoverRecursive" }) {
            data.Add(solver, new EXACTCOVER().defaultInstance, true);
            data.Add(solver, ExactCoverSearchTree_Tests.DeadEnd, false);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenCovered(string solver, string instance, bool covered) {
        string body = await Visualize($"visualization=ExactCoverSearchTreeVisualization{solver}&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Exact Cover", instance);
        Assert.Equal(covered, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (covered) Assert.NotEmpty(green);
        else Assert.Empty(green);
        // A dead end is drawn dashed.
        if (!covered) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
