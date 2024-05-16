using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_TSP;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.NPComplete.NPC_TSP.Verifiers;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_TSP;

[ApiController]
[Route("[controller]")]
[Tags("TSP")]

#pragma warning disable CS1591
public class TSPGenericController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a default Cut object</summary>

    [ProducesResponseType(typeof(TSP), 200)]
    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new TSP(), options);
        return jsonString;
    }

    ///<summary>Returns a Cut object created from a given instance </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
    ///<response code="200">Returns CUT problem object</response>

    [ProducesResponseType(typeof(TSP), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new TSP(problemInstance), options);
        return jsonString;
    }

    ///<summary>Returns a graph object used for dynamic visualization </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        TSP aSet = new TSP(problemInstance);
        TSPGraph aGraph = aSet.tspAsGraph;
        List<Edge> edgesWithoutWeight = new List<Edge>();
        foreach (var edgeTuple in aGraph.getEdgeList)
        {
            Node n1 = edgeTuple.source;
            Node n2 = edgeTuple.target;
            edgesWithoutWeight.Add(new Edge(n1, n2));
        }
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, edgesWithoutWeight);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

    ///<summary>Returns a graph object used for dynamic solved visualization </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
    ///<param name="solution" example="{{2,1},{2,3},{2,4},{5,3},{5,4}}">Cut instance string.</param>

    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
#pragma warning disable CS1591
    public String solvedVisualization([FromQuery] string problemInstance, string solution)
    {
#pragma warning restore CS1591
        var options = new JsonSerializerOptions { WriteIndented = true };
        TSP aSet = new TSP(problemInstance);
        TSPGraph aGraph = aSet.tspAsGraph;
        List<Edge> edgesWithoutWeight = new List<Edge>();
        foreach (var edgeTuple in aGraph.getEdgeList)
        {
            Node n1 = edgeTuple.source;
            Node n2 = edgeTuple.target;
            edgesWithoutWeight.Add(new Edge(n1, n2));
        }
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, edgesWithoutWeight);

        List<string> parsedS = solution.Replace("{", "").Replace("}", "").Split(',').ToList();

        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            apiGraph.nodes[i].attribute1 = i.ToString();
            if(parsedS.Contains(apiGraph.nodes[i].name))
                apiGraph.nodes[i].attribute2 = true.ToString();
            else 
                apiGraph.nodes[i].attribute2 = false.ToString();
        }

        for (int j = 0; j < apiGraph.links.Count; j++)
        {
            for (int i = 0; i < parsedS.Count - 1; i++)
            {
                if ((parsedS[i] == apiGraph.links[j].source && parsedS[i+1] == apiGraph.links[j].target) || (parsedS[i] == apiGraph.links[j].target && parsedS[i+1] == apiGraph.links[j].source)) {
                    apiGraph.links[j].attribute1 = true.ToString();
                    break;
                }
                else
                    apiGraph.links[j].attribute1 = false.ToString();
            }
        }

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }

}


[ApiController]
[Route("[controller]")]
[Tags("TSP")]
#pragma warning disable CS1591
public class TSPVerifierController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns information about the Cut Verifier </summary>
    ///<response code="200">Returns CutVerifier</response>

    [ProducesResponseType(typeof(TSPVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        TSPVerifier verifier = new TSPVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    ///<summary>Verifies if a given certificate is a solution to a given Cut problem</summary>
    ///<param name="certificate" example="{{2,1},{2,3},{2,4},{5,3},{5,4}}">certificate solution to Cut problem.</param>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
    ///<response code="200">Returns a boolean</response>

    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery] string certificate, [FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        TSP CUT_PROBLEM = new TSP(problemInstance);
        TSPVerifier verifier = new TSPVerifier();

        Boolean response = verifier.verify(CUT_PROBLEM, certificate);
        string responseString;
        if (response)
        {
            responseString = "True";
        }
        else { responseString = "False"; }
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(responseString, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("TSP")]
#pragma warning disable CS1591
public class TSPBruteForceController : ControllerBase
{
#pragma warning restore CS1591


    // Return Generic Solver Class
    ///<summary>Returns information about the Cut brute force solver </summary>
    ///<response code="200">Returns CutBruteSolver solver Object</response>

    [ProducesResponseType(typeof(TSPBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        TSPBruteForce solver = new TSPBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
    ///<summary>Returns a solution to a given  Cut problem instance </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}"> Cut problem instance string.</param>
    ///<response code="200">Returns solution string </response>

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        TSP problem = new TSP(problemInstance);
        string solution = problem.defaultSolver.solve(problem);

        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}