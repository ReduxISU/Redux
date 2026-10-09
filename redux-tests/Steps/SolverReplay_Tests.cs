using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Replay test over every solver that records typed steps (implements ISolver<T, TPartial>), found by
// reflection over the API assembly so converted solvers are picked up automatically. Each is run on its
// problem's default instance.
//
// Note: a randomized solver must take a seed (or otherwise be deterministic) for the answer-equality
// check (a) to hold across runs. A solver with a public constructor taking one int is built with a fixed seed here
// (see CreateSolver); the only random ones so far are the two vertex cover 2-approximations.
public class SolverReplay_Tests {
    private static Type? TypedInterface(Type solver) =>
        solver.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>));

    public static TheoryData<string> TypedSolverNames =>
        new(ProblemProvider.Solvers.Where(kv => TypedInterface(kv.Value) != null).Select(kv => kv.Key).OrderBy(n => n));

    private const int Seed = 12345;

    // The solver as the replay should run it: seeded if it can be, so repeated runs make the same random choices.
    private static ISolver CreateSolver(Type solverType) {
        var seeded = solverType.GetConstructor([typeof(int)]);
        return (ISolver)(seeded != null ? seeded.Invoke([Seed]) : Activator.CreateInstance(solverType)!);
    }

    // The certificate a verifier expects, taken from a solver's answer string. Path-style answers
    // ("The sequence of states to accept is: 1, 2") carry the certificate after the colon; a solver whose
    // answer is already a certificate needs no mapping.
    private static string CertificateFrom(string answer) {
        string firstLine = answer.Split('\n')[0].Trim();
        if (firstLine.StartsWith("No Solution")) return "";
        const string prefix = "The sequence of states to accept is:";
        return firstLine.StartsWith(prefix) ? firstLine[prefix.Length..].Replace(" ", "") : answer;
    }

    // What the final Partial must equal, derived independently from the answer string.
    private static object ExpectedFinalPartial(object lastPartial, string answer) {
        if (lastPartial is ActiveStates last) {
            // The end states of the accepting run(s): the last state of each path line.
            var ends = answer.Split('\n')
                .Where(l => l.StartsWith("The sequence of states to accept is:"))
                .Select(l => l.Split(':')[1].Split(',').Last().Trim());
            return new ActiveStates(ends, last.Position);
        }
        if (lastPartial is NodeSet) {
            // Node-set answers are "{a,b,c}"; "{}" is no answer. Order does not matter (NodeSet sorts).
            return new NodeSet(answer.Replace("{", "").Replace("}", "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0));
        }
        throw new NotImplementedException(
            $"Add an expected final Partial for {lastPartial.GetType().Name} to SolverReplay_Tests.ExpectedFinalPartial.");
    }

    [Fact]
    public void AtLeastDfaAndNfaAreDiscovered() {
        var names = TypedSolverNames.Select(d => d.Data).ToList();
        Assert.Contains("dfasolver", names);
        Assert.Contains("nfasolver", names);
    }

    [Theory]
    [MemberData(nameof(TypedSolverNames))]
    public void Replay_OnDefaultInstance_AgreesWithSolveAndVerifier(string name) {
        Type solverType = ProblemProvider.Solvers[name];
        Type problemType = TypedInterface(solverType)!.GetGenericArguments()[0];
        var problem = (IProblem)Activator.CreateInstance(problemType)!;
        string instance = problem.defaultInstance;
        var solver = CreateSolver(solverType);

        var withSteps = solver.Run(instance, withSteps: true);
        var withoutSteps = solver.Run(instance, withSteps: false);

        // (a) The answer does not depend on recording, and matches plain solve().
        Assert.Equal(solver.solve(instance), withSteps.Answer);
        Assert.Equal(withSteps.Answer, withoutSteps.Answer);
        Assert.Empty(withoutSteps.Steps);

        // The run reports the shape of its steps, and every step has that shape.
        Type shape = TypedInterface(solverType)!.GetGenericArguments()[1];
        Assert.Equal(shape, withSteps.StepShape);
        Assert.NotEmpty(withSteps.Steps);
        Assert.All(withSteps.Steps, s => Assert.IsType(typeof(SolverStep<>).MakeGenericType(shape), s));

        // (b) The last step is Done, and its Partial is the answer's shape.
        dynamic last = withSteps.Steps[^1];
        Assert.Equal(StepEvent.Done, (StepEvent)last.Event);
        Assert.Equal(1, withSteps.Steps.Count(s => ((dynamic)s).Event == StepEvent.Done));
        object finalPartial = last.Partial;
        Assert.Equal(ExpectedFinalPartial(finalPartial, withSteps.Answer), finalPartial);

        // (c) Done.Ok agrees with the problem's own verifier on the returned answer.
        bool? ok = last.Ok;
        Assert.NotNull(ok);
        // "{}" is how the node-set solvers say "no answer"; the verifiers reject it or refuse to parse it, so it
        // counts as not verified without asking them (a vertex cover of size 3 may simply not exist, as with
        // the 2-approximation on the default Vertex Cover instance).
        bool noAnswer = finalPartial is NodeSet { Nodes.Length: 0 };
        bool verified = !noAnswer && problem.defaultVerifier.verify(instance, CertificateFrom(withSteps.Answer));
        Assert.Equal(verified, ok);
    }

    [Fact]
    public void GetSteps_OnConvertedSolver_StillReturnsTypedSteps() {
        var steps = ((ISolver)new API.Problems.P.P_DFA.Solvers.DFASolver()).GetSteps(new API.Problems.P.P_DFA.DFA().defaultInstance);
        Assert.NotEmpty(steps);
        Assert.IsType<SolverStep<ActiveStates>>(steps[0]);
    }

    [Fact]
    public void LegacySolver_RunKeepsUntypedSteps() {
        // A solver that has not been converted reports no shape and keeps its untyped steps.
        var sssp = ProblemProvider.Solvers.Values.First(t => TypedInterface(t) == null && typeof(ISolver).IsAssignableFrom(t) && t.Name == "SSSPSolver");
        var legacy = (ISolver)Activator.CreateInstance(sssp)!;
        var problem = (IProblem)Activator.CreateInstance(
            sssp.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0])!;
        var run = legacy.Run(problem.defaultInstance, true);
        Assert.Null(run.StepShape);
        Assert.NotEmpty(run.Steps);
    }

    [Fact]
    public void StepsFor_DropsStepsOnShapeMismatch_KeepsThemOnMatch() {
        object[] steps = [new SolverStep<ActiveStates>(new ActiveStates(["1"]), StepEvent.Done, [], "x") { Ok = true }];
        var typed = new SolveRun("a", steps, typeof(ActiveStates));
        var legacy = new SolveRun("a", ["legacy"], null);

        Assert.Single(typed.StepsFor(typeof(ActiveStates)));
        Assert.Empty(typed.StepsFor(typeof(NodeSet)));   // different typed shape
        Assert.Empty(typed.StepsFor(null));              // typed steps, legacy visualization
        Assert.Empty(legacy.StepsFor(typeof(ActiveStates))); // legacy steps, typed visualization
        Assert.Single(legacy.StepsFor(null));            // legacy with legacy keeps working
    }
}
