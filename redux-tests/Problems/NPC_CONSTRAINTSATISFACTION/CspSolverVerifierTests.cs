using System.Diagnostics;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class CONSTRAINTSATISFACTION_SolverVerifier_Tests {
    private static ISolver<CONSTRAINTSATISFACTION>[] Solvers() => [
        new ConstraintSatisfactionBruteForce(), new ConstraintSatisfactionBacktracking(),
        new ConstraintSatisfactionIntelligent(), new ConstraintSatisfactionIntelligent { ForwardChecking = false }
    ];

    [Theory]
    [InlineData("((x,y),({1,2},{1,2}),{\"x!=y\"})", "(x:1,y:2)")]
    [InlineData("((x,y),({1,2},{1,2}),{\"x!=y\"})", " ( y:+2 , x:1 ) ")]
    [InlineData("((_a,A),({-2},{3}),{\"_a+A=1\"})", "(_a:-2,A:3)")]
    [InlineData("((),(),{})", "()")]
    [InlineData("((),(),{\"1<2\"})", "()")]
    [InlineData("((x),({-2147483648}),{\"-x=2147483648\"})", "(x:-2147483648)")]
    [InlineData("((x),({2147483647}),{\"x+x=4294967294\"})", "(x:2147483647)")]
    public void Verifier_AcceptsValidCertificates(string instance, string certificate) {
        var verifier = new ConstraintSatisfactionVerifier();
        Assert.True(verifier.verify(new CONSTRAINTSATISFACTION(instance), certificate));
        Assert.Equal(certificate, verifier.certificate);
    }

    [Theory]
    [InlineData("()")]
    [InlineData("(x:1)")]
    [InlineData("(x:1,y:3)")]
    [InlineData("(x:1,z:2)")]
    [InlineData("(X:1,y:2)")]
    [InlineData("(x:1,x:2,y:2)")]
    [InlineData("(x:1,y:2,z:2)")]
    public void Verifier_ThrowsOnInvalidAssignments(string certificate) {
        Assert.Throws<CertificateParseException>(() => new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), certificate));
    }

    [Fact]
    public void Verifier_ReturnsFalseForValidUnsatisfiedAssignment() {
        Assert.False(new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), "(x:1,y:1)"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("(x,1)")]
    [InlineData("{x=1}")]
    [InlineData("(x:1.0)")]
    [InlineData("(x:2147483648)")]
    [InlineData("{(x,1),}")]
    [InlineData("{,(x,1)}")]
    [InlineData("{(x,1)(y,2)}")]
    [InlineData("{(x,1),,(y,2)}")]
    [InlineData("(x:1) junk")]
    [InlineData("(x:1,y:no)")]
    public void Verifier_ThrowsOnMalformedCertificates(string? certificate) {
        Assert.Throws<CertificateParseException>(() => new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), certificate!));
    }

    [Theory]
    [InlineData("x=y", false)]
    [InlineData("x==y", false)]
    [InlineData("x!=y", true)]
    [InlineData("x<y", true)]
    [InlineData("x<=y", true)]
    [InlineData("x>y", false)]
    [InlineData("x>=y", false)]
    [InlineData("-(x-(y+1))=2", true)]
    [InlineData("x--y=3", true)]
    [InlineData("x-(-(-y))=-1", true)]
    [InlineData("+(x)+(-y)=-1", true)]
    [InlineData("x-x=0", true)]
    [InlineData("x-x!=0", false)]
    [InlineData("1=2", false)]
    public void Verifier_EvaluatesExpressions(string constraint, bool expected) {
        var problem = new CONSTRAINTSATISFACTION($"((x,y),({{1}},{{2}}),{{\"{constraint}\"}})");
        Assert.Equal(expected, new ConstraintSatisfactionVerifier().verify(problem, "(x:1,y:2)"));
        foreach (var solver in Solvers()) Assert.Equal(expected, solver.solve(problem) != "{}");
    }

    [Theory]
    [InlineData("x%2=1")]
    [InlineData("x^2=1")]
    [InlineData("allDifferent(x,y)")]
    [InlineData("x<y && x>0")]
    [InlineData("0<x<3")]
    [InlineData("(x=y)")]
    [InlineData("z=1")]
    [InlineData("x===y")]
    [InlineData("x+")]
    [InlineData("x=()")]
    [InlineData("x=(1")]
    [InlineData("x=1)")]
    [InlineData("x=1 2")]
    [InlineData("x=1(2)")]
    public void UnsupportedConstraints_ThrowBeforeSearchOrVerification(string constraint) {
        var problem = new CONSTRAINTSATISFACTION($"((x,y),({{1}},{{2}}),{{\"1=2\",\"{constraint}\"}})");
        Assert.Throws<ProblemParseException>(() => new ConstraintSatisfactionVerifier().verify(problem, "(x:1,y:2)"));
        foreach (var solver in Solvers()) Assert.Throws<ProblemParseException>(() => solver.solve(problem));
    }

    [Theory]
    [InlineData("((x,y),({1,2},{1,2}),{\"x!=y\"})")]
    [InlineData("((x,y,z),({0,1},{0,1},{0,1}),{\"x+y+z=3\"})")]
    [InlineData("((x),({-1,0,1}),{\"x=1\"})")]
    [InlineData("((x,y,z),({1,2},{1,2},{1,2}),{\"x!=y\",\"y!=z\",\"x=z\"})")]
    // The first branch wipes out y's domain; a later branch must restore it.
    [InlineData("((x,y,z),({1,2},{1,2},{1,2}),{\"x+y+z=5\",\"x!=y\",\"y=z\"})")]
    [InlineData("((x,y),({1,2,3},{2}),{})")]
    [InlineData("((),(),{})")]
    [InlineData("((),(),{\"2>=1\"})")]
    public void EverySolver_FindsValidAssignment(string instance) {
        var problem = new CONSTRAINTSATISFACTION(instance);
        foreach (var solver in Solvers())
            Assert.True(new ConstraintSatisfactionVerifier().verify(problem, solver.solve(problem)), solver.GetType().Name);
    }

    [Theory]
    [InlineData("((x),({1}),{\"x!=1\"})")]
    [InlineData("((x,y,z),({1,2},{1,2},{1,2}),{\"x!=y\",\"y!=z\",\"x!=z\"})")]
    [InlineData("((x,y,z),({0,1},{0,1},{0,1}),{\"x+y+z=4\"})")]
    [InlineData("((x),({1,2}),{\"1>2\"})")]
    [InlineData("((),(),{\"1=2\"})")]
    public void EverySolver_ExhaustsUnsatisfiableInstances(string instance) {
        var problem = new CONSTRAINTSATISFACTION(instance);
        foreach (var solver in Solvers()) Assert.Equal("{}", solver.solve(problem));
        if (problem.X.Count == 0) Assert.False(new ConstraintSatisfactionVerifier().verify(problem, "()"));
        else Assert.Throws<CertificateParseException>(() => new ConstraintSatisfactionVerifier().verify(problem, "()"));
    }

    [Fact]
    public void Solvers_AgreeWithIndependentOracleForSmallIntegerProblems() {
        for (int target = -4; target <= 4; target++) {
            for (int limit = -2; limit <= 2; limit++) {
                var problem = new CONSTRAINTSATISFACTION($"((x,y,z),({{-1,0,1}},{{-1,0,1}},{{-1,0,1}}),{{\"x+y-z={target}\",\"x!=y\",\"y+z<={limit}\"}})");
                bool exists = false;
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++) {
                            bool valid = x + y - z == target && x != y && y + z <= limit;
                            string certificate = $"(x:{x},y:{y},z:{z})";
                            Assert.Equal(valid, new ConstraintSatisfactionVerifier().verify(problem, certificate));
                            exists |= valid;
                        }
                foreach (var solver in Solvers()) {
                    string answer = solver.solve(problem);
                    Assert.Equal(exists, answer != "{}");
                    if (exists) Assert.True(new ConstraintSatisfactionVerifier().verify(problem, answer));
                }
            }
        }
    }

    [Fact]
    public void Solvers_RespectExpiredFlagsAndCanBeReused() {
        foreach (var solver in Solvers()) {
            solver.timerHasExpired = true;
            Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(new CONSTRAINTSATISFACTION()));
            ((ISolver)solver).ResetTimer();
            Assert.Equal("{}", solver.solve(new CONSTRAINTSATISFACTION("((x),({1}),{\"x=2\"})")));
            Assert.True(new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), solver.solve(new CONSTRAINTSATISFACTION())));
        }
    }

    [Fact]
    public void InterfaceDispatch_UsesExistingStringFormats() {
        IVerifier verifier = new ConstraintSatisfactionVerifier();
        foreach (ISolver solver in Solvers()) {
            string instance = new CONSTRAINTSATISFACTION().defaultInstance;
            Assert.True(verifier.verify(instance, solver.solve(instance)));
            Assert.Throws<CertificateParseException>(() => verifier.verify(instance, "bad"));
        }
    }

    [Theory]
    [InlineData("((x),({},),{})")]
    [InlineData("((x),({}),{})")]
    [InlineData("((x,x),({1},{2}),{})")]
    [InlineData("((x),({1,1}),{})")]
    [InlineData("((x,y),({1}),{})")]
    public void ExistingInstanceValidation_IsPreserved(string instance) {
        Assert.Throws<ProblemParseException>(() => new CONSTRAINTSATISFACTION(instance));
    }
}

