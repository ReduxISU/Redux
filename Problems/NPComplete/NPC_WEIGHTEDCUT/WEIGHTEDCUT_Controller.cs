using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT;

[ApiController]
[Route("[controller]")]
[Tags("Weighted Cut")]

#pragma warning disable CS1591
public class WEIGHTEDCUTGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Cut object</summary>

    [ProducesResponseType(typeof(WEIGHTEDCUT), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new WEIGHTEDCUT(), options);
        return jsonString;
    }

///<summary>Returns a Cut object created from a given instance </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
///<response code="200">Returns CUT problem object</response>

    [ProducesResponseType(typeof(WEIGHTEDCUT), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new WEIGHTEDCUT(problemInstance), options);
        return jsonString;
    }

    ///<summary>Returns a graph object used for dynamic visualization </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        WEIGHTEDCUT aSet = new WEIGHTEDCUT(problemInstance);
        WeightedCutGraph aGraph = aSet.weightedCutAsGraph;
        List<Edge> edgesWithoutWeight = new List<Edge>();
        foreach(var edgeTuple in aGraph.getEdgeList) {
            Node n1 = edgeTuple.source;
            Node n2 = edgeTuple.target;
            edgesWithoutWeight.Add(new Edge(n1,n2));
        }
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,edgesWithoutWeight);
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
    public String solvedVisualization([FromQuery]string problemInstance, string solution){
    #pragma warning restore CS1591
        var options = new JsonSerializerOptions { WriteIndented = true };
        WEIGHTEDCUT aSet = new WEIGHTEDCUT(problemInstance);
        WeightedCutGraph aGraph = aSet.weightedCutAsGraph;
        List<Edge> edgesWithoutWeight = new List<Edge>();
        foreach(var edgeTuple in aGraph.getEdgeList) {
            Node n1 = edgeTuple.source;
            Node n2 = edgeTuple.target;
            edgesWithoutWeight.Add(new Edge(n1,n2));
        }
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,edgesWithoutWeight);

        List<string> parsedS = solution.Replace("{","").Replace("}","").Split(',').ToList();
        Console.WriteLine(solution.Replace("{","").Replace("}",""));
        List<string> setS = new List<string>();

        for (int i = 0; i < apiGraph.nodes.Count; i++) {
            apiGraph.nodes[i].attribute1 = i.ToString();
            bool found = parsedS.Where((value, index) => index % 3 == 0 && value == apiGraph.nodes[i].name).Any();
            if(found) {
                apiGraph.nodes[i].attribute2 = true.ToString();
                setS.Add(apiGraph.nodes[i].name);
            }
        }

        for(int i=0;i<apiGraph.links.Count;i++){
            if(setS.Contains(apiGraph.links[i].source) && setS.Contains(apiGraph.links[i].target)) {
                apiGraph.links[i].attribute1 = true.ToString();
            }
            else if(setS.Contains(apiGraph.links[i].source) || setS.Contains(apiGraph.links[i].target)) {
                apiGraph.links[i].attribute1 = "Cut";
            }
            else {
                apiGraph.links[i].attribute1 = false.ToString();
            }
        }        

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }

}


[ApiController]
[Route("[controller]")]
[Tags("Weighted Cut")]
#pragma warning disable CS1591
public class WeightedCutVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Cut Verifier </summary>
///<response code="200">Returns CutVerifier</response>

    [ProducesResponseType(typeof(WeightedCutVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        WeightedCutVerifier verifier = new WeightedCutVerifier();

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
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        WEIGHTEDCUT CUT_PROBLEM = new WEIGHTEDCUT(problemInstance);
        WeightedCutVerifier verifier = new WeightedCutVerifier();

        Boolean response = verifier.verify(CUT_PROBLEM, certificate);
        string responseString;
        if(response){
            responseString = "True";
        }
        else{responseString = "False";}
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(responseString, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Weighted Cut")]
#pragma warning disable CS1591
public class WeightedCutBruteForceController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the Cut brute force solver </summary>
///<response code="200">Returns CutBruteSolver solver Object</response>

    [ProducesResponseType(typeof(WeightedCutBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        WeightedCutBruteForce solver = new WeightedCutBruteForce();

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
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        WEIGHTEDCUT problem = new WEIGHTEDCUT(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}