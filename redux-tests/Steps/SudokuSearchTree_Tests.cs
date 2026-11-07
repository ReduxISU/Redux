using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUDOKU;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Sudoku Backtracking's search tree: it links up, a square with no digit left is a pruned node, every digit the search
// gives up is marked failed (the search never says so, so the next step kept carries it), and the Done ends on the
// answer's path, whose digits are the answer's.
public class SudokuSearchTree_Tests {

    // The snapshot's Sudoku instances, and the dead ends BoardSolvers_Tests names.
    public static TheoryData<string> Instances() {
        var data = new TheoryData<string>();
        foreach (var instance in BoardSolvers_Tests.Rows().Where(r => r.Solver == "sudokusolver").Select(r => r.Instance).Distinct()) data.Add(instance);
        data.Add(BoardSolvers_Tests.DeadEndSudoku);
        data.Add("1,2,3,4,5,6,7,8,0;0,0,0,0,0,0,0,0,9;" + string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 7)));
        return data;
    }

    // At most this many steps are kept, so the hardest puzzles stay cheap; a run that fits is checked to the end.
    private const int Cap = 3000;

    [Theory]
    [MemberData(nameof(Instances))]
    public void SearchTree_IsWellFormed_EveryDigitGivenUpFails_AndTheDoneEndsOnTheAnswersPath(string instance) {
        var sudoku = new SUDOKU(instance);
        var rec = new StepRecorder<BoardCells>(cap: Cap);
        string answer = new SudokuSolver().Solve(sudoku, rec);
        Assert.Equal(new SudokuSolver().solve(sudoku), answer);
        var (parentOf, _) = SearchTreeSteps.CheckLinks(rec.Steps);
        foreach (var step in rec.Steps.Where(s => s.Event != StepEvent.Done)) {
            var node = step.Tree![^1];
            if (step.Event == StepEvent.Try) Assert.EndsWith("=" + step.Partial.Values.First(v => v.Key == step.Focus[0]).Value, node.Label);
            if (step.Event == StepEvent.Reject) Assert.Equal((SearchNodeState.Pruned, node.NodeId == 0 ? "clues clash" : "no digit left"), (node.State, node.Bound));
        }
        var done = rec.Steps[^1];
        bool solved = answer != "{}";
        Assert.Equal(solved, done.Ok);
        var path = solved ? SearchTreeSteps.AnswerPath(rec.Steps, parentOf) : [];
        if (solved) {
            // The path names every square the clues left empty, with the answer's digit.
            var rows = answer.Split(';').Select(r => r.Split(',')).ToArray();
            var expected = new List<string>();
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (sudoku.grid[r][c] == 0) expected.Add($"R{r + 1}C{c + 1}={rows[r][c]}");
            Assert.Equal(expected, path);
        }
        if (rec.Hidden > 0) return;
        // With every step kept: apply every patch, Done included; then every digit off the answer's path has failed, and
        // with no answer, the root too.
        var state = new Dictionary<int, SearchNodeState>();
        var label = new Dictionary<int, string>();
        foreach (var step in rec.Steps)
            foreach (var node in step.Tree ?? []) {
                state[node.NodeId] = node.State;
                label[node.NodeId] = node.Label;
            }
        var onPath = solved ? done.Tree!.SkipWhile(n => n.ParentId != null).Select(n => n.NodeId).ToHashSet() : [];
        foreach (var (id, s) in state) {
            if (onPath.Contains(id)) continue;
            Assert.True(s is SearchNodeState.Failed or SearchNodeState.Pruned, $"{label[id]} ends {s}");
        }
    }

    [Fact]
    public void ADeadEnd_IsAPrunedSquareUnderTheDigitBefore_WhichFailsInTheDone() {
        var rec = new StepRecorder<BoardCells>();
        new SudokuSolver().Solve(new SUDOKU(BoardSolvers_Tests.DeadEndSudoku), rec);
        string Text(IReadOnlyList<SearchNode>? tree) => string.Join(" ", (tree ?? []).Select(n => $"{n.NodeId}<{n.ParentId?.ToString() ?? "-"}:{n.Label}:{n.State}{(n.Bound is null ? "" : $"({n.Bound})")}"));
        Assert.Equal(["0<-:start:Explored 1<0:R1C8=9:Explored", "2<1:R1C9:Pruned(no digit left)", "1<0:R1C8=9:Failed 0<-:start:Failed"],
            rec.Steps.Select(s => Text(s.Tree)));
    }

    [Fact]
    public void ClashingClues_PruneTheRoot() {
        var rec = new StepRecorder<BoardCells>();
        new SudokuSolver().Solve(new SUDOKU("5,5,0,0,0,0,0,0,0;" + string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 8))), rec);
        var root = Assert.Single(rec.Steps[0].Tree!);
        Assert.Equal((0, "start", SearchNodeState.Pruned, "clues clash"), (root.NodeId, root.Label, root.State, root.Bound));
        Assert.Null(rec.Steps[^1].Tree);
    }
}

// The Search Tree picture on the real /ProblemProvider/visualize?format=frames output.
public class SudokuSearchTree_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SudokuSearchTree_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<string> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return body;
    }

    public static TheoryData<string, bool> Grids() => new() {
        { new SUDOKU().defaultInstance, true },
        { BoardSolvers_Tests.HardSudoku, true },
        { BoardSolvers_Tests.DeadEndSudoku, false },
    };

    [Theory]
    [MemberData(nameof(Grids))]
    public async Task SearchTreePicture_IsWellFormed_AndGreenOnlyWhenSolved(string instance, bool solved) {
        string body = await Visualize("visualization=SudokuSearchTreeVisualization&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Sudoku", instance);
        Assert.Equal(solved, frames[^1].GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        if (solved) Assert.NotEmpty(green);
        else Assert.Empty(green);
    }
}
