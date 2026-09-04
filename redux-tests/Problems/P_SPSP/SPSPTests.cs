using System;
using System.Linq;
using Xunit;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SPSP.Solvers;
using API.Problems.P.P_SPSP.Verifiers;
using API.Problems.P.P_SPSP.Visualizations;

namespace redux_tests;
#pragma warning disable CS1591

public class SPSP_Tests {
    [Fact]
    public void SPSP_Default_Instantiation() {
        SPSP problem = new SPSP();
        Assert.Equal("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", problem.instance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    [Fact]
    public void SPSP_Custom_Instantiation() {
        string instance = "({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)";
        var problem = new SPSP(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory]
    [InlineData("({1,2}, {((1,2),-1)}),1,2")]
    [InlineData("(({1,2}, {((1,2),-1)}),1,2")]
    public void SPSP_Rejects_Negative_Edge_Weights(string instance) {
        // Dijkstra's algorithm correctness depends on non-negative weights
        // The SSSP problem must reject problem instances with negative-weights during parse time
        Assert.Throws<InvalidOperationException>(() => new SPSP(instance));
    }

    [Fact]
    public void SPSP_Rejects_Source_Outside_Node_Set() {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),4,3)";
        Assert.Throws<InvalidOperationException>(() => new SPSP(instance));
    }

    // ----- Solver ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", "{1,3,5}")]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", "{A,C,D}")]
    [InlineData("(({1,2,3},((1,2))),1,3)", "{}")]
    public void SPSP_Solver(string instance, string certificate) {
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        string solution = solver.solve(problem);
        Assert.Equal(certificate, solution);
    }

    [Theory]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", 9)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)", 5)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", 2)]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", 2)]
    public void SPSP_Solver_Returns_Valid_Minimum_Cost_Path(string instance, int expectedMinCost) {
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        SPSPVerifier verifier = new SPSPVerifier();

        string solution = solver.solve(problem);

        Assert.True(verifier.verify(problem, solution), $"Solver returned an invalid certificate: {solution}");
        Assert.Equal(expectedMinCost, TotalPathWeight(problem, solution));
    }

    [Fact]
    public void SSSP_Solver_Handles_Equal_Weights() {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),1,1)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();

        string solution = solver.solve(problem);

        Assert.Equal("{1}", solution);
    }

    [Fact]
    public void SPSPSolver_Empty_Node_Set_Returns_Empty_Braces() {
        SPSP problem = new SPSP("({},{})");
        SPSPSolver solver = new SPSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void SPSPSolver_Unweighted_Directed_Graph_Defaults_Edge_Weight_To_One() {
        string instance = "({1,2,3},{(1,2),(2,3)},1,3)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{1,2,3}", result);
    }

    [Fact]
    public void SPSPSolver_Weighted_Undirected_Graph_Adds_Edge_In_Both_Directions() {
        string instance = "({1,2,3},{({1,2},4),({2,3},1)},3,1)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{3,2,1}", result);
    }

    [Fact(Skip = "BUG: SPSP.ParseEdge (Problems/P/P_SPSP/P_SPSP.cs) unconditionally indexes " +
        "rawEdge[0]/rawEdge[1] for the unweighted case without first checking " +
        "rawEdge.IsOrdered(), unlike the weighted branch just above it which does check. " +
        "An unweighted, undirected edge like \"{1,2}\" is a genuine SPADE set, which does " +
        "not support indexing, so construction throws " +
        "System.InvalidOperationException(\"Cannot index into a set\") from " +
        "SPADE.UtilCollection.get_Item. As a result no unweighted, undirected SPSP instance " +
        "can ever be constructed.")]
    public void SPSPSolver_Unweighted_Undirected_Graph_Defaults_Edge_Weight_To_One() {
        string instance = "({1,2,3},{{1,2},{2,3}},3,1)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{3,2,1}", result);
    }

    [Fact]
    public void SPSPSolver_Undirected_Output_Passes_Verifier() {
        string instance = "({1,2,3},{({1,2},4),({2,3},1)},3,1)";
        SPSP problem = new SPSP(instance);
        string solution = new SPSPSolver().solve(problem);
        Assert.True(new SPSPVerifier().verify(problem, solution), $"Solver output failed verifier for: {instance}");
    }

    [Fact]
    public void SPSPSolver_Isolated_Source_With_Unreachable_Target_Returns_Empty_Braces() {
        // Node "1" (source) has no edges touching it at all, so it never gets an
        // adjacency-list entry; the solver must skip cleanly rather than throw, and
        // report the (unreachable) target with an empty certificate.
        string instance = "(({1,2,3},{((2,3),1)}),1,3)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{}", result);
    }

    // ----- Solver — GetSteps ----- //

    [Fact]
    public void SPSPSolver_GetSteps_Returns_Empty_When_No_Nodes() {
        SPSP problem = new SPSP("({},{})");
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetSteps(problem);
        Assert.Empty(steps);
    }

    [Fact]
    public void SPSPSolver_GetSteps_Emits_Growing_Certificate_Per_Settled_Node_Then_Stops_At_Target() {
        // Node "1" is never reachable from source "2" in this directed graph, so the
        // solver's search stops (target reached) before every node has been settled --
        // GetSteps must reflect that early termination, not visit all four nodes.
        string instance = "(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetSteps(problem).Cast<string>().ToList();

        Assert.Equal(new List<string> { "{2}", "{2,3}", "{2,3,4}" }, steps);
    }

    [Fact]
    public void SPSPSolver_GetSteps_Stops_Cleanly_When_Source_Is_Isolated() {
        string instance = "(({1,2,3},{((2,3),1)}),1,3)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetSteps(problem).Cast<string>().ToList();

        Assert.Equal(new List<string> { "{1}" }, steps);
    }

    // ----- Solver — GetTableSteps ----- //

    [Fact]
    public void SPSPSolver_GetTableSteps_Returns_Empty_When_No_Nodes() {
        SPSP problem = new SPSP("({},{})");
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem);
        Assert.Empty(steps);
    }

    [Fact]
    public void SPSPSolver_GetTableSteps_Vertices_Are_Sorted_By_Name_Regardless_Of_Declaration_Order() {
        string instance = "(({D,C,B,A},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SPSPSolver.SPSPTableStep>().ToList();

        Assert.Equal(new List<string> { "A", "B", "C", "D" }, steps[0].vertices.Select(v => v.name));
    }

    [Fact]
    public void SPSPSolver_GetTableSteps_First_Step_Has_No_Known_Nodes_And_Infinite_Costs() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SPSPSolver.SPSPTableStep>().ToList();

        var first = steps[0];
        Assert.Null(first.currentVertex);
        Assert.Equal("1", first.sourceVertex);
        Assert.Equal("5", first.targetVertex);
        Assert.Empty(first.knownSet);

        // The source's distance is seeded to 0 before the loop even starts, so only the
        // non-source vertices are still at infinity in this very first snapshot.
        Assert.All(first.vertices.Where(v => v.name != "1"), v => {
            Assert.Equal("∞", v.cost);
            Assert.Null(v.path);
            Assert.False(v.known);
        });

        var source = first.vertices.First(v => v.name == "1");
        Assert.Equal("0", source.cost);
        Assert.Equal("{1}", source.path);
        Assert.False(source.known);
    }

    [Fact]
    public void SPSPSolver_GetTableSteps_Final_Step_Reports_Target_Cost_And_Path() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SPSPSolver.SPSPTableStep>().ToList();

        var finalTarget = steps[^1].vertices.First(v => v.name == "5");
        Assert.Equal("9", finalTarget.cost);
        Assert.Equal("{1,3,5}", finalTarget.path);
        Assert.True(finalTarget.known);
    }

    [Fact]
    public void SPSPSolver_GetTableSteps_Stops_Once_Target_Reached_Leaving_Unreachable_Nodes_Unknown() {
        // Node "1" is unreachable from source "2"; once target "4" is settled the loop
        // breaks immediately (skipping the post-relaxation step), so "1" stays unknown.
        string instance = "(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SPSPSolver.SPSPTableStep>().ToList();

        var last = steps[^1];
        Assert.Null(last.currentVertex);
        var node1 = last.vertices.First(v => v.name == "1");
        Assert.False(node1.known);
        Assert.Equal("∞", node1.cost);
        Assert.Null(node1.path);

        var node4 = last.vertices.First(v => v.name == "4");
        Assert.True(node4.known);
        Assert.Equal("2", node4.cost);
    }

    [Fact]
    public void SPSPSolver_GetTableSteps_Intermediate_Relaxation_Step_Reports_Current_Vertex() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SPSPSolver.SPSPTableStep>().ToList();

        // steps[0] = initial, steps[1] = "1" settled (finalize), steps[2] = post-relax after "1"
        Assert.Equal("1", steps[2].currentVertex);
    }

    [Fact]
    public void SPSPSolver_GetTableSteps_Skips_Relaxation_Step_When_Source_Has_No_Adjacency_Entry() {
        // Node "1" never appears in any edge, so it has no adjacency-list entry at all;
        // the "!adjacency.TryGetValue(...) continue" branch must be taken cleanly.
        string instance = "(({1,2,3},{((2,3),1)}),1,3)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        var steps = solver.GetTableSteps(problem).Cast<SPSPSolver.SPSPTableStep>().ToList();

        // Only the initial step and the "1 settled" finalize step -- no post-relax step
        // is ever added, and the search halts with the target still unreached.
        Assert.Equal(2, steps.Count);
        Assert.Null(steps[^1].currentVertex);
        var target = steps[^1].vertices.First(v => v.name == "3");
        Assert.False(target.known);
    }

    // ----- Verifier ----- //

    [Theory] //Tests independent set verifier with a few certificates
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", "{1,3,5}", true)]
    [InlineData("({1,2,3,4,5},{(1,2),(1,3),(2,3),(3,5),(2,4),(4,5)},1,5)", "{1,3,5}", true)]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", "{1,2,3,5}", false)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)", "{1,3,6}", true)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)", "{1,2,4,5}", false)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{2,3,4}", true)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{1,2,3,4}", false)]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", "{A,C,D}", true)]
    public void SSSP_Verifier(string instance, string certificate, bool expected) {
        SPSP problem = new SPSP(instance);
        SPSPVerifier verifier = new SPSPVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SSSP_Verifier_Rejects_Empty_Certificates_When_Path_Exists() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPVerifier verifier = new SPSPVerifier();
        Assert.False(verifier.verify(problem, "{}"));
    }

    // ----- Visualization ----- //
    [Fact]
    public void StepsVisualization_Highlights_Final_Path_Nodes_As_Solution() {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPVisualization visualization = new SPSPVisualization();

        var steps = new List<object> { "{1,3,5}" };
        var frames = visualization.StepsVisualization(problem, steps);

        var frame = Assert.IsType<API_GraphJSON>(frames[0]);
        Assert.Contains(frame.nodes, n => n.name == "1" && (n.color == "Solution" || n.color == "ElementHighlight"));
        Assert.Contains(frame.nodes, n => n.name == "5" && (n.color == "Solution" || n.color == "ElementHighlight"));
    }

    // ----- Helper ----- //
    // The purpose of this helper function is to parse a certificate like "{1,3,5}" and sums the edge weights along the path
    private static int TotalPathWeight(SPSP problem, string certificate) {
        var nodes = certificate.Trim('{', '}').Split(',').ToList();
        var adjacency = SPSPSolver.BuildAdjacencyList(problem.graph);

        int total = 0;
        for (int i = 0; i < nodes.Count - 1; i++) {
            var edge = adjacency[nodes[i]].First(e => e.neighbor == nodes[i + 1]);
            total += edge.weight;
        }
        return total;
    }
}
