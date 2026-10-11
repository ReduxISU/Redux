using System.Diagnostics;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Geometry;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Convex Hull solver records steps for the Geometry picture. Recording must never change an answer.
// Endpoints/Snapshots/geometry_solver_answers.tsv holds what it returned (solver, instance and answer as JSON strings; tab
// separated) BEFORE the picture existed (with the #708/#709 and #806 fixes in), on its default instance, hand-picked edge
// cases and seeded random instances.
public class GeometrySolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("geometry_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
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

    internal const string Solver = "convexhullsolver";
    internal const string Visualization = "convexhullvisualization";
    internal const string Default = "{(-0.49429993857731547,-0.14539088193288174),(0.7674622377407927,-0.21537444528240846),(-0.9308276577586132,-0.1423800479224624),(-0.6984449872706371,0.3857380723376367),(-0.27394905790800017,-0.7488048223660126),(0.6681904385614421,-0.11341930745315643),(0.6077591838324792,0.5288040272918157),(0.32371432910023556,0.12231914413229394),(-0.32705115386597394,0.6744065707101621),(0.2723211656942368,-0.8053758131859647)}";
    internal const string SquareInside = "{(0,0),(4,0),(4,4),(0,4),(1,1),(2,3),(3,2),(2,2),(1,3)}";
    internal const string OnALine = "{(0,0),(1,0),(2,0),(3,0),(1.5,2)}";

    private static SolveRun Run(string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[Solver])!).Run(instance, withSteps: true);

    private static List<SolverStep<HullBuild>> Steps(string instance) => Run(instance).Steps.Cast<SolverStep<HullBuild>>().ToList();

    private static List<GeometryFrame> Frames(string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[Visualization])!;
        var response = vis.BuildFrames(instance, Run(instance));
        Assert.Equal("Geometry", response.Type);
        return response.Frames.Cast<GeometryFrame>().ToList();
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
        Assert.False(on.TimedOut);
        Assert.Equal(IncrementalSolvers_Tests.Classification[solverName], on.Incremental);

        var problem = new CONVEXHULL(instance);
        var spec = ConvexHullVisualization.Spec(problem);
        var ids = GeometryFrames.IdsByPoint(spec).Values.ToHashSet();
        var steps = on.Steps.Cast<SolverStep<HullBuild>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        // The Done step's verdict is the verifier's (every hull is right since #708), and its partial is the answer.
        Assert.True(problem.defaultVerifier.verify(problem, answer));
        Assert.True(steps[^1].Ok);
        Assert.Equal(ConvexHullVisualization.ParseHull(problem, answer), steps[^1].Partial);

        foreach (var s in steps) {
            var p = s.Partial;
            Assert.All(p.Pieces.SelectMany(x => x).Concat(p.Left).Concat(p.Right).Concat(p.Chain).Concat(p.Dropped).Concat(s.Focus), id => Assert.Contains(id, ids));
            // A merge has both sides or neither, and a chain only while merging.
            Assert.Equal(p.Left.Length == 0, p.Right.Length == 0);
            if (p.Left.Length == 0) Assert.Empty(p.Chain);
            // A point is never both a corner on the table and dropped.
            Assert.Empty(p.Pieces.SelectMany(x => x).Concat(p.Left).Concat(p.Right).Intersect(p.Dropped));
            // Pushes are Tries focused on the chain's last corner; pops are Rejects focused on a point no longer at its end.
            if (s.Event == StepEvent.Reject) Assert.DoesNotContain(Assert.Single(s.Focus), p.Chain.TakeLast(1));
        }
        // Every point ends up a corner of the hull or dropped, each once.
        var last = steps[^1].Partial;
        Assert.Equal(ids.Order(), last.Pieces.SelectMany(x => x).Concat(last.Dropped).Order());
        Assert.Equal(steps.Count(s => s.Event == StepEvent.Accept), last.Pieces.SingleOrDefault()?.Length >= 3 ? 1 : 0);

        var frames = Frames(instance);
        Assert.Equal(steps.Count, frames.Count);
        Assert.True(frames[^1].Ok);
    }

    [Fact]
    public void Default_SortsSplitsAndMerges_LikeTheMockup() {
        var frames = Frames(Default);
        Assert.Equal(61, frames.Count);
        Assert.Equal("Sort the 10 points by x, then y: P3, P4, P1, P9, P5, P10, P8, P7, P6 and P2. Then split the list in half, again and again, until each piece has at most 3 points.",
            frames[0].Caption);
        Assert.Equal("Smallest piece: P3 and P4. Its hull is the segment between them.", frames[1].Caption);
        Assert.Equal("Smallest piece: P1, P9 and P5. Its hull is the triangle they make.", frames[2].Caption);
        Assert.Equal("Merge the left hull (P3 and P4) with the right hull (P1, P5 and P9). Walk their 5 corners left to right for the bottom edge, then back for the top edge, keeping only left turns.",
            frames[3].Caption);
        Assert.Equal(["P3,P4 left", "P1,P5,P9 right"], frames[3].Pieces.Select(p => $"{string.Join(",", p.Points)} {p.Role}"));
        // The issue's pop: a clockwise turn takes the corner off the chain.
        var pop = frames[6];
        Assert.Equal((StepEvent.Reject, "The turn at P4 (from P3 to P1) is clockwise, so pop P4 from the bottom edge."), (pop.Event, pop.Caption));
        Assert.Equal(["P3"], pop.Chain);
        Assert.Equal(PictureState.Rejected, pop.Points["P4"].State);
        Assert.Equal("Back at P3: the top edge closes the hull.", frames[16].Caption);
        Assert.Equal("The merged hull is P3, P5, P9 and P4. P1 is now inside it (or on an edge), so it is dropped.", frames[17].Caption);
        Assert.Equal(PictureState.Rejected, frames[17].Points["P1"].State);
        Assert.Equal((StepEvent.Accept, "The hull is P3, P5, P10, P2, P7, P9 and P4, counterclockwise from P3."), (frames[^2].Event, frames[^2].Caption));
        var done = frames[^1];
        Assert.Equal("Done. The hull has 7 corners, listed counterclockwise from P3: P3, P5, P10, P2, P7, P9 and P4. The other 3 points lie inside it or on an edge.", done.Caption);
        // Redux's certificate example, corner for corner: the same 7 corners, counterclockwise (the example starts at P10).
        Assert.Equal(["P3", "P5", "P10", "P2", "P7", "P9", "P4"], done.Hull);
        Assert.Equal("P1:Covered P10:Solution P2:Solution P3:Solution P4:Solution P5:Solution P6:Covered P7:Solution P8:Covered P9:Solution",
            string.Join(" ", done.Points.Select(p => $"{p.Key}:{p.Value.State}")));
    }

    [Fact]
    public void CollinearPoints_AreDroppedFromTheMiddle_AndAStraightTurnIsPopped() {
        var frames = Frames(OnALine);
        Assert.Equal("Smallest piece: P1 and P2. Its hull is the segment between them.", frames[1].Caption);
        Assert.Equal("Smallest piece: P5, P3 and P4. Its hull is the triangle they make.", frames[2].Caption);
        Assert.Contains(frames, f => f.Event == StepEvent.Reject && f.Caption == "P1, P2 and P3 are on one line, so pop P2 from the bottom edge.");
        Assert.Equal(["P1", "P4", "P5"], frames[^1].Hull);
        var line = Frames("{(0,0),(1,1),(2,2)}");
        Assert.Equal("Smallest piece: P1, P2 and P3. They are on one line, so P2 in the middle is dropped.", line[1].Caption);
        Assert.Equal("All the points are on one line, so the hull is the segment from P1 to P3.", line[^1].Caption);
    }

    [Theory]
    [InlineData("{}", "There are no points, so the hull is empty.", 1)]
    [InlineData("{(1,1)}", "The only point is P1, so the hull is that single point.", 2)]
    [InlineData("{(1,1),(2,3)}", "All the points are on one line, so the hull is the segment from P1 to P2.", 3)]
    public void TinyInstances_SayWhatTheHullIs(string instance, string caption, int count) {
        var frames = Frames(instance);
        Assert.Equal(count, frames.Count);
        Assert.Equal(caption, frames[^1].Caption);
        Assert.True(frames[^1].Ok);
    }

    [Fact]
    public void ALongRun_ShowsTheFirst150Steps_ThenTheHull() {
        var (_, instance, answer) = Rows().Last();
        var frames = Frames(instance);
        Assert.Equal(StepRecorder<HullBuild>.DefaultCap + 2, frames.Count);
        Assert.Equal(StepEvent.Accept, frames[^2].Event);
        Assert.Matches(@"^Done\. The hull has \d+ corners, .* \(\d+ not shown\)$", frames[^1].Caption);
        Assert.Equal(answer.Count(c => c == '('), frames[^1].Hull.Count + 1);
    }

    [Fact]
    public void ManyPoints_StayWellWithinTheTimeBudget() {
        var rnd = new Random(674);
        string instance = "{" + string.Join(",", Enumerable.Range(0, 2000).Select(_ => $"({rnd.Next(-100000, 100000)},{rnd.Next(-100000, 100000)})").Distinct()) + "}";
        var sw = Stopwatch.StartNew();
        var frames = Frames(instance);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        Assert.True(frames[^1].Ok);
    }
}
