using System.Numerics;
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
        if (result.StartsWith('(')) Assert.True(problem.defaultVerifier.verify(problem, result));
    }

    [Fact]
    public void FractionalRelaxationRequiresIntegerBranching() {
        // Divisibility tightening rules out x+y=1/2.
        var problem = new INTEGERLINEARPROGRAMMING("((1,1),((2,2),(-2,-2)),(1,-1))");
        Assert.Equal("infeasible", problem.defaultSolver.solve(problem));
    }

    [Fact]
    public void BranchingRejectsUniqueFractionalPoint() {
        // The only real solution is (1/3,1/3); each row has coefficient gcd 1.
        var problem = new INTEGERLINEARPROGRAMMING("((1,1),((1,2),(-1,-2),(2,1),(-2,-1)),(1,-1,1,-1))");
        Assert.Equal("infeasible", problem.defaultSolver.solve(problem));
    }

    [Fact]
    public void CoupledBoundsNeedNoSingleVariableConstraints() {
        var problem = new INTEGERLINEARPROGRAMMING("((2,1),((1,1),(-1,-1),(1,-1),(-1,1)),(4,0,2,2))");
        string result = problem.defaultSolver.solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, result));
        Assert.Equal("(3,1)", result);
    }

    [Fact]
    public void SolverHonorsTimerAndWorkLimit() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var solver = new IntegerLinearProgrammingSolver { timerHasExpired = true };
        Assert.Equal("timeout", solver.solve(problem));
        ((ISolver)solver).ResetTimer();
        Assert.Equal("(2,2)", solver.solve(problem));
        solver.WorkLimit = 1;
        Assert.Equal("timeout", solver.solve(problem));
    }

    [Theory]
    [InlineData("(2,2)", true)]
    [InlineData("(0,0)", false)]
    [InlineData("(1,1)", false)]
    [InlineData("(1,2)", false)]
    [InlineData("(2,1)", false)]
    [InlineData("(3,1)", false)]
    [InlineData("(-1,1)", false)]
    public void VerifierChecksOptimality(string certificate, bool expected) {
        var problem = new INTEGERLINEARPROGRAMMING();
        Assert.Equal(expected, problem.defaultVerifier.verify(problem, certificate));
    }
    [Theory]
    [InlineData("")]
    [InlineData("(1)")]
    [InlineData("(1,2,3)")]
    [InlineData("(1.5,2)")]
    [InlineData("(a,2)")]
    [InlineData("timeout")]
    [InlineData("{1,2}")]
    [InlineData("((1),2)")]
    public void MalformedCertificatesHaveStructuredErrors(string certificate) {
        var problem = new INTEGERLINEARPROGRAMMING();
        Assert.Throws<CertificateParseException>(() => problem.defaultVerifier.verify(problem, certificate));
    }

    [Fact]
    public void VerifierUsesExactArithmeticBeyondInt64() {
        var problem = new INTEGERLINEARPROGRAMMING("((1),((99999999999999999999)),(99999999999999999999))");
        Assert.True(problem.defaultVerifier.verify(problem, "(1)"));
        Assert.False(problem.defaultVerifier.verify(problem, "(99999999999999999999)"));
    }

    [Fact]
    public void VisualizationShowsAssignmentValueAndSlack() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var visualization = new IntegerLinearProgrammingDefaultVisualization();
        Assert.Equal(VisualizationType.DynamicTable, visualization.visualizationType);
        var table = Assert.IsType<API_TableJSON>(visualization.SolvedVisualization(problem, "(2,2)"));
        Assert.Equal("10", table.rows.Single(r => r.id == "objective").cells["value"]);
        Assert.Equal("0", table.rows.Single(r => r.id == "constraint1").cells["slack"]);
        Assert.Equal("1", table.rows.Single(r => r.id == "constraint3").cells["slack"]);
        Assert.Equal("2", table.rows.Single(r => r.id == "assignment").cells["x1"]);
        Assert.Equal(problem.A.Count + 1, visualization.visualize(problem).rows.Count);
    }

    [Theory]
    [InlineData("infeasible")]
    [InlineData("unbounded")]
    [InlineData("timeout")]
    public void VisualizationDisplaysStatusWithoutParsingItAsAVector(string status) {
        var problem = new INTEGERLINEARPROGRAMMING();
        var table = Assert.IsType<API_TableJSON>(problem.defaultVisualization.SolvedVisualization(problem, status));
        Assert.False(string.IsNullOrWhiteSpace(table.title));
        Assert.DoesNotContain(table.rows, r => r.id == "assignment");
    }

    [Fact]
    public void StepVisualizationUsesSuppliedSnapshots() {
        var problem = new INTEGERLINEARPROGRAMMING();
        var steps = problem.defaultSolver.GetSteps(problem);
        var tables = problem.defaultVisualization.StepsVisualization(problem, steps);
        Assert.Equal(steps.Count, tables.Count);
        Assert.Equal("Optimal assignment", Assert.IsType<API_TableJSON>(tables[^1]).title);
        Assert.Equal("10", Assert.IsType<API_TableJSON>(tables[^1]).rows[0].cells["value"]);
        Assert.Empty(problem.defaultVisualization.StepsVisualization(problem, new List<object>()));
    }

    [Fact]
    public void SmallBoundedProgramsAgreeWithExhaustiveEnumeration() {
        var random = new Random(2718);
        for (int sample = 0; sample < 40; sample++) {
            int c1 = random.Next(-3, 4), c2 = random.Next(-3, 4);
            int a1 = random.Next(-3, 4), a2 = random.Next(-3, 4), b = random.Next(-5, 6);
            var problem = new INTEGERLINEARPROGRAMMING($"(({c1},{c2}),((1,0),(-1,0),(0,1),(0,-1),({a1},{a2})),(3,3,3,3,{b}))");
            BigInteger? expected = null;
            for (int x = -3; x <= 3; x++)
                for (int y = -3; y <= 3; y++)
                    if (a1 * x + a2 * y <= b) {
                        BigInteger value = c1 * x + c2 * y;
                        if (expected is null || value > expected.Value) expected = value;
                    }
            string result = problem.defaultSolver.solve(problem);
            if (expected is null) Assert.Equal("infeasible", result);
            else {
                Assert.True(problem.defaultVerifier.verify(problem, result));
                var vector = IntegerLinearProgrammingVerifier.ParseAssignment(problem, result);
                Assert.Equal(expected.Value, c1 * vector[0] + c2 * vector[1]);
            }
        }
    }
}
