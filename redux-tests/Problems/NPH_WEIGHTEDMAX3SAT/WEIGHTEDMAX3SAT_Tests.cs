using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Verifiers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Visualizations;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Visualizations;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WEIGHTEDMAX3SAT_Tests {
    private static string Instance(string formula, params long[] weights) =>
        $"(({formula}),({string.Join(",", weights)}))";

    private static string InstanceWithVariables(int count) =>
        Instance(string.Join(" & ", Enumerable.Range(0, count).Select(i => $"(x{i})")),
            Enumerable.Repeat(1L, count).ToArray());

    private static ISolver<WEIGHTEDMAX3SAT> Solver(int kind) => kind switch {
        0 => new WeightedMax3SatBruteForceSolver(),
        1 => new WeightedMax3SatBranchAndBoundSolver(),
        _ => new WeightedMax3SatConditionalExpectationsSolver(),
    };

    [Fact]
    public void Metadata_DeclaresOptimizationAndTypedDefaults() {
        IProblem problem = new WEIGHTEDMAX3SAT();
        Assert.Equal("Weighted Max-3SAT", problem.problemName);
        Assert.Equal(ComplexityClass.NPHard, problem.complexityClass);
        Assert.Equal(ProblemType.Logic, problem.problemType);
        Assert.Equal(ProblemVariant.Optimization, problem.problemVariant);
        Assert.IsType<WeightedMax3SatBruteForceSolver>(problem.defaultSolver);
        Assert.IsType<WeightedMax3SatSolutionVerifier>(problem.defaultVerifier);
        Assert.IsType<WeightedMax3SatVisualization>(problem.defaultVisualization);
        Assert.Same(problem.defaultSolver, problem.defaultSolver);
        Assert.Equal(typeof(object), typeof(WEIGHTEDMAX3SAT).BaseType);
        Assert.Contains("1–3 literal occurrences", problem.instanceFormat);
        Assert.Contains(WeightedMax3SatSolutionVerifier.CertificateExample, problem.certificateFormat);
        Assert.True(problem.defaultVerifier.verify(problem.instance, WeightedMax3SatSolutionVerifier.CertificateExample));
        Assert.True(new WeightedMax3SatOptimalityVerifier().verify(
            (WEIGHTEDMAX3SAT)problem, WeightedMax3SatSolutionVerifier.CertificateExample));
        Assert.Contains("OPT/2", new WeightedMax3SatConditionalExpectationsSolver().solverDefinition);
    }

    [Theory]
    [InlineData("x", 1)]
    [InlineData("(x | y)", 2)]
    [InlineData("(x | y | z)", 3)]
    [InlineData("(x | x | x)", 3)]
    [InlineData("(x | !x | x)", 3)]
    public void Parser_AcceptsOneToThreeLiteralOccurrences(string formula, int count) {
        var problem = new WEIGHTEDMAX3SAT(Instance(formula, 7));
        Assert.Equal(count, problem.weightedMaxSat.weightedClauses.Single().Clause.Literals.Count);
        Assert.Equal(7, problem.weightedMaxSat.weightedClauses.Single().Weight);
    }

    [Theory]
    [InlineData("(a | b | c | d)")]
    [InlineData("(x | x | x | x)")]
    [InlineData("(x | !x | x | !x)")]
    [InlineData("x & (a | b | c | d | e)")]
    public void Parser_RejectsFourOrMoreOccurrencesIncludingRepeats(string formula) {
        string input = formula.StartsWith("x &") ? Instance(formula, 1, 2) : Instance(formula, 1);
        var exception = Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAX3SAT(input));
        Assert.Equal("Weighted Max-3SAT", exception.ProblemName);
        Assert.Equal(input, exception.Received);
        Assert.Contains("literal occurrences", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an instance")]
    [InlineData("((),(1))")]
    [InlineData("((()),(1))")]
    [InlineData("(((x)),(-1))")]
    [InlineData("(((x)),(1.5))")]
    [InlineData("(((x)),(one))")]
    [InlineData("(((x)),(9223372036854775808))")]
    [InlineData("(((x) & (!x)),(9223372036854775807,1))")]
    [InlineData("(((x) & (!x)),(1))")]
    [InlineData("(((x)),(1,2))")]
    [InlineData("(((x)),())")]
    [InlineData("(((x)),1)")]
    [InlineData("(((x)),(1),extra)")]
    public void Parser_RejectsMalformedInputWithOuterProblemIdentity(string input) {
        var exception = Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAX3SAT(input));
        Assert.Equal("Weighted Max-3SAT", exception.ProblemName);
        Assert.Equal(input, exception.Received);
    }

    [Fact]
    public void Parser_PreservesOrderingDuplicatesWhitespaceAndZeroWeights() {
        const string input = "( ( (z | !a | z) & (z | !a | z) & (b) ) , (0,7,1) )";
        var problem = new WEIGHTEDMAX3SAT(input);
        Assert.Equal(input, problem.instance);
        Assert.Equal(new[] { "z", "a", "b" }, problem.weightedMaxSat.variables);
        Assert.Equal(new long[] { 0, 7, 1 }, problem.weightedMaxSat.weightedClauses.Select(c => c.Weight));
        Assert.Equal(problem.weightedMaxSat.formula.Clauses[0].ToString(),
            problem.weightedMaxSat.formula.Clauses[1].ToString());
        Assert.Equal(8, problem.weightedMaxSat.Value(
            new Dictionary<string, bool> { ["z"] = false, ["a"] = false, ["b"] = true }));
    }

    [Theory]
    [InlineData("(((x) & (!x)),(5,4))", "((x:True),5)")]
    [InlineData("(((x | y)),(1))", "((x:False,y:True),1)")]
    [InlineData("(((x) & (!x) & (y)),(0,0,0))", "((x:False,y:False),0)")]
    [InlineData("(((x | x | !x) & (!x)),(6,2))", "((x:False),8)")]
    [InlineData("(((x | y) & (!x) & (!y)),(3,2,2))", "((x:False,y:True),5)")]
    [InlineData("(((x) & (!x)),(9223372036854775797,10))", "((x:True),9223372036854775797)")]
    public void ExactSolvers_ReturnKnownOptimumAndFirstAssignment(string instance, string expected) {
        var problem = new WEIGHTEDMAX3SAT(instance);
        Assert.Equal(expected, new WeightedMax3SatBruteForceSolver().solve(problem));
        Assert.Equal(expected, new WeightedMax3SatBranchAndBoundSolver().solve(problem));
        Assert.True(new WeightedMax3SatOptimalityVerifier().verify(problem, expected));
    }

    [Fact]
    public void Solvers_AgreeWithIndependentExhaustiveEvaluation() {
        var random = new Random(867);
        for (int testCase = 0; testCase < 100; testCase++) {
            int count = random.Next(1, 7);
            // Zero-weight unit clauses ensure every generated variable occurs, in index order.
            var clauses = Enumerable.Range(0, count)
                .Select(i => new[] { (Variable: i, Negated: false) }).ToList();
            var weights = Enumerable.Repeat(0L, count).ToList();
            int clauseCount = random.Next(1, 12);
            for (int i = 0; i < clauseCount; i++) {
                clauses.Add(Enumerable.Range(0, random.Next(1, 4))
                    .Select(_ => (Variable: random.Next(count), Negated: random.Next(2) == 0)).ToArray());
                weights.Add(random.Next(0, 100));
            }
            string formula = string.Join(" & ", clauses.Select(clause =>
                "(" + string.Join(" | ", clause.Select(l => (l.Negated ? "!" : "") + $"x{l.Variable}")) + ")"));
            var problem = new WEIGHTEDMAX3SAT(Instance(formula, weights.ToArray()));

            // Evaluate the generated literals directly: neither the shared parser's formula
            // model nor the shared Value/Optimize methods supply this oracle.
            long Value(bool[] assignment) => clauses.Select((clause, i) =>
                clause.Any(l => assignment[l.Variable] != l.Negated) ? weights[i] : 0).Sum();
            long optimum = Enumerable.Range(0, 1 << count).Max(mask =>
                Value(Enumerable.Range(0, count).Select(i => (mask & (1 << i)) != 0).ToArray()));

            for (int kind = 0; kind < 3; kind++) {
                string certificate = Solver(kind).solve(problem);
                var parsed = WeightedMaxSatSolutionVerifier.ParseCertificate(problem.weightedMaxSat, certificate);
                long actual = Value(Enumerable.Range(0, count).Select(i => parsed.Assignment[$"x{i}"]).ToArray());
                Assert.Equal(count, parsed.Assignment.Count);
                Assert.Equal(actual, parsed.Value);
                Assert.True(new WeightedMax3SatSolutionVerifier().verify(problem, certificate));
                if (kind < 2) Assert.Equal(optimum, actual);
                else Assert.True((BigInteger)2 * actual >= optimum);
            }
        }
    }

    [Fact]
    public void ConditionalExpectations_HandlesLargeWeightsRepeatedLiteralsAndTautologies() {
        var problem = new WEIGHTEDMAX3SAT(Instance("(x | x | !x) & (!y) & y", long.MaxValue - 10, 6, 4));
        string result = new WeightedMax3SatConditionalExpectationsSolver().solve(problem);
        Assert.True(new WeightedMax3SatSolutionVerifier().verify(problem, result));
        long value = WeightedMaxSatSolutionVerifier.ParseCertificate(problem.weightedMaxSat, result).Value;
        Assert.True((BigInteger)2 * value >= long.MaxValue);
    }

    [Theory]
    [InlineData("((x:True),5)", true, true)]
    [InlineData("((x:T),5)", true, true)]
    [InlineData("((x:F),4)", true, false)]
    [InlineData("((x:False),4)", true, false)]
    [InlineData("((x:True),4)", false, false)]
    [InlineData("((x:True,y:False),5)", false, false)]
    [InlineData("((),0)", false, false)]
    public void Verifiers_DistinguishFeasibilityFromOptimality(string certificate, bool valid, bool optimal) {
        var problem = new WEIGHTEDMAX3SAT(Instance("x & !x", 5, 4));
        Assert.Equal(valid, new WeightedMax3SatSolutionVerifier().verify(problem, certificate));
        Assert.Equal(optimal, new WeightedMax3SatOptimalityVerifier().verify(problem, certificate));
    }

    [Fact]
    public void Verifiers_RejectIncompleteMultiVariableAssignment() {
        var problem = new WEIGHTEDMAX3SAT(Instance("(x | y) & (!x | z)", 5, 2));
        Assert.False(new WeightedMax3SatSolutionVerifier().verify(problem, "((x:True,y:False),5)"));
        Assert.False(new WeightedMax3SatOptimalityVerifier().verify(problem, "((x:True,y:False),5)"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("x:True,5")]
    [InlineData("((x:yes),5)")]
    [InlineData("((x:True,x:False),5)")]
    [InlineData("((x:True),-1)")]
    [InlineData("((x:True),9223372036854775808)")]
    [InlineData("((x:True),5,6)")]
    public void Verifiers_MalformedCertificateIdentifiesOuterProblem(string certificate) {
        var problem = new WEIGHTEDMAX3SAT(Instance("x", 5));
        foreach (IVerifier<WEIGHTEDMAX3SAT> verifier in new IVerifier<WEIGHTEDMAX3SAT>[] {
            new WeightedMax3SatSolutionVerifier(), new WeightedMax3SatOptimalityVerifier()
        }) {
            var exception = Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
            Assert.Same(problem, exception.Problem);
            Assert.Equal(certificate, exception.Received);
            Assert.IsType<CertificateParseException>(exception.InnerException);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExactSolvers_RejectTwentyOneVariablesWithOuterProblemIdentity(int kind) {
        var problem = new WEIGHTEDMAX3SAT(InstanceWithVariables(21));
        var exception = Assert.Throws<ExactSearchLimitExceededException>(() => Solver(kind).solve(problem));
        AssertLimit(exception);
    }

    [Fact]
    public void OptimalityLimit_DoesNotRestrictSolutionVerificationOrApproximation() {
        var problem = new WEIGHTEDMAX3SAT(InstanceWithVariables(21));
        string certificate = new WeightedMax3SatConditionalExpectationsSolver().solve(problem);
        Assert.True(new WeightedMax3SatSolutionVerifier().verify(problem, certificate));
        AssertLimit(Assert.Throws<ExactSearchLimitExceededException>(
            () => new WeightedMax3SatOptimalityVerifier().verify(problem, certificate)));
    }

    private static void AssertLimit(ExactSearchLimitExceededException exception) {
        Assert.Equal("Weighted Max-3SAT", exception.ProblemName);
        Assert.Equal(21, exception.VariableCount);
        Assert.Equal(20, exception.MaxVariables);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Solvers_AlreadyExpired_ReturnTimeoutAndCanBeReset(int kind) {
        var solver = Solver(kind);
        solver.TimerExpired();
        // Twenty variables are accepted without completing an exhaustive search.
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(new WEIGHTEDMAX3SAT(InstanceWithVariables(20))));
        solver.ResetTimer();
        Assert.True(new WeightedMax3SatSolutionVerifier().verify(new WEIGHTEDMAX3SAT(),
            solver.solve(new WEIGHTEDMAX3SAT())));
        var run = solver.Run(WEIGHTEDMAX3SAT._defaultInstance, withSteps: false, timeLimit: TimeSpan.Zero);
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void Visualization_ReusesWeightedPayloadWithOwnSolver() {
        var problem = new WEIGHTEDMAX3SAT(Instance("(x | !y | x) & !x", 5, 0));
        var visualization = new WeightedMax3SatVisualization();
        Assert.IsType<WeightedMax3SatBruteForceSolver>(visualization.solver);
        Assert.Equal(VisualizationType.BooleanSatisfiability, visualization.visualizationType);
        API_SAT initial = visualization.visualize(problem);
        Assert.Equal(new long?[] { 5, 0 }, initial.clauses.Select(c => c.weight));
        Assert.Equal(new[] { "x", "!y", "x" }, initial.clauses[0].literals.Select(l => l.literal));
        var solved = (API_SAT)visualization.SolvedVisualization(problem, "((x:True,y:False),5)");
        Assert.All(solved.clauses[0].literals, l => Assert.Equal("Solution", l.color));
        Assert.Equal("Background", solved.clauses[1].literals.Single().color);
        Assert.Equal(JsonSerializer.Serialize(new WeightedMaxSatVisualization().visualize(problem.weightedMaxSat)),
            JsonSerializer.Serialize(initial));
        Assert.Equal(JsonSerializer.Serialize(initial),
            JsonSerializer.Serialize((API_SAT)visualization.SolvedVisualization(problem, "malformed")));
    }
}
