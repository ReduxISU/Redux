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
// ones with no answer). A row changes only when a bug fix changes that answer (#752: Clique Cover with K = 1 or K > n).
public class GroupSolvers_Tests {
    // Graph Coloring's default instance before #938 changed it; these steps are written for it.
    private const string OldColoringDefault = "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)";

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
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringGreedy()).Run(OldColoringDefault, true);
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
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringBruteForce()).Run(OldColoringDefault, true);
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
        Assert.All(rejects, s => Assert.Matches(@"^Try \{[^}]*\},\{[^}]*\},\{[^}]*\}: (\S+ and \S+ are in one group but aren't joined|nodes? .+ (is|are) in no clique)\.$", s.Caption));
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
    public void CliqueCoverBruteForce_ACandidateThatLeavesANodeOut_SaysSoFirst() {
        // Digits run 0..3 per node, node 1 first: 300 is {2,3},{},{} with node 1 in no group. 2 and 3 are not joined
        // either, but the left-out node is the first reason, and it is the focus (#889).
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run("(({1,2,3},{{1,2}}),4)", true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        var leftOut = steps.First(s => s.Event == StepEvent.Reject && !s.Partial.Of.ContainsKey("1"));
        Assert.Equal("Try {2,3},{},{}: node 1 is in no clique.", leftOut.Caption);
        Assert.Equal(["1"], leftOut.Focus);
    }

    [Fact]
    public void CliqueCoverBruteForce_WhenKExceedsTheNodes_SearchesWithOneGroupPerNode() {
        // K = 4 > 3 nodes: a cover never needs more than 3 cliques, so the search uses 3 groups (#752; it used to give up).
        // Digits run 0..3 per node, node 1 first: 000, 100, 200, 300 (node 1 in no group), 010, then 110 is {3},{1,2},{}.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run("(({1,2,3},{{1,2}}),4)", true);
        Assert.Equal("{{3},{1,2},{}}", run.Answer);
        var done = run.Steps.Cast<SolverStep<Groups>>().Last();
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.True(done.Ok);
        Assert.Equal("{3},{1,2},{} covers the nodes with cliques, found after 6 tries.", done.Caption);
    }

    [Fact]
    public void CliqueCoverBruteForce_WithKOf1_TriesTheOneGrouping() {
        // K = 1: the only grouping puts every node in one group (#752; the loop used to end before trying it).
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run("(({1,2,3},{{1,2},{1,3},{2,3}}),1)", true);
        Assert.Equal("{{1,2,3}}", run.Answer);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.Equal([StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal("{1,2,3} covers the nodes with cliques, found after 1 tries.", steps[^1].Caption);
    }

    [Fact]
    public void CliqueCoverBruteForce_WithKOf0_SaysNoGroupsAreAllowed() {
        // K = 0 used to hang (#752). With no groups the nodes can't be covered.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run("(({1,2,3},{{1,2}}),0)", true);
        Assert.Equal("{}", run.Answer);
        var step = Assert.IsType<SolverStep<Groups>>(Assert.Single(run.Steps));
        Assert.Equal(StepEvent.Done, step.Event);
        Assert.False(step.Ok);
        Assert.Equal("K = 0 allows no groups, so the 3 nodes can't be covered.", step.Caption);
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

    [Theory]
    [InlineData("(({},{}),2)")]
    [InlineData("(({},{}),0)")]
    [InlineData("(({1,2,3},{{1,2}}),0)")]
    [InlineData("(({1,2,3},{{1,2}}),-1)")]
    [InlineData("(({1,2,3,4},{{1,2},{3,4}}),1)")]
    public void CliqueCover_GivingUp_DoneOkIsWhatTheVerifierSaysOfItsAnswer(string instance) {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers.CliqueCoverBruteForce()).Run(instance, true);
        Assert.Equal("{}", run.Answer);
        var problem = (IProblem)new API.Problems.NPComplete.NPC_CLIQUECOVER.CLIQUECOVER();
        Assert.Equal(problem.defaultVerifier.verify(instance, run.Answer), run.Steps.Cast<SolverStep<Groups>>().Last().Ok);
    }

    [Theory]
    [InlineData("(({1,2},{{1,2}}),0)", false, "K = 0 allows no colors, so the 2 nodes can't be colored.")]
    [InlineData("(({1},{}),-1)", false, "K = -1 allows no colors, so the 1 node can't be colored.")]
    [InlineData("(({},{}),2)", true, "The graph has no nodes, so there is nothing to color.")]
    public void ColoringBruteForce_WithNoColorsToGive_AnswersAtOnce(string instance, bool ok, string caption) {
        // K <= 0 used to hang (#758); with no nodes there is nothing to color.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringBruteForce()).Run(instance, true);
        Assert.Equal("{}", run.Answer);
        var step = Assert.IsType<SolverStep<Groups>>(Assert.Single(run.Steps));
        Assert.Equal(StepEvent.Done, step.Event);
        Assert.Equal(ok, step.Ok);
        Assert.Equal(caption, step.Caption);
    }

    [Fact]
    public void Greedy_WithKOf1_SaysOnlyColor0IsAllowed() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringGreedy()).Run("(({1,2},{{1,2}}),1)", true);
        var reject = run.Steps.Cast<SolverStep<Groups>>().Single(s => s.Event == StepEvent.Reject);
        Assert.Equal("Node 2 needs color 1, but K = 1 allows only color 0: neighbour 1 already uses color 0.", reject.Caption);
    }
}
