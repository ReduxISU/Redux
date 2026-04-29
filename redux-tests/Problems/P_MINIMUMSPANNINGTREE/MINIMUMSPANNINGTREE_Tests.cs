using System;
using Xunit;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_MINIMUMSPANNINGTREE;
using API.Problems.P.P_MINIMUMSPANNINGTREE.Solvers;
using API.Problems.P.P_MINIMUMSPANNINGTREE.Verifiers;
using API.Problems.P.P_MINIMUMSPANNINGTREE.Visualizations;
using Newtonsoft.Json.Linq;

namespace redux_tests;
#pragma warning disable CS1591

public class MINIMUMSPANNINGTREE_Tests
{
    [Fact]
    public void MINIMUMSPANNINGTREE_Default_Instantiation()
    {
        P_MINIMUMSPANNINGTREE problem = new P_MINIMUMSPANNINGTREE();
        Assert.Equal(P_MINIMUMSPANNINGTREE._defaultInstance, problem.instance);
        Assert.Equal(P_MINIMUMSPANNINGTREE._defaultInstance, problem.defaultInstance);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Solver_Simple_Triangle()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new KruskalSolver().solve(problem);
        Assert.Equal("{{1,2},{2,3}}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_PrimSolver_Simple_Triangle()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new PrimSolver().solve(problem);
        Assert.Equal("{{1,2},{2,3}}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Accepts_Optimal_Triangle()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        bool result = new MinimumSpanningTreeVerifier().verify(problem, "{{1,2},{2,3}}");
        Assert.True(result);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Accepts_PrimSolver_Certificate()
    {
        string instance = "({1,2,3,4},{({1,2},1),({2,3},2),({3,4},1),({1,4},2),({1,3},3)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new PrimSolver().solve(problem);
        bool result = new MinimumSpanningTreeVerifier().verify(problem, solution);
        Assert.True(result);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Solver_Unique_MST()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},10)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new KruskalSolver().solve(problem);
        Assert.Equal("{{1,2},{2,3}}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Accepts_Multiple_Valid_MSTs()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},1),({1,3},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        var verifier = new MinimumSpanningTreeVerifier();

        Assert.True(verifier.verify(problem, "{{1,2},{2,3}}"));
        Assert.True(verifier.verify(problem, "{{1,2},{1,3}}"));
        Assert.True(verifier.verify(problem, "{{1,3},{2,3}}"));
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Rejects_Cycle()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},1),({1,3},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        bool result = new MinimumSpanningTreeVerifier().verify(problem, "{{1,2},{2,3},{1,3}}" );
        Assert.False(result);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Rejects_Disconnected_Certificate()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        bool result = new MinimumSpanningTreeVerifier().verify(problem, "{{1,2}}" );
        Assert.False(result);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Rejects_Too_Many_Edges()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},1),({1,3},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        bool result = new MinimumSpanningTreeVerifier().verify(problem, "{{1,2},{2,3},{1,3}}" );
        Assert.False(result);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Verifier_Rejects_Edge_Not_In_Graph()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        bool result = new MinimumSpanningTreeVerifier().verify(problem, "{{1,2},{1,3}}" );
        Assert.False(result);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Solver_Handles_Equal_Weight_Edges()
    {
        string instance = "({1,2,3,4},{({1,2},1),({2,3},1),({3,4},1),({1,4},1),({2,4},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new KruskalSolver().solve(problem);
        Assert.Equal(3, GraphParser.parseUndirectedEdgeListWithStringFunctions(solution).Count / 2);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_PrimSolver_Handles_Equal_Weight_Edges()
    {
        string instance = "({1,2,3,4},{({1,2},1),({2,3},1),({3,4},1),({1,4},1),({2,4},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new PrimSolver().solve(problem);
        Assert.Equal(3, GraphParser.parseUndirectedEdgeListWithStringFunctions(solution).Count / 2);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Solver_Supports_Single_Vertex()
    {
        string instance = "({1},{})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new KruskalSolver().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_PrimSolver_Supports_Single_Vertex()
    {
        string instance = "({1},{})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new PrimSolver().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Solver_Returns_Empty_For_Disconnected_Graph()
    {
        string instance = "({1,2,3},{({1,2},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new KruskalSolver().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_PrimSolver_Returns_Empty_For_Disconnected_Graph()
    {
        string instance = "({1,2,3},{({1,2},1)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);
        string solution = new PrimSolver().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_DefaultVisualization_Is_MSTVisualization()
    {
        var problem = new P_MINIMUMSPANNINGTREE();
        Assert.IsType<MinimumSpanningTreeVisualization>(problem.defaultVisualization);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_Visualization_Highlights_Solution_Edges_And_Nodes()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        var problem = new P_MINIMUMSPANNINGTREE(instance);

        API_GraphJSON graph = (API_GraphJSON)new MinimumSpanningTreeVisualization()
            .SolvedVisualization(problem, "{{1,2},{2,3}}");

        Assert.Equal("Solution", graph.nodes.Single(n => n.name == "1").color);
        Assert.Equal("Solution", graph.nodes.Single(n => n.name == "2").color);
        Assert.Equal("Solution", graph.nodes.Single(n => n.name == "3").color);
        Assert.Equal("Solution", graph.links.Single(l => l.source == "1" && l.target == "2").color);
        Assert.Equal("Solution", graph.links.Single(l => l.source == "2" && l.target == "3").color);
        Assert.Equal("Background", graph.links.Single(l => l.source == "1" && l.target == "3").color);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_AllProblemsRefactor_Can_Filter_P_Problems()
    {
        string response = new ALL_ProblemsRefactorController().getDefault("P");
        Assert.Contains("MINIMUMSPANNINGTREE", response);
    }

    [Fact]
    public void MINIMUMSPANNINGTREE_ProblemInfo_Accepts_Unprefixed_Name_And_Reports_P_Type()
    {
        string response = new ProblemProvider().info("MINIMUMSPANNINGTREE");
        JObject json = JObject.Parse(response);

        Assert.Equal("P", json["problemType"]?.ToString());
        Assert.Equal(P_MINIMUMSPANNINGTREE._defaultInstance, json["defaultInstance"]?.ToString());
    }
}
