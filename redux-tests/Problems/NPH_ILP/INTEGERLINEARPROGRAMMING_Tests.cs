using System.Numerics;
using System.Text.Json;
using API.Interfaces.Steps;
using Microsoft.AspNetCore.Mvc;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.NPHard.NPH_ILP;
using API.Problems.NPHard.NPH_ILP.Solvers;
using API.Problems.NPHard.NPH_ILP.Verifiers;
using API.Problems.NPHard.NPH_ILP.Visualizations;
using Xunit;

namespace redux_tests;

public class INTEGERLINEARPROGRAMMING_Tests {
    [Fact]
    public void DefaultProgramHasOptimalValueTen() {
        var problem = new INTEGERLINEARPROGRAMMING();
        Assert.Equal("(2,2)", problem.defaultSolver.solve(problem));
        Assert.True(problem.defaultVerifier.verify(problem, "(2,2)"));
    }

    [Theory]
    [InlineData("((1),((2),(-1)),(5,0))", "(2)")]
    [InlineData("((-1),((1),(-1)),(-2,5))", "(-5)")]
    [InlineData("((1),((1),(-1)),(-2,5))", "(-2)")]
    [InlineData("((1),((2),(-2)),(1,-1))", "infeasible")]
    [InlineData("((1),((1),(-1)),(0,-1))", "infeasible")]
    [InlineData("((1),((0)),(-1))", "infeasible")]
    [InlineData("((1),((-1)),(0))", "unbounded")]
    [InlineData("((-1),((1)),(0))", "unbounded")]
    [InlineData("((0),((-1)),(0))", "(0)")]
    [InlineData("((1,0),((1,0),(-1,0)),(2,0))", "(2,0)")]
    [InlineData("((1,0),((0,2),(0,-2)),(1,-1))", "infeasible")]
    [InlineData("((1),((1),(-1)),(100000000000000000000,0))", "(100000000000000000000)")]
    public void SolverHandlesIntegerBoundsAndStatuses(string input, string expected) {
        var problem = new INTEGERLINEARPROGRAMMING(input);
        string result = problem.defaultSolver.solve(problem);

        Assert.Equal(expected, result);

        if (result.StartsWith('('))
            Assert.True(problem.defaultVerifier.verify(problem, result));
    }

    [Fact]
    public void FractionalRelaxationRequiresIntegerBranching() {
        // Divisibility tightening rules out x+y=1/2.
        var problem = new INTEGERLINEARPROGRAMMING(
            "((1,1),((2,2),(-2,-2)),(1,-1))"
        );

        Assert.Equal("infeasible", problem.defaultSolver.solve(problem));
    }

    [Fact]
    public void BranchingRejectsUniqueFractionalPoint() {
        // The only real solution is (1/3,1/3).
        var problem = new INTEGERLINEARPROGRAMMING(
            "((1,1),((1,2),(-1,-2),(2,1),(-2,-1)),(1,-1,1,-1))"
        );

        Assert.Equal("infeasible", problem.defaultSolver.solve(problem));
    }

    [Fact]
    public void CoupledBoundsNeedNoSingleVariableConstraints() {
        var problem = new INTEGERLINEARPROGRAMMING(
            "((2,1),((1,1),(-1,-1),(1,-1),(-1,1)),(4,0,2,2))"
        );

        string result = problem.defaultSolver.solve(problem);

        Assert.True(problem.defaultVerifier.verify(problem, result));
        Assert.Equal("(3,1)", result);
    }

