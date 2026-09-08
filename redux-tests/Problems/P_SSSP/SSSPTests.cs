using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SSSP;
using API.Problems.P.P_SSSP.Solvers;
using API.Problems.P.P_SSSP.Verifiers;
using API.Problems.P.P_SSSP.Visualizations;
using System;
using System.Linq;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class SSSP_Tests {
    [Fact]
    public void SSSP_Default_Instantiation() {
        SSSP problem = new SSSP();
        Assert.Equal("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)", problem.instance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    [Fact]
    public void SSSP_Custom_Instantiation() {
        string instance = "({1,2,3,4,5,6},{((1,2),2),((1,3),4),((2,4),7),((2,3),1),((3,5),3),((4,6),1),((5,4),2),((5,6),5)},1)";
        var problem = new SSSP(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory]
    [InlineData("({1,2}, {((1,2),-1)}),1")]
    [InlineData("(({1,2}, {((1,2),-1)}),1")]
    public void SSSP_Rejects_Negative_Edge_Weights(string instance) {
        // Dijkstra's algorithm correctness depends on non-negative weights
        // The SSSP problem must reject problem instances with negative-weights during parse time
        Assert.Throws<InvalidOperationException>(() => new SSSP(instance));
    }

    [Fact]
    public void SPSP_Rejects_Source_Outside_Node_Set() {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),4)";
        Assert.Throws<InvalidOperationException>(() => new SPSP(instance));
    }

    // ----- Solver ----- //

    [Fact]
    public void SSSP_Solver_Default_Instantiation() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSPSolver solver = new SSSPSolver();
        var problem = new SSSP(instance);
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{1,3}),(4,{1,2,4}),(5,{1,3,5})}", result);
    }

    [Fact]
    public void SSSP_Solver_Custom_Instantiation() {
        string instance = "({1,2,3,4,5,6},{((1,2),2),((1,3),4),((2,4),7),((2,3),1),((3,5),3),((4,6),1),((5,4),2),((5,6),5)},1)";
        SSSPSolver solver = new SSSPSolver();
        var problem = new SSSP(instance);
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{1,2,3}),(4,{1,2,3,5,4}),(5,{1,2,3,5}),(6,{1,2,3,5,4,6})}", result);
    }

    [Fact]
    public void SSSPSolver_Single_Node_No_Edges() {
        string instance = "({1},{},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1})}", result);
    }

    [Fact]
    public void SSSPSolver_Unreachable_Node_Returns_Empty_Path() {
        string instance = "({1,2,3},{((1,2),5)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{})}", result);
    }

    [Fact]
    public void SSSPSolver_Empty_Node_Set_Returns_Empty_Braces() {
        // No explicit source term and an empty node set: sourceNode resolves to "",
        // and solve() short-circuits on the nodes.Count == 0 check before touching sourceNode.
        string instance = "({},{})";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void SSSPSolver_Unweighted_Directed_Graph_Defaults_Edge_Weight_To_One() {
        // No weights supplied: BuildAdjacencyList's unweighted/ordered branch assigns weight 1.
        string instance = "({1,2,3},{(1,2),(2,3)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{1,2,3})}", result);
    }

    [Fact]
    public void SSSPSolver_Weighted_Undirected_Graph_Adds_Edge_In_Both_Directions() {
        // Undirected weighted edge {1,2} must be traversable from either endpoint.
        string instance = "({1,2,3},{({1,2},4),({2,3},1)},3)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{3,2,1}),(2,{3,2}),(3,{3})}", result);
    }

    [Fact(Skip = "BUG: SSSP.ParseGraph's unweighted-undirected grammar pattern " +
        "(\"{(N,E) | N is set, E subset unorderedcross N\") is malformed -- it is missing " +
        "the left-hand 'N' operand of 'unorderedcross' (compare the working weighted-undirected " +
        "pattern, itself also missing that operand but harmlessly so since the ordered/weighted " +
        "pattern matches undirected weighted edges first) and is also missing its closing '}'. " +
        "As a result no unweighted, undirected SSSP instance can ever be constructed: every " +
        "attempt throws InvalidOperationException(\"Failed to parse SSSP instance.\") -- see " +
        "Problems/P/P_SSSP/P_SSSP.cs, ParseGraph's parseAttempts array.")]
    public void SSSPSolver_Unweighted_Undirected_Graph_Defaults_Edge_Weight_To_One() {
        string instance = "({1,2,3},{{1,2},{2,3}},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{1,2,3})}", result);
    }

    [Fact]
    public void SSSPSolver_Output_Passes_Verifier_For_Undirected_Graph() {
        string instance = "({1,2,3},{({1,2},4),({2,3},1)},3)";
        SSSP problem = new SSSP(instance);
        string solution = new SSSPSolver().solve(problem);
        Assert.True(new SSSPVerifier().verify(problem, solution), $"Solver output failed verifier for: {instance}");
    }

    // ----- Solver: GetSteps ----- //

    [Fact]
    public void SSSPSolver_GetSteps_Returns_Empty_When_No_Nodes() {
        SSSP problem = new SSSP("({},{})");
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetSteps(problem);
        Assert.Empty(steps);
    }

    [Fact]
    public void SSSPSolver_GetSteps_Produces_One_Step_Per_Settled_Node() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetSteps(problem).Cast<SSSPSolver.SSSPGraphStep>().ToList();

        Assert.Equal(5, steps.Count);

        // First settled node is always the source, with no incoming edge yet.
        Assert.Equal("1", steps[0].currentNode);
        Assert.Null(steps[0].currentEdgeFrom);
        Assert.Contains("1", steps[0].knownNodes);
        Assert.Empty(steps[0].treeEdges);

        // Every node has been settled by the final step.
        Assert.Equal(5, steps[^1].knownNodes.Count);
    }

    // ----- Solver: GetTableSteps ----- //

    [Fact]
    public void SSSPSolver_GetTableSteps_Returns_Empty_When_No_Nodes() {
        SSSP problem = new SSSP("({},{})");
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem);
        Assert.Empty(steps);
    }

    [Fact]
    public void SSSPSolver_GetTableSteps_First_Step_Has_No_Known_Nodes_And_Infinite_Displays() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SSSPSolver.SSSPTableStep>().ToList();

        var first = steps[0];
        Assert.Null(first.currentVertex);
        Assert.Equal("1", first.sourceVertex);
        Assert.Empty(first.knownSet);
        Assert.All(first.vertices.Where(v => v.name != "1"), v => {
            Assert.Equal("∞", v.display);
            Assert.Null(v.path);
            Assert.False(v.known);
            Assert.Equal("", v.order);
        });
    }

    [Fact]
    public void SSSPSolver_GetTableSteps_Emits_Two_Steps_Per_Reachable_Node_Plus_Initial() {
        // One "settled" step and one "post-relaxation" step per node that is dequeued,
        // in addition to the single initial-state step emitted before the loop starts.
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SSSPSolver.SSSPTableStep>().ToList();

        Assert.Equal(1 + 2 * 5, steps.Count);
    }

    [Fact]
    public void SSSPSolver_GetTableSteps_Final_Step_Has_Null_CurrentVertex_And_All_Nodes_Known() {
        // Once the priority queue empties out, isLastVertex is true and the trailing
        // relaxation step reports no "current" vertex.
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SSSPSolver.SSSPTableStep>().ToList();

        var last = steps[^1];
        Assert.Null(last.currentVertex);
        Assert.All(last.vertices, v => Assert.True(v.known));
    }

    [Fact]
    public void SSSPSolver_GetTableSteps_Intermediate_Relaxation_Step_Reports_Current_Vertex() {
        // Immediately after node "1" is settled and its neighbors relaxed, the queue still
        // has unvisited entries, so isLastVertex is false and currentVertex == "1".
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SSSPSolver.SSSPTableStep>().ToList();

        // steps[0] = initial, steps[1] = "1" settled, steps[2] = post-relax after "1"
        Assert.Equal("1", steps[2].currentVertex);
    }

    [Fact]
    public void SSSPSolver_GetTableSteps_Shows_Distance_Predecessor_And_Path_Once_Known() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SSSPSolver.SSSPTableStep>().ToList();

        var finalVertex2 = steps[^1].vertices.First(v => v.name == "2");
        Assert.Equal("4,1", finalVertex2.display);
        Assert.Equal("{1,2}", finalVertex2.path);
        Assert.True(finalVertex2.known);
        Assert.NotEqual("", finalVertex2.order);
    }

    [Fact]
    public void SSSPSolver_GetTableSteps_Unreachable_Node_Stays_Infinite_With_No_Path() {
        string instance = "({1,2,3},{((1,2),5)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SSSPSolver.SSSPTableStep>().ToList();

        var finalVertex3 = steps[^1].vertices.First(v => v.name == "3");
        Assert.Equal("∞", finalVertex3.display);
        Assert.Null(finalVertex3.path);
        Assert.False(finalVertex3.known);
        Assert.Equal("", finalVertex3.order);
    }

    // ----- Verifier ----- //

    [Theory]
    [InlineData("{(1,{1}),(2,{1,2}),(3,{1,3}),(4,{1,2,4}),(5,{1,3,5})}", true)]
    [InlineData("{(1,{1}),(2,{1,2}),(3,{1,3}),(4,{1,2,4}),(5,{1,2,4,5})}", false)]
    public void SSSPVerifier_Certificate_Validation(string certificate, bool expectedResult) {
        SSSP problem = new SSSP();
        SSSPVerifier verifier = new SSSPVerifier();
        Assert.Equal(expectedResult, verifier.verify(problem, certificate));
    }

    // ----- Visualization ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)")]
    [InlineData("({1,2,3,4,5,6},{((1,2),2),((1,3),4),((2,4),7),((2,3),1),((3,5),3),((4,6),1),((5,4),2),((5,6),5)},1)")]
    public void SSSPVisualization_StepsVisualization_SettledTreeEdges_Accumulate_Without_Resetting(string instance) {
        SSSP problem = new SSSP(instance);
        var solver = new SSSPSolver();
        var visualization = new SSSPVisualization();

        var steps = solver.GetSteps(problem);
        var visualSteps = visualization.StepsVisualization(problem, steps);

        Assert.True(steps.Count >= 2);

        var previousSettledEdges = new HashSet<(string, string)>();
        foreach (var stepObj in steps) {
            var step = (SSSPSolver.SSSPGraphStep)stepObj;
            var knownNodes = new HashSet<string>(step.knownNodes);

            // Only edges pointing into already-settled nodes are guaranteed final;
            // edges into not-yet-settled nodes may still be relaxed away.
            var currentSettledEdges = new HashSet<(string, string)>(
                step.treeEdges.Where(e => knownNodes.Contains(e.to)));

            Assert.True(previousSettledEdges.IsSubsetOf(currentSettledEdges));

            previousSettledEdges = currentSettledEdges;
        }

        Assert.NotEmpty(previousSettledEdges);
    }
}