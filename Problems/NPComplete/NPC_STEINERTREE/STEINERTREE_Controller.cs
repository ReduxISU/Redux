using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_STEINERTREE;
using API.Problems.NPComplete.NPC_STEINERTREE.Solvers;
using API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;


namespace API.Problems.NPComplete.NPC_STEINERTREE;

[ApiController]
[Route("[controller]")]
[Tags("Steiner Tree")]

#pragma warning disable CS1591
public class STEINERTREEGenericController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a default Steiner Tree object</summary>

    [ProducesResponseType(typeof(STEINERTREE), 200)]
    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new STEINERTREE(), options);
        return jsonString;
    }

    ///<summary>Returns a Steiner Tree object created from a given instance </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}">Steiner Tree problem instance string.</param>
    ///<response code="200">Returns Steiner Tree problem object</response>

    [ProducesResponseType(typeof(STEINERTREE), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new STEINERTREE(problemInstance), options);
        return jsonString;
    }
    ///<summary>Returns a graph object used for dynamic visualization </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Steiner problem instance string.</param>
    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        STEINERTREE aSet = new STEINERTREE(problemInstance);
        SteinerGraph aGraph = aSet.steinerAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

    ///<summary>Returns a graph object used for dynamic solved visualization </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Steiner problem instance string.</param>
    ///<param name="solution" example="{{2,1},{2,3},{2,4},{5,3},{5,4}}">Steiner instance string.</param>

    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
#pragma warning disable CS1591
    public String getSolvedVisualization([FromQuery] string problemInstance, string solution)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        STEINERTREE steiner = new STEINERTREE(problemInstance);
        List<string> solutionListNodes = solution.Replace("{", "").Replace("}", "").Split(",").ToList();
        List<string> solutionListEdges = solution.Replace("{{", "").Replace("}}", "").Split("},{").ToList();
        SteinerGraph hGraph = steiner.steinerAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(hGraph.getNodeList, hGraph.getEdgeList);
        if(solution != "{}") {
        for(int j = 0; j < solutionListNodes.Count; j++)
        {
            for (int i = 0; i < apiGraph.nodes.Count; i++)
            {
                if (solutionListNodes.Contains(apiGraph.nodes[i].name))
                {
                    apiGraph.nodes[i].attribute1 = i.ToString();
                    apiGraph.nodes[i].attribute2 = true.ToString();
                    if(steiner.terminals.Contains(apiGraph.nodes[i].name)) {
                        apiGraph.nodes[i].attribute3 = true.ToString();
                    }
                }
            }
        }

        for(int j = 0; j < solutionListEdges.Count; j++) {
            List<string> edgeValues = solutionListEdges[j].Split(',').ToList();
            string target = edgeValues[1];
            string source = edgeValues[0];
            for (int i = 0; i < apiGraph.links.Count; i++)
            {
                if ((apiGraph.links[i].target == target && apiGraph.links[i].source == source) ||
                (apiGraph.links[i].source == target && apiGraph.links[i].target == source) )
                {
                    apiGraph.links[i].attribute1 = true.ToString();
                }
            }
        }
        }
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }
}

[ApiController]
[Route("[controller]")]
[Tags("Steiner Tree")]
#pragma warning disable CS1591
public class SteinerTreeVerifierController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns information about the Steiner Tree Verifier </summary>
    ///<response code="200">Returns Steiner TreeVerifier</response>

    [ProducesResponseType(typeof(SteinerTreeVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SteinerTreeVerifier verifier = new SteinerTreeVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    ///<summary>Verifies if a given certificate is a solution to a given Steiner Tree problem</summary>
    ///<param name="certificate" example="{1,2,4,5,3,1}">certificate solution to Steiner Tree problem.</param>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}}">Steiner Tree problem instance string.</param>
    ///<response code="200">Returns a boolean</response>

    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery] string certificate, [FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        STEINERTREE STEINERTREE_PROBLEM = new STEINERTREE(problemInstance);
        SteinerTreeVerifier verifier = new SteinerTreeVerifier();

        Boolean response = verifier.verify(STEINERTREE_PROBLEM, certificate);
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
[Tags("Steiner Tree")]
#pragma warning disable CS1591
public class SteinerTreeBruteForceController : ControllerBase
{
#pragma warning restore CS1591


    // Return Generic Solver Class
    ///<summary>Returns information about the Steiner Tree brute force solver </summary>
    ///<response code="200">Returns Steiner TreeBruteSolver solver Object</response>

    [ProducesResponseType(typeof(SteinerTreeBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SteinerTreeBruteForce solver = new SteinerTreeBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
    ///<summary>Returns a solution to a given  Steiner Tree problem instance </summary>
    ///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}"> Steiner Tree problem instance string.</param>
    ///<response code="200">Returns solution string </response>

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        STEINERTREE problem = new STEINERTREE(problemInstance);
        string solution = problem.defaultSolver.solve(problem);

        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}