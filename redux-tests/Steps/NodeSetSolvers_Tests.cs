using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Clique, Vertex Cover, Minimum Vertex Cover, Independent Set and Dominating Set solvers record NodeSet steps.
// Recording must never change an answer. Endpoints/Snapshots/nodeset_solver_answers.tsv holds what each
// deterministic solver returned (solver, instance, answer; tab separated) BEFORE it recorded steps, on its default
// instance and on 12 seeded random graphs (including ones with no answer). The two 2-approximations choose
// edges at random, so they are checked for validity instead.
public class NodeSetSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(SnapshotPath("nodeset_solver_answers.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static Type ProblemTypeOf(Type solver) =>
        solver.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];

    private static NodeSet Parse(string answer) =>
        new(answer.Replace("{", "").Replace("}", "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0));

    private static bool IsVertexCoverFamily(Type problem) => problem.Name is "VERTEXCOVER" or "MINIMUMVERTEXCOVER";

    // Checks a run against the answer the solver gave: same answer with recording on and off, the last step is Done
    // with that answer's nodes, and Done.Ok says whether there is an answer.
    private static void AssertRunAgrees(ISolver solver, Type problemType, string instance, string answer) {
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(typeof(NodeSet), on.StepShape);

        var steps = on.Steps.Cast<SolverStep<NodeSet>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.Equal(Parse(answer), steps[^1].Partial);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));

        bool expectedOk;
        if (answer != "{}") {
            var problem = (IProblem)Activator.CreateInstance(problemType)!;
            expectedOk = problem.defaultVerifier.verify(instance, answer);
            Assert.True(expectedOk, $"{answer} is not a valid answer to {instance}");
        } else {
            // "{}" means no answer, except that a graph with no edges is covered by the empty set.
            expectedOk = IsVertexCoverFamily(problemType) && instance.Contains("{})") && !instance.Contains("{{");
        }
        Assert.Equal(expectedOk, steps[^1].Ok);
    }

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeRecordingSteps(string solverName, string instance, string answer) {
        var type = ProblemProvider.Solvers[solverName];
        var solver = (ISolver)Activator.CreateInstance(type)!;
        AssertRunAgrees(solver, ProblemTypeOf(type), instance, answer);
    }

    [Theory]
    [InlineData("twoapproximationminimumvertexcover", "({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})")]
    [InlineData("twoapproximationminimumvertexcover", "({1,2,3,4,5,6},{{1,2},{1,6},{2,3},{2,4},{3,4},{4,6},{5,6}})")]
    [InlineData("twoapproximationvertexcover", "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),5)")]
    [InlineData("twoapproximationvertexcover", "(({1,2,3,4,5,6},{{1,2},{1,6},{2,3},{2,4},{3,4},{4,6},{5,6}}),6)")]
    public void TwoApproximation_WithASeed_RepeatsItself_AndCoversEveryEdge(string solverName, string instance) {
        var type = ProblemProvider.Solvers[solverName];
        var problemType = ProblemTypeOf(type);
        for (int seed = 1; seed <= 8; seed++) {
            var solver = (ISolver)type.GetConstructor([typeof(int)])!.Invoke([seed]);
            string answer = solver.solve(instance);
            AssertRunAgrees(solver, problemType, instance, answer);   // the same seed gives the same answer every run
        }
    }

    [Fact]
    public void TwoApproximation_WithoutASeed_StillCoversEveryEdge() {
        // The parameterless constructor keeps the original behaviour: random choices, a valid cover each time.
        ISolver solver = new API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers.TwoApproximationMinimumVertexCover();
        const string instance = "({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})";
        for (int i = 0; i < 20; i++) {
            var run = solver.Run(instance, withSteps: true);
            var last = (SolverStep<NodeSet>)run.Steps[^1];
            Assert.True(last.Ok);
            Assert.Equal(Parse(run.Answer), last.Partial);
            Assert.True(((IVerifier)new API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Verifiers.MinimumVertexCoverVerifier()).verify(instance, run.Answer));
        }
    }

    [Fact]
    public void StepsAreDecisionsInTheOrderTheAlgorithmMakesThem() {
        // Greedy takes the highest-degree node first; the steps say so, one Accept per node, then Done.
        var run = ((ISolver)new API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers.GreedyMinimumVertexCover())
            .Run("({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})", withSteps: true);
        var steps = run.Steps.Cast<SolverStep<NodeSet>>().ToList();
        Assert.All(steps.Take(steps.Count - 1), s => Assert.Equal(StepEvent.Accept, s.Event));
        Assert.Equal("Take a: it covers 3 more edges, the most of any node left.", steps[0].Caption);
        Assert.Equal(["a"], steps[0].Focus);
        Assert.Equal(new NodeSet(["a"]), steps[0].Partial);
    }

    [Fact]
    public void BruteForceCaptions_NameTheReasonACandidateFails() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CLIQUE.Solvers.CliqueBruteForce()).Run(new API.Problems.NPComplete.NPC_CLIQUE.CLIQUE().defaultInstance, true);
        var rejects = run.Steps.Cast<SolverStep<NodeSet>>().Where(s => s.Event == StepEvent.Reject).ToList();
        Assert.NotEmpty(rejects);
        Assert.All(rejects, s => Assert.Matches(@"^Try \{[^}]*\}: \S+ and \S+ aren't joined\.$", s.Caption));
    }
}
