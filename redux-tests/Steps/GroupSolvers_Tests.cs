using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Graph Coloring (brute force, greedy) and Clique Cover (brute force) solvers record Groups steps. Recording must never
// change an answer. Endpoints/Snapshots/groups_solver_answers.tsv holds what each solver returned (solver, instance,
// answer; tab separated) BEFORE it recorded steps, on its default instance and on 12 seeded random graphs (including
// ones with no answer).
public class GroupSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(SnapshotPath("groups_solver_answers.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static Type ProblemTypeOf(Type solver) =>
        solver.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];

    [Fact]
    public void TheSnapshotCoversEveryGroupsSolver_WithAnswersAndWithout() {
        var rows = RecordedAnswers.Select(d => d.Data.Item1).GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(["cliquecoverbruteforce", "graphcoloringbruteforce", "graphcoloringgreedy"], rows.Keys.Order());
        Assert.All(rows.Values, n => Assert.Equal(13, n));
        Assert.Contains(RecordedAnswers, d => d.Data.Item3 == "{}");
        Assert.Contains(RecordedAnswers, d => d.Data.Item3 != "{}");
    }

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeRecordingSteps(string solverName, string instance, string answer) {
        var type = ProblemProvider.Solvers[solverName];
        var solver = (ISolver)Activator.CreateInstance(type)!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);

        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(typeof(Groups), on.StepShape);

        // The last step is the only Done, every caption is a sentence, and Done.Ok is what the problem's verifier says.
        var steps = on.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        var problem = (IProblem)Activator.CreateInstance(ProblemTypeOf(type))!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, steps[^1].Ok);
        Assert.Equal(answer != "{}", steps[^1].Ok);   // these solvers return {} exactly when they found no answer
    }

    // The last step's partial answer says the same thing as the answer string: the n-th class is group n.
    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void TheDoneStep_ShowsTheAnswerTheSolverGave(string solverName, string instance, string answer) {
        var run = ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!).Run(instance, withSteps: true);
        Assert.Equal(GraphGroupFrames.ParseClasses(answer), ((SolverStep<Groups>)run.Steps[^1]).Partial);
    }

    // Every partial answer names only nodes of the graph, and a node is in at most one group.
    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void EveryStep_NamesOnlyNodesOfTheGraph(string solverName, string instance, string answer) {
        _ = answer;
        var type = ProblemProvider.Solvers[solverName];
        var run = ((ISolver)Activator.CreateInstance(type)!).Run(instance, withSteps: true);
        dynamic problem = Activator.CreateInstance(ProblemTypeOf(type), instance)!;
        HashSet<string> nodes = ((IEnumerable<string>)problem.nodes).ToHashSet();
        Assert.All(run.Steps.Cast<SolverStep<Groups>>(), s => Assert.All(s.Partial.Of.Keys, n => Assert.Contains(n, nodes)));
    }

    [Fact]
    public void Greedy_ColorsOneNodePerStep_InNodeOrder_NamingTheNeighboursItAvoids() {
        // The default instance: a is joined to everyone, so a gets color 0, then b (joined to a) gets 1, c (joined to a and b) gets 2.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringGreedy()).Run(new GRAPHCOLORING().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.Equal(9 + 1, steps.Count);
        Assert.All(steps.Take(9), s => Assert.Equal(StepEvent.Accept, s.Event));
        Assert.Equal("Color node a with color 0: no neighbour is colored yet.", steps[0].Caption);
        Assert.Equal(["a"], steps[0].Focus);
        Assert.Equal(new Groups([new("a", 0)]), steps[0].Partial);
        Assert.Equal("Color node b with color 1: neighbour a already uses color 0.", steps[1].Caption);
        Assert.Equal("Color node c with color 2: neighbours a and b already use colors 0 and 1.", steps[2].Caption);
        Assert.Equal(["c"], steps[2].Focus);
        Assert.Equal(new Groups([new("a", 0), new("b", 1), new("c", 2)]), steps[2].Partial);
    }

    [Fact]
    public void Greedy_ANodeThatNeedsMoreColorsThanKIsARejectStep_AndTheAnswerIsNone() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringGreedy()).Run("(({1,2,3},{{1,2},{1,3},{2,3}}),2)", true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.Equal(["Accept", "Accept", "Reject", "Done"], steps.Select(s => s.Event.ToString()));
        Assert.Equal("Node 3 needs color 2, but K = 2 allows only colors 0 to 1: neighbours 1 and 2 already use colors 0 and 1.", steps[2].Caption);
        Assert.Equal(new Groups([new("1", 0), new("2", 1), new("3", 2)]), steps[2].Partial);   // the choice is shown, then thrown away
        Assert.Equal("{}", run.Answer);
        Assert.False(steps[3].Ok);
        Assert.Empty(steps[3].Partial.Of);
    }

    [Fact]
    public void ColoringBruteForce_RejectsNameTheClashingNeighbours_AndStartsFromTheFirstCandidate() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringBruteForce()).Run(new GRAPHCOLORING().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        var rejects = steps.Where(s => s.Event == StepEvent.Reject).ToList();
        Assert.NotEmpty(rejects);
        // The first candidate puts every node in color 0, so the first edge in the instance clashes.
        Assert.Equal("Try {a,b,c,d,e,f,g,h,i},{},{}: a and b are both color 0.", steps[0].Caption);
        Assert.Equal(["a", "b"], steps[0].Focus);
        Assert.Equal(StepEvent.Reject, steps[0].Event);
        Assert.All(rejects, s => Assert.Matches(@"^Try \{[^}]*\},\{[^}]*\},\{[^}]*\}: (\S+ and \S+ are both color \d|\S+ has no color)\.$", s.Caption));
    }

    [Fact]
    public void ColoringBruteForce_ACandidateWithAnUncoloredNodeLeavesItOutOfThePartial() {
        // K = 1 on two joined nodes: candidates are digits 0..1 per node; (0,0) clashes, (1,0) leaves node 1 uncolored.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringBruteForce()).Run("(({1,2,3},{{1,2}}),1)", true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.Contains(steps, s => s.Caption.EndsWith("1 has no color.") && !s.Partial.Of.ContainsKey("1") && s.Partial.Of.ContainsKey("2"));
    }

    [Fact]
    public void CliqueCoverBruteForce_RejectsNameTheUnjoinedPair_AndTheDefaultEndsOnTheThreeCliques() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run(new CLIQUECOVER().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        var rejects = steps.Where(s => s.Event == StepEvent.Reject).ToList();
        Assert.NotEmpty(rejects);
        Assert.All(rejects, s => Assert.Matches(@"^Try \{[^}]*\},\{[^}]*\},\{[^}]*\}: (\S+ and \S+ are in one group but aren't joined|\S+ is in no group)\.$", s.Caption));
        // The search tries far more than 150 candidates, so the recorder keeps the first 150, then the Accept for the
        // winning candidate (kept past the cap), then Done.
        Assert.Equal(152, steps.Count);
        Assert.Equal(StepEvent.Accept, steps[^2].Event);
        Assert.Equal(steps[^1].Partial, steps[^2].Partial);
        Assert.EndsWith("Every group is a clique.", steps[^2].Caption);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Contains("not shown", steps[^1].Caption);
        Assert.Equal(["1", "2", "3"], steps[^1].Partial.Of.Where(p => p.Value == steps[^1].Partial.Of["1"]).Select(p => p.Key).Order());
    }

    [Fact]
    public void CliqueCoverBruteForce_WhenKExceedsTheNodesItSaysSo() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run("(({1,2,3},{{1,2}}),4)", true);
        var step = Assert.IsType<SolverStep<Groups>>(Assert.Single(run.Steps));
        Assert.Equal(StepEvent.Done, step.Event);
        Assert.False(step.Ok);
        Assert.Contains("K = 4 is more than the 3 nodes", step.Caption);
    }

    [Fact]
    public void TheRecorderCapKeepsTheFirst150_AndDoneSaysHowManyAreHidden() {
        // K6 needs 6 colors; with K = 3 every one of the 4^6 candidates fails.
        const string instance = "(({1,2,3,4,5,6},{{1,2},{1,3},{1,4},{1,5},{1,6},{2,3},{2,4},{2,5},{2,6},{3,4},{3,5},{3,6},{4,5},{4,6},{5,6}}),3)";
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringBruteForce()).Run(instance, true);
        Assert.Equal(151, run.Steps.Count);
        Assert.Contains("not shown", ((SolverStep<Groups>)run.Steps[^1]).Caption);
        Assert.Equal("{}", run.Answer);
    }
}
