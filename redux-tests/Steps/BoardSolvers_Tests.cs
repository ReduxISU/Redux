using System.Diagnostics;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Sudoku and N-Queens solvers record steps for the Board picture. Recording must never change an answer.
// Endpoints/Snapshots/board_solver_answers.tsv holds what each returned (solver, instance and answer as JSON strings; tab
// separated) BEFORE the picture existed (with the #704/#705, #798 and #799 fixes in), on its default instance, hand-picked
// edge cases and seeded random instances.
public class BoardSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("board_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], JsonSerializer.Deserialize<string>(parts[1])!, JsonSerializer.Deserialize<string>(parts[2])!);
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
        ["sudokusolver"] = "sudokuvisualization",
        ["nqueensbacktracking"] = "nqueensvisualization",
        ["nqueensconstructive"] = "nqueensvisualization",
    };

    internal static string SudokuDefault => new API.Problems.NPComplete.NPC_SUDOKU.SUDOKU().defaultInstance;

    // Arto Inkala's puzzle: the hardest one the snapshot holds (about 50,000 tries for this solver).
    internal const string HardSudoku =
        "8,0,0,0,0,0,0,0,0;0,0,3,6,0,0,0,0,0;0,7,0,0,9,0,2,0,0;0,5,0,0,0,7,0,0,0;0,0,0,0,4,5,7,0,0;0,0,0,1,0,0,0,3,0;0,0,1,0,0,0,0,6,8;0,0,8,5,0,0,0,1,0;0,9,0,0,0,0,4,0,0";

    // No clue clashes, but row 1 needs 8 and 9 in R1C8 and R1C9, and both columns already hold an 8: the solver puts 9 in
    // R1C8, finds nothing left for R1C9, and has nothing else to try.
    internal const string DeadEndSudoku =
        "1,2,3,4,5,6,7,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,8,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,8;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0";

    private static string Empty9 => string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 9));

    private static SolveRun Run(string solver, string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).Run(instance, withSteps: true);

    private static List<SolverStep<BoardCells>> Steps(string solver, string instance) => Run(solver, instance).Steps.Cast<SolverStep<BoardCells>>().ToList();

    private static (BoardPayload Payload, List<BoardFrame> Frames) Picture(string solver, string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solver]])!;
        var response = vis.BuildFrames(instance, Run(solver, instance));
        Assert.Equal("Board", response.Type);
        return ((BoardPayload)response.Payload, response.Frames.Cast<BoardFrame>().ToList());
    }

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

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
        Assert.False(on.TimedOut);
        Assert.Equal(IncrementalSolvers_Tests.Classification[solverName], on.Incremental);

        var steps = on.Steps.Cast<SolverStep<BoardCells>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        // At most the cap plus Done: these solvers never Accept, so nothing is kept past the cap.
        Assert.True(steps.Count <= StepRecorder<BoardCells>.DefaultCap + 1);
        Assert.DoesNotContain(steps, s => s.Event == StepEvent.Accept);

        var (payload, frames) = Picture(solverName, instance);
        Assert.Equal(steps.Count, frames.Count);
        var squares = Enumerable.Range(0, payload.Size).SelectMany(i => Enumerable.Range(0, payload.Size).Select(j => BoardFrames.CellId(i, j))).ToHashSet();
        foreach (var step in steps) {
            // Every id a step names is a square of the board.
            Assert.All(step.Focus, id => Assert.Contains(id, squares));
            Assert.All(step.Partial.Values.Keys, id => Assert.Contains(id, squares));
            Assert.All(step.Partial.Conflicts, c => { Assert.Contains(c.Cell, squares); Assert.Contains(c.With, squares); });
        }

        // The Done step's verdict is the verifier's, and its board is the answer.
        var problemType = ProblemProvider.Solvers[solverName].GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];
        var problem = (IProblem)Activator.CreateInstance(problemType, [instance])!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, steps[^1].Ok);
        Assert.Equal(verified, frames[^1].Ok);
        if (verified) {
            // Every square the answer fills that is not a clue is Solution; clues stay Background.
            var clues = payload.Clues.Select(c => c.Id).ToHashSet();
            Assert.All(frames[^1].Cells, c => Assert.Equal(PictureState.Solution, c.Value.State));
            Assert.Equal(steps[^1].Partial.Values.Keys.Where(id => !clues.Contains(id)).Order(StringComparer.Ordinal), frames[^1].Cells.Keys);
        } else {
            Assert.Empty(steps[^1].Partial.Values);
            Assert.Empty(frames[^1].Cells);
        }
    }

    // ---- Sudoku ----

    [Fact]
    public void Sudoku_Default_TriesDigitsSquareBySquare_AsTheMockupDoes() {
        var steps = Steps("sudokusolver", SudokuDefault);
        // 429 tries: the first 150 steps are kept, then the answer.
        Assert.Equal(151, steps.Count);
        Assert.Equal("R1C1 can still hold 6, 8 or 9. Try 6.", steps[0].Caption);
        Assert.Equal(["r0c0"], steps[0].Focus);
        Assert.Equal("6", steps[0].Partial.Values["r0c0"]);
        // The clues are on the board in every step.
        Assert.Equal("1", steps[0].Partial.Values["r0c3"]);
        Assert.Equal(31, steps[0].Partial.Values.Count);
        // The mockup's frame 6: R1C6 can only hold 8.
        Assert.Equal("R1C6 can only hold 8. Try it.", steps[4].Caption);
        Assert.Equal(["r0c5"], steps[4].Focus);

        // The first dead end: R2C6 has nothing left, and the next try changes the square before it.
        var dead = steps.First(s => s.Event == StepEvent.Reject);
        Assert.Equal("R2C6 has no digit left: every digit from 1 to 9 is already in its row, column or box. Undo the last choice.", dead.Caption);
        Assert.Equal(["r1c5"], dead.Focus);
        Assert.False(dead.Partial.Values.ContainsKey("r1c5"));
        var next = steps[steps.IndexOf(dead) + 1];
        Assert.Equal("3 in R2C4 led to a dead end. Try 9 instead.", next.Caption);
        Assert.Equal("9", next.Partial.Values["r1c3"]);

        var done = steps[^1];
        Assert.Equal("Every square is filled and no row, column or box repeats a digit. Solved after 429 tries. (396 not shown)", done.Caption);
        Assert.Equal(81, done.Partial.Values.Count);
        Assert.Empty(steps.SelectMany(s => s.Partial.Conflicts));
    }

    [Fact]
    public void Sudoku_Frames_KeepPlacedDigitsPlainUntilTheAnswer_HighlightOnlyTheTriedSquare_AndNeverTheClues() {
        var (payload, frames) = Picture("sudokusolver", SudokuDefault);
        Assert.Equal(("sudoku", 9, 3, 1, 30), (payload.Game, payload.Size, payload.Box, payload.LabelsFrom, payload.Clues.Count));
        Assert.Equal("r0c3 0 3 1", $"{payload.Clues[0].Id} {payload.Clues[0].R} {payload.Clues[0].C} {payload.Clues[0].Value}");
        var clues = payload.Clues.Select(c => c.Id).ToHashSet();

        // Step 5 (index 4): four digits placed in row 1 (plain: not the answer yet, #839) and R1C6 being tried.
        Assert.Equal("r0c5:ElementHighlight", States(frames[4].Cells));
        Assert.Equal(["4", "5", "6", "7"], new[] { "r0c0", "r0c1", "r0c2", "r0c4" }.Select(id => frames[4].Values[id]).Order());
        Assert.Equal("8", frames[4].Values["r0c5"]);
        Assert.Equal("1", frames[4].Values["r0c3"]);
        Assert.Empty(frames[4].Pieces);

        var dead = frames.First(f => f.Event == StepEvent.Reject);
        Assert.Equal(PictureState.Rejected, dead.Cells["r1c5"].State);
        Assert.False(dead.Values.ContainsKey("r1c5"));
        foreach (var f in frames) {
            Assert.DoesNotContain(f.Cells.Keys, clues.Contains);
            Assert.Empty(f.Links);
            Assert.True(f.Cells.Count(c => c.Value.State != PictureState.Solution) <= 1);
            // Solution colour means the answer: only the last frame, the confirmed Done, has any (#839).
            if (f != frames[^1]) Assert.DoesNotContain(f.Cells.Values, c => c.State == PictureState.Solution);
        }
        Assert.Equal((StepEvent.Done, true), (frames[^1].Event, frames[^1].Ok));
        // The capped run still ends on the finished board: every square filled, the 51 the solver filled Solution.
        Assert.Equal(81, frames[^1].Values.Count);
        Assert.Equal(51, frames[^1].Cells.Count);
    }

    [Fact]
    public void Sudoku_ClashingClues_AreShownLinked_AndNothingIsSearched() {
        string instance = "1,0,0,1,0,0,0,0,0;" + string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 8));
        var steps = Steps("sudokusolver", instance);
        Assert.Equal([StepEvent.Reject, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal("The clues already clash: R1C1 and R1C4 both hold 1 in the same row. No grid can be completed, so there is nothing to search.", steps[0].Caption);
        Assert.Equal(["r0c0", "r0c3"], steps[0].Focus);
        Assert.Equal([new BoardConflict("r0c3", "r0c0")], steps[0].Partial.Conflicts);
        Assert.Equal((false, "No solution: the clues break the rules before a single square is filled."), (steps[1].Ok!.Value, steps[1].Caption));

        var (_, frames) = Picture("sudokusolver", instance);
        Assert.Equal("r0c0:Rejected r0c3:Rejected", States(frames[0].Cells));
        var link = Assert.Single(frames[0].Links);
        Assert.Equal("r0c3>r0c0 Rejected", $"{link.From}>{link.To} {link.State}");
        // The no-answer Done shows the clues only.
        Assert.Equal(["r0c0", "r0c3"], frames[1].Values.Keys);
        Assert.Empty(frames[1].Cells);
    }

    [Fact]
    public void Sudoku_ClashesInABox_AreAllLinked() {
        string instance = "5,0,0,0,0,0,0,0,0;0,5,0,0,0,0,0,5,0;" + string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 7));
        var reject = Steps("sudokusolver", instance)[0];
        Assert.StartsWith("The clues already clash: R1C1 and R2C2 both hold 5 in the same box.", reject.Caption);
        Assert.Equal([new BoardConflict("r1c1", "r0c0"), new BoardConflict("r1c7", "r1c1")], reject.Partial.Conflicts);
        Assert.Equal(["r0c0", "r1c1", "r1c7"], reject.Focus);
    }

    [Fact]
    public void Sudoku_ADeadEndFoundBySearch_UndoesThenGivesUp() {
        var steps = Steps("sudokusolver", DeadEndSudoku);
        Assert.Equal([StepEvent.Try, StepEvent.Reject, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal("R1C8 can only hold 9. Try it.", steps[0].Caption);
        Assert.Equal("R1C9 has no digit left: every digit from 1 to 9 is already in its row, column or box. Undo the last choice.", steps[1].Caption);
        Assert.Equal("9", steps[1].Partial.Values["r0c7"]);
        Assert.Equal("Every choice ran into a dead end after 1 try. This grid has no solution.", steps[2].Caption);
        Assert.False(steps[2].Ok);
        Assert.Equal("{}", Run("sudokusolver", DeadEndSudoku).Answer);
    }

    [Fact]
    public void Sudoku_FirstEmptySquareWithNoDigit_HasNothingToUndo() {
        string instance = "1,2,3,4,5,6,7,8,0;0,0,0,0,0,0,0,0,9;" + string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 7));
        var steps = Steps("sudokusolver", instance);
        Assert.Equal("R1C9 has no digit left: every digit from 1 to 9 is already in its row, column or box. So the grid has no solution.", steps[0].Caption);
        Assert.Equal("This grid has no solution.", steps[1].Caption);
    }

    [Fact]
    public void Sudoku_AFullGrid_HasNothingToTry() {
        string full = "6,4,8,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5";
        var done = Assert.Single(Steps("sudokusolver", full));
        Assert.Equal((true, "Every square was already filled and no row, column or box repeats a digit, so there was nothing to try."), (done.Ok!.Value, done.Caption));
        var (_, frames) = Picture("sudokusolver", full);
        // Every square is a clue, so nothing is drawn as the solver's.
        Assert.Empty(Assert.Single(frames).Cells);
    }

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class SudokuTiming {
        [Theory]
        [InlineData("default")]
        [InlineData("hard")]
        [InlineData("empty")]
        public void Sudoku_LargeSearches_FinishWellWithinTheDefaultBudget(string which) {
            string instance = which switch { "default" => SudokuDefault, "hard" => HardSudoku, _ => Empty9 };
            var budget = TimeSpan.FromSeconds(ProblemProvider.DefaultSolveTimeLimitSeconds);
            var clock = Stopwatch.StartNew();
            var run = ((ISolver)new API.Problems.NPComplete.NPC_SUDOKU.Solvers.SudokuSolver()).Run(instance, withSteps: true, budget);
            Assert.False(run.TimedOut);
            Assert.True(clock.Elapsed < budget / 4, $"took {clock.Elapsed}");
            Assert.Equal(StepRecorder<BoardCells>.DefaultCap + 1, run.Steps.Count);
        }
    }

    // ---- N-Queens ----

    [Fact]
    public void Backtracking_Four_PlacesTakesBackAndMoves_AsTheMockupDoes() {
        var steps = Steps("nqueensbacktracking", "4");
        Assert.Equal([
            "Row 0: column 0 is safe. Place the queen there.",
            "Row 1: columns 0 and 1 are attacked (0 along its column by the queen at (0,0)). Place the queen at column 2.",
            "Row 2: every column is attacked, so back up and move the queen in row 1.",
            "Back in row 1: move the queen on to column 3.",
            "Row 2: column 0 is attacked (0 along its column by the queen at (0,0)). Place the queen at column 1.",
            "Row 3: every column is attacked, so back up and move the queen in row 2.",
            "Row 2: the rest of the row is attacked too, so back up and move the queen in row 1.",
            "Row 1: its queen was already in the last column, so back up and move the queen in row 0.",
            "Back in row 0: move the queen on to column 1.",
            "Row 1: columns 0, 1 and 2 are attacked (0 diagonally by the queen at (0,1)). Place the queen at column 3.",
            "Row 2: column 0 is safe. Place the queen there.",
            "Row 3: columns 0 and 1 are attacked (0 along its column by the queen at (2,0)). Place the queen at column 2.",
            "All 4 queens are placed and none attacks another: {(0,1),(1,3),(2,0),(3,2)}. Found after 8 placements.",
        ], steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Try, StepEvent.Try, StepEvent.Reject, StepEvent.Try, StepEvent.Try, StepEvent.Reject, StepEvent.Reject, StepEvent.Reject,
            StepEvent.Try, StepEvent.Try, StepEvent.Try, StepEvent.Try, StepEvent.Done], steps.Select(s => s.Event));

        // A placement: the queens so far, the new one as the focus, and each skipped square linked to its attacker.
        Assert.Equal(["r0c0", "r1c2"], steps[1].Partial.Values.Keys);
        Assert.Equal(["r1c2"], steps[1].Focus);
        Assert.Equal([new BoardConflict("r1c0", "r0c0"), new BoardConflict("r1c1", "r0c0")], steps[1].Partial.Conflicts);
        // A dead row: the whole row and the queen above that must move are what failed.
        Assert.Equal(["r2c0", "r2c1", "r2c2", "r2c3", "r1c2"], steps[2].Focus);
        Assert.Equal([new BoardConflict("r2c0", "r0c0"), new BoardConflict("r2c1", "r1c2"), new BoardConflict("r2c2", "r0c0"), new BoardConflict("r2c3", "r1c2")],
            steps[2].Partial.Conflicts);
        Assert.Equal(["r0c0", "r1c2"], steps[2].Partial.Values.Keys);
    }

    [Fact]
    public void Backtracking_Four_Frames_KeepQueensPlainUntilTheAnswer_AndMarkTheFailedRowRejected() {
        var (payload, frames) = Picture("nqueensbacktracking", "4");
        Assert.Equal(("queens", 4, (int?)null, 0), (payload.Game, payload.Size, payload.Box, payload.LabelsFrom));
        Assert.Empty(payload.Clues);
        Assert.Equal("r1c0:Rejected r1c1:Rejected r1c2:ElementHighlight", States(frames[1].Cells));
        Assert.Equal(["r0c0 queen", "r1c2 queen"], frames[1].Pieces.Select(p => $"{p.Id} {p.Kind}"));
        Assert.Equal(["r1c0>r0c0", "r1c1>r0c0"], frames[1].Links.Select(l => $"{l.From}>{l.To}"));
        Assert.Empty(frames[1].Values);
        // The dead row 2: all of it Rejected, and the queen in row 1 that has to move.
        Assert.Equal("r1c2:Rejected r2c0:Rejected r2c1:Rejected r2c2:Rejected r2c3:Rejected", States(frames[2].Cells));
        // Queens stay plain while the solver searches (#839); only the confirmed answer is Solution.
        Assert.All(frames.SkipLast(1), f => Assert.DoesNotContain(f.Cells.Values, c => c.State == PictureState.Solution));
        var done = frames[^1];
        Assert.Equal("r0c1:Solution r1c3:Solution r2c0:Solution r3c2:Solution", States(done.Cells));
        Assert.Empty(done.Links);
    }

    [Fact]
    public void Backtracking_Three_RunsOutOfRows_AndAnswersNothing() {
        var steps = Steps("nqueensbacktracking", "3");
        Assert.Equal("Row 0 has no column left to try.", steps[^2].Caption);
        Assert.Equal(["r0c0", "r0c1", "r0c2"], steps[^2].Focus);
        Assert.Equal("Every arrangement runs into an attack. No placement exists for n = 3, so the solver returns {}.", steps[^1].Caption);
        Assert.False(steps[^1].Ok);
        Assert.Empty(steps[^1].Partial.Values);
    }

    [Theory]
    [InlineData("nqueensbacktracking", "1", "The one queen is placed: {(0,0)}. Found after 1 placement.")]
    [InlineData("nqueensconstructive", "1", "The one queen is placed in 1 step, with no search: {(0,0)}.")]
    [InlineData("nqueensbacktracking", "0", "An empty board needs no queens, so the answer is {}.")]
    [InlineData("nqueensconstructive", "0", "An empty board needs no queens, so the answer is {}.")]
    [InlineData("nqueensconstructive", "2", "The formula has no answer for n = 2, and none exists, so the solver returns {} without placing anything.")]
    public void SmallBoards_SayWhatHappened(string solver, string n, string caption) {
        var done = Steps(solver, n)[^1];
        Assert.Equal(caption, done.Caption);
        Assert.Equal(n is "0" or "1", done.Ok);
    }

    [Fact]
    public void Constructive_Eight_WritesTheListThenPlacesARowAtATime() {
        var steps = Steps("nqueensconstructive", "8");
        Assert.Equal(10, steps.Count);
        Assert.Equal("n = 8, so n mod 12 = 8. Write the even columns, then the odd ones, and swap the odds in pairs: 2, 4, 6, 8, 3, 1, 7, 5. Row i gets the i-th number, minus 1.",
            steps[0].Caption);
        Assert.Empty(steps[0].Focus);
        Assert.Empty(steps[0].Partial.Values);
        Assert.Equal("Row 0: column 1, straight from the list. No checking needed.", steps[1].Caption);
        Assert.Equal(["r0c1"], steps[1].Focus);
        Assert.Equal(["r0c1", "r1c3", "r2c5", "r3c7", "r4c2", "r5c0", "r6c6", "r7c4"], steps[8].Partial.Values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("All 8 queens placed in 8 steps, with no search: {(0,1),(1,3),(2,5),(3,7),(4,2),(5,0),(6,6),(7,4)}.", steps[^1].Caption);
        var (_, frames) = Picture("nqueensconstructive", "8");
        Assert.Equal("r2c5:ElementHighlight", States(frames[3].Cells));
        Assert.Equal(["r0c1", "r1c3", "r2c5"], frames[3].Pieces.Select(p => p.Id));
    }

    [Theory]
    [InlineData("9", "move 2 to the end of the evens; move 1 and 3 to the end of the odds: 4, 6, 8, 2, 5, 7, 9, 1, 3")]
    [InlineData("14", "swap 1 and 3, then move 5 to the end of the odds: 2, 4, 6, 8, 10, 12, 14, 3, 1, 7, 9, 11, 13, 5")]
    public void Constructive_NamesTheAdjustmentsItMakes(string n, string expected) =>
        Assert.Contains(expected, Steps("nqueensconstructive", n)[0].Caption);

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class QueensTiming {
        [Fact]
        public void Constructive_ALargeBoard_KeepsCaptionsShort_AndEndsWithEveryQueen() {
            var clock = Stopwatch.StartNew();
            var run = Run("nqueensconstructive", "100000");
            var steps = run.Steps.Cast<SolverStep<BoardCells>>().ToList();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");
            Assert.Equal(StepRecorder<BoardCells>.DefaultCap + 1, steps.Count);
            Assert.EndsWith(", … (100,000 in all). Row i gets the i-th number, minus 1.", steps[0].Caption);
            Assert.Equal("All 100,000 queens placed in 100,000 steps, with no search. (99851 not shown)", steps[^1].Caption);
            Assert.Equal(100000, steps[^1].Partial.Values.Count);
            Assert.True(steps[^1].Ok);
        }
    }
}