[Collection(TimingCollection.Name)]
public class CONSTRAINTSATISFACTION_Timeout_Tests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RunningSearch_StopsThroughReduxTimer(int algorithm) {
        ISolver solver = algorithm switch {
            0 => new ConstraintSatisfactionBruteForce(),
            1 => new ConstraintSatisfactionBacktracking(),
            2 => new ConstraintSatisfactionIntelligent(),
            _ => new ConstraintSatisfactionIntelligent { ForwardChecking = false }
        };
        // A global sum keeps all partial assignments undecided until the last variable;
        // proving it impossible requires an exponential search for all three algorithms.
        string[] names = Enumerable.Range(0, 32).Select(i => $"x{i}").ToArray();
        string instance = $"(({string.Join(",", names)}),({string.Join(",", names.Select(_ => "{0,1}"))}),{{\"{string.Join("+", names)}=33\"}})";
        var watch = Stopwatch.StartNew();
        SolveRun run = solver.Run(instance, withSteps: false, TimeSpan.FromMilliseconds(30));
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        // Redux resets the flag at the start of the next limited run.
        SolveRun next = solver.Run(new CONSTRAINTSATISFACTION().defaultInstance, withSteps: false, TimeSpan.FromSeconds(1));
        Assert.False(next.TimedOut);
        Assert.True(new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), next.Answer));
    }
}
