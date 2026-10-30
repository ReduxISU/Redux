using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Visualizations;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WEIGHTEDMAXSAT_Tests {
    private static string Instance(string formula, params long[] weights) =>
        $"(({formula}),({string.Join(",", weights)}))";

    private static string InstanceWithVariables(int count) =>
        Instance("(" + string.Join(" | ", Enumerable.Range(0, count).Select(i => $"x{i}")) + ")", 1);

    [Fact]
    public void DefaultInstance_DeclaresCompleteMetadata() {
        var problem = new WEIGHTEDMAXSAT();
        Assert.Equal("(((!x1 | x2) & (x3 | x4)),(1,2))", problem.defaultInstance);
        Assert.Equal(ComplexityClass.NPHard, problem.complexityClass);
        Assert.Equal(ProblemType.Logic, problem.problemType);
        Assert.False(string.IsNullOrWhiteSpace(problem.instanceFormat));
        Assert.False(string.IsNullOrWhiteSpace(problem.certificateFormat));
        Assert.Equal(2, problem.weightedClauses.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("(((x)),1)")]
    [InlineData("(((x)),(1)),extra")]
    [InlineData("(((x)),(1),extra)")]
    [InlineData("(((x)),(-1))")]
    [InlineData("(((x)),(one))")]
    [InlineData("((),(1))")]
    [InlineData("((!(x)),(1))")]
    [InlineData("(((x)),())")]
    public void Parser_RejectsMalformedWeightedCnf(string instance) {
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXSAT(instance));
    }

    [Fact]
    public void Parser_AcceptsZeroAndUnitWeights() {
        var problem = new WEIGHTEDMAXSAT(Instance("(x | y) & (!x) & (!y)", 0, 1, 1));
        Assert.Equal(new long[] { 0, 1, 1 }, problem.weightedClauses.Select(c => c.Weight));
    }

    [Fact]
    public void DefaultInstance_ExpectedCertificateIsOptimal() {
        var problem = new WEIGHTEDMAXSAT();
        const string certificate = "((x1:False,x2:True,x3:True,x4:False),3)";

        Assert.Contains(certificate, problem.certificateFormat);
        Assert.True(new WeightedMaxSatOptimalityVerifier().verify(problem, certificate));
    }

    [Fact]
    public void Parser_PreservesWhitespaceRepeatedClausesAndClauseOrder() {
        var problem = new WEIGHTEDMAXSAT("(  ( (x | !y) & (x | !y) )  ,  ( 0 , 7 )  )");

        Assert.Equal(2, problem.formula.Clauses.Count);
        Assert.Equal(problem.formula.Clauses[0].ToString(), problem.formula.Clauses[1].ToString());
        Assert.Equal(new long[] { 0, 7 }, problem.weightedClauses.Select(c => c.Weight));
    }

    [Theory]
    [InlineData("(((x) & (!x)),(1))")]
    [InlineData("(((x)),(1,2))")]
    public void Parser_RejectsWeightCountDifferentFromClauseCount(string instance) {
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXSAT(instance));
    }

    [Theory]
    [InlineData("(((x) & (!x)),(5,4))", 5)]
    [InlineData("(((x) & (!x)),(1,1))", 1)]
    [InlineData("(((x) & (!x)),(0,0))", 0)]
    [InlineData("(((x | y) & (!x) & (!y)),(3,2,2))", 5)]
    public void ExactSolvers_AgreeOnKnownOptimum(string instance, long optimum) {
        var problem = new WEIGHTEDMAXSAT(instance);
        string brute = new WeightedMaxSatBruteForceSolver().solve(problem);
        string bounded = new WeightedMaxSatBranchAndBoundSolver().solve(problem);
        var verifier = new WeightedMaxSatOptimalityVerifier();

        Assert.True(verifier.verify(problem, brute));
        Assert.True(verifier.verify(problem, bounded));
        Assert.Equal(optimum, WeightedMaxSatSolutionVerifier.ParseCertificate(problem, brute).Value);
        Assert.Equal(optimum, WeightedMaxSatSolutionVerifier.ParseCertificate(problem, bounded).Value);
    }

    [Fact]
    public void UnitWeights_RecoverOrdinaryMaxSat() {
        var problem = new WEIGHTEDMAXSAT(Instance("(x | y) & (!x) & (!y)", 1, 1, 1));
        string result = new WeightedMaxSatBruteForceSolver().solve(problem);
        Assert.Equal(2, WeightedMaxSatSolutionVerifier.ParseCertificate(problem, result).Value);
    }

    [Fact]
    public void ExactSolvers_RejectMoreThanTwentyVariables() {
        var problem = new WEIGHTEDMAXSAT(InstanceWithVariables(21));

        var brute = Assert.Throws<WeightedMaxSatExactSearchLimitExceededException>(
            () => new WeightedMaxSatBruteForceSolver().solve(problem));
        var bounded = Assert.Throws<WeightedMaxSatExactSearchLimitExceededException>(
            () => new WeightedMaxSatBranchAndBoundSolver().solve(problem));

        Assert.Equal(21, brute.VariableCount);
        Assert.Equal(20, brute.MaxVariables);
        Assert.Equal(21, bounded.VariableCount);
        Assert.Equal(20, bounded.MaxVariables);
    }

    [Fact]
    public void OptimalityVerifier_RejectsMoreThanTwentyVariablesWithValidCertificate() {
        var problem = new WEIGHTEDMAXSAT(InstanceWithVariables(21));
        string certificate = "((" + string.Join(",", Enumerable.Range(0, 21).Select(i => $"x{i}:True")) + "),1)";

        var exception = Assert.Throws<WeightedMaxSatExactSearchLimitExceededException>(
            () => new WeightedMaxSatOptimalityVerifier().verify(problem, certificate));

        Assert.Equal(21, exception.VariableCount);
        Assert.Equal(20, exception.MaxVariables);
    }

    [Fact]
    public void ConditionalExpectations_AcceptsMoreThanTwentyVariables() {
        var problem = new WEIGHTEDMAXSAT(InstanceWithVariables(21));

        string certificate = new WeightedMaxSatConditionalExpectationsSolver().solve(problem);

        Assert.True(new WeightedMaxSatSolutionVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ConditionalExpectations_ReturnsFeasibleHalfApproximation() {
        var instances = new[] {
            Instance("(a | b) & (!a | c) & (!b | !c) & a", 8, 5, 3, 2),
            Instance("x & !x & (x | y)", 4, 3, 0),
            Instance("(p | q | r) & (!p | !q) & (q | !r)", 2, 7, 6),
        };
        foreach (string instance in instances) {
            var problem = new WEIGHTEDMAXSAT(instance);
            long optimum = WeightedMaxSatSolutionVerifier.ParseCertificate(problem,
                new WeightedMaxSatBruteForceSolver().solve(problem)).Value;
            string approximate = new WeightedMaxSatConditionalExpectationsSolver().solve(problem);
            var parsed = WeightedMaxSatSolutionVerifier.ParseCertificate(problem, approximate);
            Assert.True(new WeightedMaxSatSolutionVerifier().verify(problem, approximate));
            Assert.True((BigInteger)2 * parsed.Value >= optimum);
        }
    }

    [Theory]
    [InlineData("(((x | x) & (!x)),(9,4))", 9)]
    [InlineData("(((x | !x) & (y | y) & (!y)),(7,5,3))", 12)]
    [InlineData("(((x | x | !x) & (!x | !x)),(6,2))", 8)]
    public void RepeatedAndComplementaryLiterals_AreHandledCorrectly(string instance, long optimum) {
        var problem = new WEIGHTEDMAXSAT(instance);
        string brute = new WeightedMaxSatBruteForceSolver().solve(problem);
        string bounded = new WeightedMaxSatBranchAndBoundSolver().solve(problem);
        string approximate = new WeightedMaxSatConditionalExpectationsSolver().solve(problem);

        Assert.Equal(optimum, WeightedMaxSatSolutionVerifier.ParseCertificate(problem, brute).Value);
        Assert.Equal(optimum, WeightedMaxSatSolutionVerifier.ParseCertificate(problem, bounded).Value);
        Assert.True(new WeightedMaxSatSolutionVerifier().verify(problem, approximate));
    }

    [Fact]
    public void ConditionalExpectations_PeriodicallyChecksExpiration() {
        var problem = new WEIGHTEDMAXSAT(Instance("(a | b | c) & (!a | b) & (!b | c)", 5, 4, 3));
        int checks = 0;

        var result = WeightedMaxSatConditionalExpectationsSolver.Optimize(problem, () => ++checks >= 3);

        Assert.True(checks >= 3);
        Assert.True(result.Assignment.Count < problem.variables.Count);
    }

    [Fact]
    public void ConditionalExpectationsSolver_AlreadyExpired_ReturnsTimeout() {
        var solver = new WeightedMaxSatConditionalExpectationsSolver();
        ((ISolver)solver).TimerExpired();

        Assert.Equal("timeout", solver.solve(new WEIGHTEDMAXSAT()));
    }

    [Fact]
    public void ExactSolvers_AgreeOnDeterministicRandomInstances() {
        var random = new Random(858);
        for (int testCase = 0; testCase < 100; testCase++) {
            int variableCount = random.Next(1, 7);
            int clauseCount = random.Next(1, 10);
            var clauses = new List<(string Formula, long Weight)>();
            for (int clauseIndex = 0; clauseIndex < clauseCount; clauseIndex++) {
                int literalCount = random.Next(1, 6);
                var literals = Enumerable.Range(0, literalCount).Select(_ => {
                    string variable = $"x{random.Next(variableCount)}";
                    return random.Next(2) == 0 ? variable : "!" + variable;
                });
                clauses.Add(($"({string.Join(" | ", literals)})", random.Next(0, 100)));
            }

            var problem = new WEIGHTEDMAXSAT(Instance(
                string.Join(" & ", clauses.Select(clause => clause.Formula)),
                clauses.Select(clause => clause.Weight).ToArray()));
            long brute = WeightedMaxSatSolutionVerifier.ParseCertificate(problem,
                new WeightedMaxSatBruteForceSolver().solve(problem)).Value;
            long bounded = WeightedMaxSatSolutionVerifier.ParseCertificate(problem,
                new WeightedMaxSatBranchAndBoundSolver().solve(problem)).Value;
            Assert.Equal(brute, bounded);
        }
    }

    [Theory]
    [InlineData("((x:True),5)", true)]
    [InlineData("((x:False),4)", true)]
    [InlineData("((x:True),4)", false)]
    [InlineData("((x:True,y:False),5)", false)]
    [InlineData("((),0)", false)]
    public void SolutionVerifier_ChecksAssignmentAndReportedValue(string certificate, bool expected) {
        var problem = new WEIGHTEDMAXSAT(Instance("x & !x", 5, 4));
        Assert.Equal(expected, new WeightedMaxSatSolutionVerifier().verify(problem, certificate));
    }

    [Fact]
    public void SolutionVerifier_RejectsIncompleteMultiVariableCertificate() {
        var problem = new WEIGHTEDMAXSAT(Instance("(x | y) & (!x | z)", 5, 2));

        Assert.False(new WeightedMaxSatSolutionVerifier().verify(problem, "((x:True,y:False),5)"));
    }

    [Fact]
    public void LargeWeights_AreSolvedWithoutIntermediateOverflow() {
        long first = long.MaxValue - 10;
        var problem = new WEIGHTEDMAXSAT(Instance("x & !x", first, 10));

        string exact = new WeightedMaxSatBranchAndBoundSolver().solve(problem);
        string approximate = new WeightedMaxSatConditionalExpectationsSolver().solve(problem);

        Assert.Equal(first, WeightedMaxSatSolutionVerifier.ParseCertificate(problem, exact).Value);
        Assert.True(new WeightedMaxSatSolutionVerifier().verify(problem, approximate));
        Assert.True((BigInteger)2 * WeightedMaxSatSolutionVerifier.ParseCertificate(problem, approximate).Value >= first);
    }

    [Fact]
    public void Parser_RejectsTotalWeightOverflow() {
        Assert.Throws<ProblemParseException>(() =>
            new WEIGHTEDMAXSAT(Instance("x & !x", long.MaxValue, 1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("x:True,5")]
    [InlineData("((x:yes),5)")]
    [InlineData("((x:True,x:False),5)")]
    [InlineData("((x:True),-1)")]
    [InlineData("(((x,True)),5)")]
    [InlineData("((x:True),5,6)")]
    public void Verifier_RejectsMalformedCertificate(string certificate) {
        var problem = new WEIGHTEDMAXSAT(Instance("x", 5));
        Assert.Throws<CertificateParseException>(() => new WeightedMaxSatSolutionVerifier().verify(problem, certificate));
    }

    [Fact]
    public void OptimalityVerifier_RejectsFeasibleSuboptimalCertificate() {
        var problem = new WEIGHTEDMAXSAT(Instance("x & !x", 5, 4));
        Assert.True(new WeightedMaxSatSolutionVerifier().verify(problem, "((x:False),4)"));
        Assert.False(new WeightedMaxSatOptimalityVerifier().verify(problem, "((x:False),4)"));
    }

    [Fact]
    public void Visualization_ContainsWeightsAndHighlightsSatisfiedLiterals() {
        var problem = new WEIGHTEDMAXSAT(Instance("(x | !y) & !x", 5, 2));
        var visualization = new WeightedMaxSatVisualization();
        API_SAT initial = visualization.visualize(problem);
        var solved = (API_SAT)visualization.SolvedVisualization(problem, "((x:True,y:False),5)");

        Assert.Equal(new long?[] { 5, 2 }, initial.clauses.Select(c => c.weight));
        Assert.Equal("Solution", solved.clauses[0].literals.Single(l => l.literal == "x").color);
        Assert.Equal("Background", solved.clauses[1].literals.Single().color);
    }

    [Fact]
    public void Visualization_SerializesWeightsWithoutChangingOrdinarySatShape() {
        string weightedJson = JsonSerializer.Serialize(
            new WeightedMaxSatVisualization().visualize(new WEIGHTEDMAXSAT(Instance("x", 5))));
        string satJson = JsonSerializer.Serialize(
            new SatDefaultVisualization()
                .visualize(new API.Problems.NPComplete.NPC_SAT.SAT("x")));

        Assert.Contains("\"weight\":5", weightedJson);
        Assert.DoesNotContain("\"weight\"", satJson);
    }
}
