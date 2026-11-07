using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search tree of Set Cover Algorithm X Bounded Search: it links up, a set that takes the cover over K is pruned (and
// stays pruned when it is taken back), an element in no set is a pruned node, every other set taken back fails, and the
// Done ends on the answer's path, whose sets are a cover within K, or fails the root.
public class SetCoverSearchTree_Tests {

    // Small random instances (seeded), with K from 0 to 3, and a few by hand.
    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        var random = new Random(4473);
        for (int t = 0; t < 80; t++) {
            int n = random.Next(1, 7), count = random.Next(0, 7), k = random.Next(0, 4);
            var sets = new List<string>();
            for (int s = 0; s < count; s++) {
                var set = Enumerable.Range(1, n).Where(_ => random.NextDouble() < 0.4).ToList();
                if (set.Count == 0) set.Add(random.Next(1, n + 1));
                sets.Add("{" + string.Join(",", set) + "}");
            }
            data.Add($"({{{string.Join(",", Enumerable.Range(1, n))}}},{{{string.Join(",", sets)}}},{k})");
        }
        data.Add(new SETCOVER().defaultInstance);
        data.Add(OverK);
        data.Add(InNoSet);
        return data;
    }

    // Every element is in one set of its own, so one set is not enough.
    public const string OverK = "({1,2},{{1},{2}},1)";
    // No set holds 3.
    public const string InNoSet = "({1,2,3},{{1,2}},2)";

    private static bool Verifies(SETCOVER problem, string answer) {
        try { return problem.defaultVerifier.verify(problem, answer); } catch { return false; }
    }

    private static string Text(IReadOnlyList<SearchNode>? tree) =>
        string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string instance) {
        var problem = new SETCOVER(instance);
        var rec = new StepRecorder<ChosenSets>(cap: int.MaxValue);
        string answer = new HeuristicSolver().Solve(problem, rec);
        Assert.Equal(new HeuristicSolver().solve(problem), answer);
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Each step is about its last node: an Accept's is the set it takes, a Reject's a set over K or an element in no set
        // (pruned and noted), a Backtrack's the set it takes back (failed, or still pruned, with the note it was cut with).
        var cutWith = new Dictionary<int, string?>();
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event == StepEvent.Accept) {
                Assert.Equal(step.Focus[0], node.Label);
                Assert.Contains(node.State, new[] { SearchNodeState.Explored, SearchNodeState.Solution });
            }
            if (step.Event == StepEvent.Reject) {
                Assert.Equal(SearchNodeState.Pruned, node.State);
                Assert.Contains(node.Bound, new[] { $"over K = {problem.K}", "in no set" });
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
            // The path's sets, by id, are a cover within K, as many as the answer's ("{{1,2},{3}}" names 2).
            var path = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
            var sets = path.Select(id => problem.subsets[int.Parse(id[1..]) - 1]).ToList();
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
    public void ASetOverK_IsPruned_AndStaysPrunedWhenTakenBack() {
        var rec = new StepRecorder<ChosenSets>();
        new HeuristicSolver().Solve(new SETCOVER(OverK), rec);
        Assert.Equal(["0<-:start:Explored 1<0:S1:Explored", "2<1:S2:Explored", "2<1:S2:Pruned(over K = 1)", "2<1:S2:Pruned(over K = 1)",
            "1<0:S1:Failed", "0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void AnElementInNoSet_IsAPrunedNodeUnderTheRoot() {
        var rec = new StepRecorder<ChosenSets>();
        new HeuristicSolver().Solve(new SETCOVER(InNoSet), rec);
        Assert.Equal(["0<-:start:Explored 1<0:3:Pruned(in no set)", "0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
    }

    // The steps up to the cap carry their nodes, and the Done still names the answer's path.
    [Fact]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath() {
        var problem = new SETCOVER("({1,2,3,4,5,6},{{1,2},{3,4},{5,6},{1,3,5},{2,4},{6}},3)");
        var rec = new StepRecorder<ChosenSets>(cap: 3);
        string answer = new HeuristicSolver().Solve(problem, rec);
        Assert.True(Verifies(problem, answer));
        Assert.All(rec.Steps.Take(3), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class SetCoverSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SetCoverSearchTree_Endpoint_Tests(AppFactory factory) {
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
    [InlineData("({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)", true)]
    [InlineData(SetCoverSearchTree_Tests.OverK, false)]
    [InlineData(SetCoverSearchTree_Tests.InNoSet, false)]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenCovered(string instance, bool covered) {
        string body = await Visualize("visualization=SetCoverSearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Set Cover", instance);
        Assert.Equal(covered, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (covered) Assert.NotEmpty(green);
        else Assert.Empty(green);
        if (!covered) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