    [Fact]
    public void SolverHonorsTimerAndWorkLimit() {
        var problem = new INTEGERLINEARPROGRAMMING();

        var solver = new IntegerLinearProgrammingSolver {
            timerHasExpired = true
        };

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));

        ((ISolver)solver).ResetTimer();

        Assert.Equal("(2,2)", solver.solve(problem));

        solver.WorkLimit = 1;

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    [Theory]
    [InlineData("(2,2)", true)]
    [InlineData("(0,0)", true)]
    [InlineData("(1,1)", true)]
    [InlineData("(1,2)", true)]
    [InlineData("(2,1)", true)]
    [InlineData("(3,1)", false)]
    [InlineData("(-1,1)", false)]
    public void VerifierChecksFeasibility(string certificate, bool expected) {
        var problem = new INTEGERLINEARPROGRAMMING();

        Assert.Equal(
            expected,
            problem.defaultVerifier.verify(problem, certificate)
        );
    }

    [Fact]
    public void VerifierAcceptsFeasibleNonOptimalAssignment() {
        var problem = new INTEGERLINEARPROGRAMMING();

        // The optimal solution is (2,2), but (1,3) is also feasible.
        Assert.Equal("(2,2)", problem.defaultSolver.solve(problem));
        Assert.True(problem.defaultVerifier.verify(problem, "(1,3)"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("(1)")]
    [InlineData("(1,2,3)")]
    [InlineData("(1.5,2)")]
    [InlineData("(a,2)")]
    [InlineData(SolveRun.TimedOutAnswer)]
    [InlineData("{1,2}")]
    [InlineData("((1),2)")]
    public void MalformedCertificatesHaveStructuredErrors(string certificate) {
        var problem = new INTEGERLINEARPROGRAMMING();

        Assert.Throws<CertificateParseException>(
            () => problem.defaultVerifier.verify(problem, certificate)
        );
    }

    [Fact]
    public void VerifierUsesExactArithmeticBeyondInt64() {
        var problem = new INTEGERLINEARPROGRAMMING(
            "((1),((99999999999999999999)),(99999999999999999999))"
        );

        Assert.True(problem.defaultVerifier.verify(problem, "(1)"));

        Assert.False(
            problem.defaultVerifier.verify(
                problem,
                "(99999999999999999999)"
            )
        );
    }

    [Fact]
    public void VisualizationShowsAssignmentValueAndSlack() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var visualization = new IntegerLinearProgrammingDefaultVisualization();

        Assert.Equal(
            VisualizationType.DynamicTable,
            visualization.visualizationType
        );

        var table = Assert.IsType<API_TableJSON>(
            visualization.SolvedVisualization(problem, "(2,2)")
        );

        Assert.Equal(
            "10",
            table.rows.Single(r => r.id == "objective").cells["value"]
        );

        Assert.Equal(
            "0",
            table.rows.Single(r => r.id == "constraint1").cells["slack"]
        );

        Assert.Equal(
            "1",
            table.rows.Single(r => r.id == "constraint3").cells["slack"]
        );

        Assert.Equal(
            "2",
            table.rows.Single(r => r.id == "assignment").cells["x1"]
        );

        Assert.Equal(
            problem.A.Count + 1,
            visualization.visualize(problem).rows.Count
        );
    }

    [Fact]
    public void VisualizationShowsFeasibleNonOptimalAssignment() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var visualization = new IntegerLinearProgrammingDefaultVisualization();

        var table = Assert.IsType<API_TableJSON>(
            visualization.SolvedVisualization(problem, "(1,3)")
        );

        Assert.Equal("Solution assignment (feasible)", table.title);

        Assert.Equal(
            "9",
            table.rows.Single(r => r.id == "objective").cells["value"]
        );
    }

    [Theory]
    [InlineData("infeasible")]
    [InlineData("unbounded")]
    [InlineData(SolveRun.TimedOutAnswer)]
    public void VisualizationDisplaysStatusWithoutParsingItAsAVector(string status) {
        var problem = new INTEGERLINEARPROGRAMMING();

        var table = Assert.IsType<API_TableJSON>(
            problem.defaultVisualization.SolvedVisualization(problem, status)
        );

        Assert.False(string.IsNullOrWhiteSpace(table.title));

        Assert.DoesNotContain(
            table.rows,
            r => r.id == "assignment"
        );
    }

    [Fact]
    public void StepVisualizationUsesSuppliedSnapshots() {
        var problem = new INTEGERLINEARPROGRAMMING();

        var steps = problem.defaultSolver.GetSteps(problem);

        var tables = problem.defaultVisualization.StepsVisualization(
            problem,
            steps
        );

        Assert.Equal(steps.Count, tables.Count);

        Assert.Equal(
            "Optimal assignment",
            Assert.IsType<API_TableJSON>(tables[^1]).title
        );

        Assert.Equal(
            "10",
            Assert.IsType<API_TableJSON>(tables[^1]).rows[0].cells["value"]
        );

        Assert.Empty(
            problem.defaultVisualization.StepsVisualization(
                problem,
                new List<object>()
            )
        );
    }

    [Fact]
    public void SmallBoundedProgramsAgreeWithExhaustiveEnumeration() {
        var random = new Random(2718);

        for (int sample = 0; sample < 40; sample++) {
            int c1 = random.Next(-3, 4);
            int c2 = random.Next(-3, 4);

            int a1 = random.Next(-3, 4);
            int a2 = random.Next(-3, 4);
            int b = random.Next(-5, 6);

            var problem = new INTEGERLINEARPROGRAMMING(
                $"(({c1},{c2}),((1,0),(-1,0),(0,1),(0,-1),({a1},{a2})),(3,3,3,3,{b}))"
            );

            BigInteger? expected = null;

            for (int x = -3; x <= 3; x++) {
                for (int y = -3; y <= 3; y++) {
                    if (a1 * x + a2 * y <= b) {
                        BigInteger value = c1 * x + c2 * y;

                        if (expected is null || value > expected.Value)
                            expected = value;
                    }
                }
            }

            string result = problem.defaultSolver.solve(problem);

            if (expected is null) {
                Assert.Equal("infeasible", result);
            } else {
                Assert.True(
                    problem.defaultVerifier.verify(problem, result)
                );

                var vector =
                    IntegerLinearProgrammingVerifier.ParseAssignment(
                        problem,
                        result
                    );

                Assert.Equal(
                    expected.Value,
                    c1 * vector[0] + c2 * vector[1]
                );
            }
        }
    }
    [Fact]
    public void BruteForceFindsDefaultOptimum() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var solver = new IntegerLinearProgrammingBruteForceSolver();

        string result = solver.solve(problem);

        Assert.Equal("(2,2)", result);
        Assert.True(problem.defaultVerifier.verify(problem, result));
    }

    [Theory]
    [InlineData("((1),((1),(-1)),(2,3))", "(2)")]
    [InlineData("((-1),((1),(-1)),(2,3))", "(-3)")]
    [InlineData("((1),((1),(-1)),(-2,0))", "infeasible")]
    public void BruteForceHandlesBoundsAndLimits(
        string input,
        string expected
    ) {
        var problem = new INTEGERLINEARPROGRAMMING(input);
        var solver = new IntegerLinearProgrammingBruteForceSolver();

        Assert.Equal(expected, solver.solve(problem));
    }

    [Fact]
    public void BruteForceRequiresFiniteEnumerationBounds() {
        var problem = new INTEGERLINEARPROGRAMMING(
            "((1),((-1)),(0))"
        );

        var solver = new IntegerLinearProgrammingBruteForceSolver();

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    [Fact]
    public void BruteForceHonorsTimerAndWorkLimit() {
        var problem = new INTEGERLINEARPROGRAMMING();

        var solver = new IntegerLinearProgrammingBruteForceSolver {
            timerHasExpired = true
        };

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));

        ((ISolver)solver).ResetTimer();

        Assert.Equal("(2,2)", solver.solve(problem));

        solver.WorkLimit = 1;

        Assert.Throws<ExactSearchLimitExceededException>(() => solver.solve(problem));
    }

    [Fact]
    public void BruteForceAgreesWithIndependentEnumeration() {
        var random = new Random(20261109);

        for (int sample = 0; sample < 50; sample++) {
            int c1 = random.Next(-3, 4);
            int c2 = random.Next(-3, 4);
            int a1 = random.Next(-3, 4);
            int a2 = random.Next(-3, 4);
            int rhs = random.Next(-4, 5);

            var problem = new INTEGERLINEARPROGRAMMING(
                $"(({c1},{c2}),((1,0),(-1,0),(0,1),(0,-1),({a1},{a2})),(2,2,2,2,{rhs}))"
            );

            BigInteger? expected = null;

            for (int x = -2; x <= 2; x++) {
                for (int y = -2; y <= 2; y++) {
                    if (a1 * x + a2 * y > rhs)
                        continue;

                    BigInteger value = c1 * x + c2 * y;

                    if (expected is null || value > expected.Value)
                        expected = value;
                }
            }

            string result = new IntegerLinearProgrammingBruteForceSolver()
                .solve(problem);

            if (expected is null) {
                Assert.Equal("infeasible", result);
            } else {
                Assert.True(problem.defaultVerifier.verify(problem, result));

                var vector = IntegerLinearProgrammingVerifier.ParseAssignment(
                    problem, result
                );

                Assert.Equal(
                    expected.Value,
                    c1 * vector[0] + c2 * vector[1]
                );
            }
        }
    }

    // ------------------------------------------------------------
    // Heuristic solver tests
    // ------------------------------------------------------------

    [Fact]
    public void HeuristicReturnsFeasibleAssignment() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var solver = new IntegerLinearProgrammingHeuristicSolver();

        string result = solver.solve(problem);

        Assert.StartsWith("(", result);
        Assert.True(problem.defaultVerifier.verify(problem, result));
    }

    [Fact]
    public void HeuristicDoesNotClaimOptimalityGuarantee() {
        var solver = new IntegerLinearProgrammingHeuristicSolver();

        Assert.Contains(
            "not guaranteed optimal",
            solver.solverDefinition
        );
    }

    [Fact]
    public void HeuristicDoesNotFalselyDeclareInfeasibility() {
        var problem = new INTEGERLINEARPROGRAMMING(
            "((1),((1),(-1)),(-2,0))"
        );

        var solver = new IntegerLinearProgrammingHeuristicSolver();

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    [Fact]
    public void HeuristicHonorsTimer() {
        var problem = new INTEGERLINEARPROGRAMMING();

        var solver = new IntegerLinearProgrammingHeuristicSolver {
            timerHasExpired = true
        };

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    // ------------------------------------------------------------
    // Step recording
    // ------------------------------------------------------------

    [Fact]
    public void AdditionalSolversProduceSteps() {
        var problem = new INTEGERLINEARPROGRAMMING();

        var bruteForce = new IntegerLinearProgrammingBruteForceSolver();
        var heuristic = new IntegerLinearProgrammingHeuristicSolver();

        Assert.NotEmpty(bruteForce.GetSteps(problem));
        Assert.NotEmpty(heuristic.GetSteps(problem));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void BruteForceRejectsOversizedBoxWithoutClaimingTimeout(int workLimit) {
        var problem = new INTEGERLINEARPROGRAMMING();
        var solver = new IntegerLinearProgrammingBruteForceSolver { WorkLimit = workLimit };
        var error = Assert.Throws<ExactSearchLimitExceededException>(() => solver.solve(problem));
        Assert.Equal(new BigInteger(12), error.SearchSpaceSize!.Value);
        Assert.Equal(workLimit, error.MaxAssignments!.Value);
        Assert.Contains("assignments", error.Message);
        Assert.False(solver.timerHasExpired);
    }

    [Fact]
    public void BruteForceAcceptsBoxExactlyAtWorkLimit() {
        var solver = new IntegerLinearProgrammingBruteForceSolver { WorkLimit = 12 };
        Assert.Equal("(2,2)", solver.solve(new INTEGERLINEARPROGRAMMING()));
    }

    [Fact]
    public void BruteForceRejectsHugeBoxWithoutIntegerOverflow() {
        var problem = new INTEGERLINEARPROGRAMMING(
            "((1),((1),(-1)),(100000000000000000000,100000000000000000000))");
        var error = Assert.Throws<ExactSearchLimitExceededException>(
            () => new IntegerLinearProgrammingBruteForceSolver().solve(problem));
        Assert.Equal(BigInteger.Parse("200000000000000000001"), error.SearchSpaceSize!.Value);
    }

    [Fact]
    public async Task OversizedBoxReturnsStructuredApiError() {
        var response = await new ProblemProvider().solve("integerlinearprogrammingbruteforcesolver",
            "((1),((1),(-1)),(100000000000000000000,100000000000000000000))");
        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var body = JsonSerializer.SerializeToElement(badRequest.Value);
        Assert.Equal("exact_search_limit_exceeded", body.GetProperty("error").GetString());
        Assert.Equal("200000000000000000001", body.GetProperty("searchSpaceSize").GetString());
        Assert.Equal(1_000_000, body.GetProperty("maxAssignments").GetInt32());
        Assert.False(body.TryGetProperty("variableCount", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdditionalSolversReplayLegacyAndTypedSteps(bool heuristic) {
        var problem = new INTEGERLINEARPROGRAMMING();
        var visualization = problem.defaultVisualization;
        ISolver<INTEGERLINEARPROGRAMMING> solver = heuristic
            ? new IntegerLinearProgrammingHeuristicSolver()
            : new IntegerLinearProgrammingBruteForceSolver();
        var legacy = solver.GetSteps(problem);
        Assert.NotEmpty(legacy);
        Assert.Equal(legacy.Count, visualization.StepsVisualization(problem, legacy).Count);

        var run = ((ISolver)solver).Run(problem.defaultInstance, withSteps: true);
        var supplied = run.StepsFor(((IVisualization)visualization).StepShape);
        Assert.NotEmpty(supplied);
        var tables = visualization.StepsVisualization(problem, supplied);
        Assert.Equal(run.Steps.Count, tables.Count);
        var final = Assert.IsType<API_TableJSON>(tables[^1]);
        Assert.Equal("10", final.rows.Single(row => row.id == "objective").cells["value"]);
        Assert.Equal(run.Answer, solver.solve(problem));
    }

    [Theory]
    [InlineData("integerlinearprogrammingbruteforcesolver")]
    [InlineData("integerlinearprogrammingheuristicsolver")]
    public async Task ChosenSolverVisualizationIncludesRecordedSteps(string solver) {
        var response = await new ProblemProvider().visualize(
            "integerlinearprogrammingdefaultvisualization",
            new INTEGERLINEARPROGRAMMING().defaultInstance, solver: solver, format: "list");
        var content = Assert.IsType<ContentResult>(response);
        using var json = JsonDocument.Parse(content.Content!);
        // Initial and solved tables alone would be two: the rest are supplied steps.
        Assert.True(json.RootElement.GetArrayLength() > 2);
    }
}
