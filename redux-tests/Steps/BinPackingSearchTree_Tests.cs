using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_BINPACKING;
using API.Problems.NPComplete.NPC_BINPACKING.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search tree of Bin Packing Brute Force: it links up, an item with no bin that has room is pruned, an item bigger than
// a bin cuts the root off, every placement taken back fails, and the Done ends on the answer's path, whose placements are a
// packing, or fails the root.
public class BinPackingSearchTree_Tests {

    // Small random instances (seeded), and a few by hand.
    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        var random = new Random(4473);
        for (int t = 0; t < 80; t++) {
            int n = random.Next(0, 7), c = random.Next(5, 13), k = random.Next(0, 5);
            var sizes = Enumerable.Range(0, n).Select(_ => random.Next(1, 10));
            data.Add($"(({string.Join(",", sizes)}),{c},{k})");
        }
        data.Add(new BINPACKING().defaultInstance);
        data.Add(TooBig);
        data.Add(NoRoom);
        return data;
    }

    // 9 does not fit in a bin of 8.
    public const string TooBig = "((3,9),8,2)";
    // Two bins of 10 hold two 6s, but not three.
    public const string NoRoom = "((6,6,6),10,2)";

    private static bool Verifies(BINPACKING problem, string answer) {
        try { return problem.defaultVerifier.verify(problem, answer); } catch { return false; }
    }

    private static string Text(IReadOnlyList<SearchNode>? tree) =>
        string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_AndEndsOnTheAnswersPath(string instance) {
        var problem = new BINPACKING(instance);
        var rec = new StepRecorder<Placement>(cap: int.MaxValue);
        string answer = new BinPackingBruteForce().Solve(problem, rec);
        Assert.Equal(new BinPackingBruteForce().solve(problem), answer);
        // With no bins allowed (and every item fitting a bin) the search never starts, and no step names a node.
        if (problem.K <= 0 && problem.S.All(x => x <= problem.C)) {
            Assert.All(rec.Steps, s => Assert.Null(s.Tree));
            return;
        }
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        // Each step is about its last node: an Accept's is the item put in the bin in focus, a Backtrack's the same placement
        // taken back (failed), a Reject's an item with no bin that has room or bigger than a bin (pruned and noted), or the
        // placement before an item every bin failed for (failed).
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event == StepEvent.Accept) {
                int item = int.Parse(step.Focus[0][1..]) - 1, bin = int.Parse(step.Focus[1][1..]);
                Assert.Equal($"{problem.S[item]} in bin {bin}", node.Label);
                Assert.Contains(node.State, new[] { SearchNodeState.Explored, SearchNodeState.Solution });
            }
            if (step.Event == StepEvent.Backtrack) Assert.Equal(SearchNodeState.Failed, node.State);
            if (step.Event == StepEvent.Reject && node.State == SearchNodeState.Pruned)
                Assert.Matches(node.NodeId == 0 ? $@"^\d+ > C = {problem.C}$" : "^no bin has room$", node.Bound);
            else if (step.Event == StepEvent.Reject)
                Assert.Equal(SearchNodeState.Failed, node.State);
        }
        var done = rec.Steps[^1];
        bool packed = answer != "{}";
        Assert.Equal(packed, done.Ok);
        if (!packed) {
            var root = done.Tree![^1];
            Assert.Equal(0, root.NodeId);
            Assert.Equal(problem.S.Any(x => x > problem.C) ? SearchNodeState.Pruned : SearchNodeState.Failed, root.State);
        } else {
            // The path puts every item, in order, in a bin; grouped by bin, its sizes are a packing.
            var path = SearchTreeSteps.AnswerPath(rec.Steps, parentOf);
            Assert.Equal(problem.S.Count, path.Count);
            var placed = path.Select(label => label.Split(" in bin ")).ToList();
            Assert.Equal(problem.S.Select(x => x.ToString()), placed.Select(p => p[0]));
            var bins = placed.GroupBy(p => p[1]).Select(g => "(" + string.Join(",", g.Select(p => p[0])) + ")");
            if (problem.S.Count > 0) Assert.True(Verifies(problem, "(" + string.Join(",", bins) + ")"));
        }
        // With every step kept, Done included: every node off the answer's path ends failed or pruned.
        var state = new Dictionary<int, SearchNodeState>();
        foreach (var node in rec.Steps.SelectMany(s => s.Tree ?? [])) state[node.NodeId] = node.State;
        var onPath = packed ? done.Tree!.SkipWhile(n => n.ParentId != null).Select(n => n.NodeId).ToHashSet() : [];
        Assert.All(state.Where(kv => !onPath.Contains(kv.Key)), kv => Assert.True(kv.Value is SearchNodeState.Failed or SearchNodeState.Pruned, $"node {kv.Key} ends {kv.Value}"));
    }

    [Fact]
    public void AnItemBiggerThanABin_CutsTheRootOff() {
        var rec = new StepRecorder<Placement>();
        new BinPackingBruteForce().Solve(new BINPACKING(TooBig), rec);
        Assert.Equal(["0<-:start:Pruned(9 > C = 8)", "0<-:start:Pruned(9 > C = 8)"], rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void AnItemWithNoRoom_IsPruned_AndThePlacementsAboveItFail() {
        var rec = new StepRecorder<Placement>();
        new BinPackingBruteForce().Solve(new BINPACKING(NoRoom), rec);
        Assert.Equal(["0<-:start:Explored 1<0:6 in bin 1:Explored", "2<1:6 in bin 2:Explored", "3<2:6:Pruned(no bin has room)", "2<1:6 in bin 2:Failed",
            "1<0:6 in bin 1:Failed", "1<0:6 in bin 1:Failed", "0<-:start:Failed", "0<-:start:Failed"], rec.Steps.Select(s => Text(s.Tree)));
    }

    // The steps up to the cap carry their nodes, and the Done still names the path.
    [Fact]
    public void CappedRun_KeepsTheCap_AndTheDoneStillNamesThePath() {
        var problem = new BINPACKING(new BINPACKING().defaultInstance);
        var rec = new StepRecorder<Placement>(cap: 3);
        string answer = new BinPackingBruteForce().Solve(problem, rec);
        Assert.True(Verifies(problem, answer));
        Assert.All(rec.Steps.Take(3), s => Assert.NotNull(s.Tree));
        Assert.Equal(SearchNodeState.Solution, rec.Steps[^1].Tree![^1].State);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class BinPackingSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public BinPackingSearchTree_Endpoint_Tests(AppFactory factory) {
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
    [InlineData("((4,7,3,6,2,8),10,3)", true)]
    [InlineData(BinPackingSearchTree_Tests.TooBig, false)]
    [InlineData(BinPackingSearchTree_Tests.NoRoom, false)]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenPacked(string instance, bool packed) {
        string body = await Visualize("visualization=BinPackingSearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Bin Packing", instance);
        Assert.Equal(packed, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (packed) Assert.NotEmpty(green);
        else Assert.Empty(green);
        if (!packed) Assert.Contains(frames[^1].GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.Kind(n) is "pruned" or "cut");
    }
}
