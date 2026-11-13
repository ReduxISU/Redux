using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxCliqueVerifier_Tests {
    [Theory]
    [InlineData("({a,b,c},12)", true, true)]
    [InlineData("({c,a,b},12)", true, true)]
    [InlineData(" ( { c , a , b } , 12 ) ", true, true)]
    [InlineData("({d},7)", true, false)]
    [InlineData("({b,d},10)", true, false)]
    [InlineData("({},0)", true, false)]
    [InlineData("({},1)", false, false)]
    [InlineData("({a,d},11)", false, false)]
    [InlineData("({a,b,c},11)", false, false)]
    [InlineData("({a,b,c},9223372036854775807)", false, false)]
    [InlineData("({unknown},0)", false, false)]
    [InlineData("({A},4)", false, false)]
    public void Verifiers_DistinguishFeasibilityFromOptimality(string certificate, bool feasible, bool optimal) {
        var problem = new WEIGHTEDMAXCLIQUE();
        Assert.Equal(feasible, new WeightedMaxCliqueSolutionVerifier().verify(problem, certificate));
        Assert.Equal(optimal, new WeightedMaxCliqueOptimalityVerifier().verify(problem, certificate));
        IVerifier solution = new WeightedMaxCliqueSolutionVerifier();
        IVerifier optimality = new WeightedMaxCliqueOptimalityVerifier();
        Assert.Equal(feasible, solution.verify(problem.instance, certificate));
        Assert.Equal(optimal, optimality.verify(problem.instance, certificate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("garbage")]
    [InlineData("{a,b,c},12")]
    [InlineData("({a,b,c})")]
    [InlineData("({a,b,c},12,extra)")]
    [InlineData("({a,b,c},12)garbage")]
    [InlineData("({a,b,c},12)()")]
    [InlineData("({a,b,c},12))")]
    [InlineData("({a,b,c},12")]
    [InlineData("((a,b,c),12)")]
    [InlineData("({},-1)")]
    [InlineData("({},+1)")]
    [InlineData("({},1.0)")]
    [InlineData("({},1e3)")]
    [InlineData("({},one)")]
    [InlineData("({},9223372036854775808)")]
    [InlineData("({},(0))")]
    [InlineData("({a,a},8)")]
    [InlineData("({unknown,unknown},0)")]
    [InlineData("({ a , a },8)")]
    [InlineData("({a,,b},7)")]
    [InlineData("({a,b,},7)")]
    [InlineData("({a,b},)")]
    [InlineData("({a,b},7,)")]
    [InlineData("({(a,b)},7)")]
    [InlineData("({{a,b}},7)")]
    [InlineData("({a b},7)")]
    public void MalformedCertificates_ThrowCertificateParseExceptionThroughBothEntryPoints(string? certificate) {
        var problem = new WEIGHTEDMAXCLIQUE();
        IVerifier<WEIGHTEDMAXCLIQUE>[] verifiers = [new WeightedMaxCliqueSolutionVerifier(), new WeightedMaxCliqueOptimalityVerifier()];
        foreach (var verifier in verifiers) {
            var error = Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate!));
            Assert.Same(problem, error.Problem);
            Assert.Equal(certificate, error.Received);
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem.instance, certificate!));
        }
    }

    [Theory]
    [InlineData("(({},{}),())", "({},0)")]
    [InlineData("(({a,b},{}),((a,0),(b,0)))", "({},0)")]
    [InlineData("(({a,b},{}),((a,0),(b,0)))", "({a},0)")]
    [InlineData("(({a,b},{{a,b}}),((a,0),(b,0)))", "({a,b},0)")]
    [InlineData("(({a,b},{}),((a,5),(b,5)))", "({a},5)")]
    [InlineData("(({a,b},{}),((a,5),(b,5)))", "({b},5)")]
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),((a,2),(b,3),(c,1),(d,4)))", "({a,b},5)")]
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),((a,2),(b,3),(c,1),(d,4)))", "({c,d},5)")]
    [InlineData("(({b,a},{{a,b}}),((b,10),(a,9223372036854775797)))", "({b,a},9223372036854775807)")]
    [InlineData("(({a},{}),((a,9223372036854775807)))", "({a},9223372036854775807)")]
    [InlineData("(({a,A},{{a,A}}),((a,3),(A,5)))", "({a,A},8)")]
    public void Verifiers_AcceptEdgeCasesAndTiedOptima(string input, string certificate) {
        var problem = new WEIGHTEDMAXCLIQUE(input);
        Assert.True(new WeightedMaxCliqueSolutionVerifier().verify(problem, certificate));
        Assert.True(new WeightedMaxCliqueOptimalityVerifier().verify(problem, certificate));
    }

    [Theory]
    [MemberData(nameof(WeightedMaxCliqueSolver_Tests.KnownInstances), MemberType = typeof(WeightedMaxCliqueSolver_Tests))]
    public void ExactSolverCertificates_PassBothVerifiers(string input, long optimum) {
        var problem = new WEIGHTEDMAXCLIQUE(input);
        ISolver<WEIGHTEDMAXCLIQUE>[] solvers = [new WeightedMaxCliqueBruteForceSolver(), new WeightedMaxCliqueBranchAndBoundSolver()];
        foreach (var solver in solvers) {
            string certificate = solver.solve(problem);
            Assert.Equal(optimum, WeightedMaxCliqueSolutionVerifier.ParseCertificate(problem, certificate).Value);
            Assert.True(new WeightedMaxCliqueSolutionVerifier().verify(problem, certificate));
            Assert.True(new WeightedMaxCliqueOptimalityVerifier().verify(problem, certificate));
        }
    }

    internal static string IsolatedInstance(int n) =>
        "(({" + string.Join(",", Enumerable.Range(0, n).Select(i => $"v{i}")) + "},{}),("
        + string.Join(",", Enumerable.Range(0, n).Select(i => $"(v{i},1)")) + "))";

    [Fact]
    public void Optimality_EnforcesTwentyVertexGuardAfterCertificateValidation() {
        var twenty = new WEIGHTEDMAXCLIQUE(IsolatedInstance(20));
        Assert.True(new WeightedMaxCliqueOptimalityVerifier().verify(twenty, "({v0},1)"));
        var twentyOne = new WEIGHTEDMAXCLIQUE(IsolatedInstance(21));
        Assert.True(new WeightedMaxCliqueSolutionVerifier().verify(twentyOne, "({v0},1)"));
        IVerifier<WEIGHTEDMAXCLIQUE> verifier = new WeightedMaxCliqueOptimalityVerifier();
        var error = Assert.Throws<ExactSearchLimitExceededException>(() => verifier.verify(twentyOne, "({v0},1)"));
        Assert.Equal("Weighted Maximum Clique", error.ProblemName);
        Assert.Equal(21, error.VariableCount);
        Assert.Equal(20, error.MaxVariables);
        Assert.Throws<ExactSearchLimitExceededException>(() => verifier.verify(twentyOne.instance, "({v0},1)"));
        Assert.False(verifier.verify(twentyOne, "({unknown},1)"));
        Assert.False(verifier.verify(twentyOne, "({v0,v1},2)"));
        Assert.Throws<CertificateParseException>(() => verifier.verify(twentyOne, "({v0,v0},2)"));
    }

    [Fact]
    public void StringEntryPoint_PreservesInstanceParseErrors() {
        IVerifier[] verifiers = [new WeightedMaxCliqueSolutionVerifier(), new WeightedMaxCliqueOptimalityVerifier()];
        foreach (var verifier in verifiers)
            Assert.Throws<ProblemParseException>(() => verifier.verify("not an instance", "({},0)"));
    }

    [Fact]
    public void Discovery_RegistersOnlySolutionVerifierAsDefault() {
        IProblem problem = new WEIGHTEDMAXCLIQUE();
        Assert.IsType<WeightedMaxCliqueSolutionVerifier>(problem.defaultVerifier);
        Assert.Same(problem.defaultVerifier, problem.defaultVerifier);
        Assert.Equal(typeof(WeightedMaxCliqueSolutionVerifier), ProblemProvider.Verifiers["weightedmaxcliquesolutionverifier"]);
        Assert.Equal(typeof(WeightedMaxCliqueOptimalityVerifier), ProblemProvider.Verifiers["weightedmaxcliqueoptimalityverifier"]);
        Assert.Equal(typeof(WeightedMaxCliqueSolutionVerifier), ProblemDefaults.Find(typeof(WEIGHTEDMAXCLIQUE), typeof(IVerifier)));
    }
}
