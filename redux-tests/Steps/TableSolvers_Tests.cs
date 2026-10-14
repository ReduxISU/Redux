using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Edit Distance and 0-1 ILP solvers record steps for the Table picture. Recording must never change an answer.
// Endpoints/Snapshots/table_solver_answers.tsv holds what each returned (solver, instance, answer as a JSON string; tab
// separated) BEFORE the picture existed (with the #797 fix in), on its default instance, the mockup's examples,
// hand-picked edge cases and seeded random instances.
public class TableSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("table_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], parts[1], JsonSerializer.Deserialize<string>(parts[2])!);
        });

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    // The visualization that draws each solver's steps.
    internal static readonly Dictionary<string, string> VisualizationOf = new() {
        ["editdistancedpsolver"] = "editdistancevisualization",
        ["integerprogrammingbruteforce"] = "intprogrammingvisualization",
    };

    private static SolveRun Run(string solver, string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).Run(instance, withSteps: true);

    private static List<SolverStep<T>> Steps<T>(string solver, string instance) => Run(solver, instance).Steps.Cast<SolverStep<T>>().ToList();

    private static (TablePayload Payload, List<TableFrame> Frames) Picture(string solver, string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solver]])!;
        var response = vis.BuildFrames(instance, Run(solver, instance));
        return ((TablePayload)response.Payload, response.Frames.Cast<TableFrame>().ToList());
    }

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsFitThePicture(string solverName, string instance, string answer) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(IncrementalSolvers_Tests.Classification[solverName], on.Incremental);

        var events = on.Steps.Select(s => (StepEvent)((dynamic)s).Event).ToList();
        Assert.Equal(StepEvent.Done, events[^1]);
        Assert.Single(events, e => e == StepEvent.Done);
        Assert.All(on.Steps, s => Assert.False(string.IsNullOrWhiteSpace((string)((dynamic)s).Caption)));
        // A whole-candidate solver accepts at most once: the candidate that works.
        if (!on.Incremental) Assert.True(events.Count(e => e == StepEvent.Accept) <= 1);

        var (payload, frames) = Picture(solverName, instance);
        Assert.Equal(on.Steps.Count, frames.Count);
        var cellIds = Enumerable.Range(0, payload.Rows.Count)
            .SelectMany(i => Enumerable.Range(0, payload.Columns.Count).Select(j => TableFrames.CellId(i, j))).ToHashSet();
        var rowIds = payload.Rows.Select(r => r.Id).ToHashSet();
        foreach (var (step, frame) in on.Steps.Cast<dynamic>().Zip(frames)) {
            // Every id a step names is an id of the picture.
            foreach (string id in (string[])step.Focus) Assert.True(cellIds.Contains(id) || rowIds.Contains(id), id);
            Assert.All(frame.Values, row => Assert.Equal(payload.Columns.Count, row.Count));
        }

        // The Done step's verdict is the verifier's.
        var problemType = ProblemProvider.Solvers[solverName].GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];
        var problem = (IProblem)Activator.CreateInstance(problemType, [instance])!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, (bool?)((dynamic)on.Steps[^1]).Ok);
        Assert.Equal(verified, frames[^1].Ok);
    }

    // ---- Edit Distance ----

    [Fact]
    public void EditDistance_FillsCellByCell_ThenTracesBack_AsTheMockupDoes() {
        var steps = Steps<TableCells>("editdistancedpsolver", "(horse, ros)");
        // Row 0 and column 0, 15 cells, the corner and 5 moves back, the answer.
        Assert.Equal(23, steps.Count);
        Assert.Equal([StepEvent.Try, .. Enumerable.Repeat(StepEvent.Try, 15), .. Enumerable.Repeat(StepEvent.Accept, 6), StepEvent.Done],
            steps.Select(s => s.Event));
        Assert.Equal("Fill row 0 and column 0 first: turning a prefix into the empty string costs one deletion or insertion per letter.", steps[0].Caption);
        Assert.Equal(9, steps[0].Partial.Values.Count);
        Assert.Empty(steps[0].Focus);

        // Cell (2,2): 'o' = 'o', so the diagonal is copied (the mockup's frame 6).
        var copy = steps[5];
        Assert.Equal(["r2c2"], copy.Focus);
        Assert.Equal("Cell (2,2): 'o' = 'o', so copy the diagonal: 1.", copy.Caption);
        Assert.Equal(["r1c1"], copy.Partial.Reads);
        Assert.Equal("1", copy.Partial.Values["r2c2"]);
        Assert.False(copy.Partial.Values.ContainsKey("r2c3"));

        // Cell (1,1): 'h' ≠ 'r', three cells read, the diagonal (replace) taken first.
        Assert.Equal("Cell (1,1): 'h' ≠ 'r'. Replace 'h' with 'r': 1 + 0. Delete 'h': 1 + 1. Insert 'r': 1 + 1. The cheapest is replace, so 1.", steps[1].Caption);
        Assert.Equal(["r0c0", "r0c1", "r1c0"], steps[1].Partial.Reads);

        Assert.Equal("Trace back from the corner (5,3), where the edit distance is 3.", steps[16].Caption);
        Assert.Equal("(5,3) came from (4,3): delete 'e'.", steps[17].Caption);
        var done = steps[^1];
        Assert.Equal("Edit distance 3: one cheapest way is 1 replacement, 2 deletions.", done.Caption);
        Assert.Equal(["r5c3", "r4c3", "r3c2", "r2c2", "r1c1", "r0c0"], done.Partial.Path);
        Assert.Equal(24, done.Partial.Values.Count);
        Assert.Equal("3", done.Partial.Values["r5c3"]);
    }

    [Theory]
    [InlineData("(kitten, sitting)")]
    [InlineData("(intention, execution)")]
    [InlineData("(abc, )")]
    [InlineData("(, abc)")]
    [InlineData("(, )")]
    [InlineData("(abcdefghijklmnop, ponmlkjihgfedcba)")]
    public void EditDistance_TheTraceBack_IsACheapestPath_FromTheCornerToTheStart(string instance) {
        var done = Steps<TableCells>("editdistancedpsolver", instance)[^1];
        var path = done.Partial.Path.Select(id => TableFrames.ParseCellId(id)!.Value).ToList();
        var problem = new API.Problems.NPComplete.NPC_EDITDISTANCE.EDITDISTANCE(instance);
        string x = problem.sourceString, y = problem.targetString;
        Assert.Equal((x.Length, y.Length), path[0]);
        Assert.Equal((0, 0), path[^1]);
        int cost = 0;
        for (int k = 1; k < path.Count; k++) {
            var (i, j) = path[k - 1];
            var (pi, pj) = path[k];
            Assert.True((i - pi, j - pj) is (1, 1) or (1, 0) or (0, 1));
            if (!(i - pi == 1 && j - pj == 1 && x[pi] == y[pj])) cost++;
        }
        Assert.Equal(int.Parse(done.Partial.Values[done.Partial.Path[0]]), cost);
    }

    [Fact]
    public void EditDistance_ABigTable_IsFilledOneRowPerStep_SoTheWholeFillShows() {
        Assert.True(EditDistanceDPSolver.FillsCellByCell(5, 3));
        Assert.True(EditDistanceDPSolver.FillsCellByCell(11, 11));
        Assert.False(EditDistanceDPSolver.FillsCellByCell(16, 16));
        var steps = Steps<TableCells>("editdistancedpsolver", "(abcdefghijklmnop, ponmlkjihgfedcba)");
        var rows = steps.Skip(1).TakeWhile(s => s.Event == StepEvent.Try).ToList();
        Assert.Equal(16, rows.Count);
        Assert.Equal(Enumerable.Range(1, 16).Select(j => $"r3c{j}"), rows[2].Focus);
        Assert.Empty(rows[2].Partial.Reads);
        Assert.Equal(17 + 16 * 3 + 16, rows[2].Partial.Values.Count);
        Assert.StartsWith("Row 3 ('c'): each cell copies the diagonal when the letters match", rows[2].Caption);
        // Nothing is hidden: every row, the trace-back and the answer fit in the cap.
        Assert.DoesNotContain("not shown", steps[^1].Caption);
    }

    [Fact]
    public void EditDistance_TheFrames_ArrowTheReadsAndKeepTheTraceBackCommittedUntilTheAnswer() {
        var (payload, frames) = Picture("editdistancedpsolver", "(horse, ros)");
        Assert.Equal(["ε", "r", "o", "s"], payload.Columns.Select(c => c.Label));
        Assert.Equal(["ε", "h", "o", "r", "s", "e"], payload.Rows.Select(r => r.Label));
        var fill = frames[1];
        Assert.Equal(["r1c1"], fill.Changed);
        Assert.Equal(PictureState.ElementHighlight, fill.Cells["r1c1"].State);
        Assert.Equal(["r0c0>r1c1 True", "r0c1>r1c1 False", "r1c0>r1c1 False"], fill.Arrows!.Select(a => $"{a.From}>{a.To} {a.Best}"));
        // While filling, nothing is Solution: the answer is the trace-back.
        Assert.All(frames.Take(16), f => Assert.DoesNotContain(f.Cells.Values, s => s.State == PictureState.Solution));
        var back = frames[18];
        Assert.Equal(PictureState.ElementHighlight, back.Cells["r3c2"].State);
        // The trace-back so far is kept, not the answer yet (#886).
        Assert.Equal(["r4c3", "r5c3"], back.Cells.Where(c => c.Value.State == PictureState.Committed).Select(c => c.Key));
        var done = frames[^1];
        Assert.Equal(["r0c0", "r1c1", "r2c2", "r3c2", "r4c3", "r5c3"], done.Cells.Keys);
        Assert.All(done.Cells.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Empty(done.Arrows!);
        Assert.True(done.Ok);
    }

    // ---- 0-1 ILP ----

    [Fact]
    public void IntProgramming_TriesAssignmentsInTheCountersOrder_AndAcceptsTheFirstThatHolds() {
        var steps = Steps<Assignment>("integerprogrammingbruteforce", "(1 -1)<=(-1)");
        Assert.Equal(
            ["Try (0 0): row 1 adds up to 0, over its bound -1.",
             "Try (1 0): row 1 adds up to 1, over its bound -1.",
             "Try (0 1): every row stays within its bound.",
             "(0 1) satisfies every row, found on try 3 of 4."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Reject, StepEvent.Reject, StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal(["r1"], steps[0].Focus);
        Assert.Empty(steps[2].Focus);
        Assert.Equal("x1=0 x2=1", string.Join(" ", steps[^1].Partial.Values.Select(p => $"{p.Key}={p.Value}")));
    }

    [Fact]
    public void IntProgramming_NoSolution_EndsWithTheEmptyAssignment_NotOk() {
        var steps = Steps<Assignment>("integerprogrammingbruteforce", "(-1 -1 0),(1 1 0),(0 0 1)<=(-2 1 1)");
        Assert.Equal(9, steps.Count);
        Assert.Equal("Try (1 1 0): row 2 adds up to 2, over its bound 1.", steps[3].Caption);
        var done = steps[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.Equal("All 8 assignments break some row, so no 0-1 solution exists.", done.Caption);
        Assert.Empty(done.Partial.Values);
    }

    [Fact]
    public void IntProgramming_TheFrames_HighlightATriedAssignment_AndMarkEachRow() {
        var (payload, frames) = Picture("integerprogrammingbruteforce", "(-1 -1 0),(0 -1 -1),(1 1 1)<=(-1 -1 2)");
        Assert.Equal(["x1", "x2", "x3", "total", "bound", "holds"], payload.Columns.Select(c => c.Key));
        Assert.Equal(["x =", "row 1", "row 2", "row 3"], payload.Rows.Select(r => r.Label));
        var tried = frames[1];
        Assert.Equal(["1", "0", "0", null, null, null], tried.Values[0]);
        Assert.Equal(["0", "-1", "-1", "0", "-1", "no"], tried.Values[2]);
        Assert.Equal("r1:Covered r2:Rejected r3:Covered", string.Join(" ", tried.Rows.Select(r => $"{r.Key}:{r.Value.State}")));
        Assert.Equal(["r0c0", "r0c1", "r0c2"], tried.Cells.Keys);
        Assert.All(tried.Cells.Values, s => Assert.Equal(PictureState.ElementHighlight, s.State));
        var done = frames[^1];
        Assert.True(done.Ok);
        Assert.All(done.Rows.Values.Concat(done.Cells.Values), s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Equal(3, done.Rows.Count);

        // No solution: the Done frame is the blank matrix.
        var (_, none) = Picture("integerprogrammingbruteforce", "(1)<=(-1)");
        Assert.Equal(["?", null, null, null], none[^1].Values[0]);
        Assert.Equal(["1", null, "-1", null], none[^1].Values[1]);
        Assert.Empty(none[^1].Rows);
        Assert.Empty(none[^1].Cells);
        Assert.False(none[^1].Ok);
    }
}
