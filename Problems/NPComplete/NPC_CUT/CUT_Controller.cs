using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_CUT;
using API.Problems.NPComplete.NPC_CUT.Solvers;
using API.Problems.NPComplete.NPC_CUT.Verifiers;


namespace API.Problems.NPComplete.NPC_CUT;

[ApiController]
[Route("[controller]")]
[Tags("Cut")]

#pragma warning disable CS1591
public class CUTGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Cut object</summary>

    [ProducesResponseType(typeof(CUT), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new CUT(), options);
        return jsonString;
    }

///<summary>Returns a Cut object created from a given instance </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
///<response code="200">Returns CUT problem object</response>

    [ProducesResponseType(typeof(CUT), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new CUT(problemInstance), options);
        return jsonString;
    }

///<summary>Returns a graph object used for dynamic visualization </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CUT aSet = new CUT(problemInstance);
        CutGraph aGraph = aSet.cutAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
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
        CUT aSet = new CUT(problemInstance);
        CutGraph aGraph = aSet.cutAsGraph;
        Dictionary<KeyValuePair<string,string>, bool> solutionDict = aSet.defaultSolver.getSolutionDict(problemInstance, solution);
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,aGraph.getEdgeList);

        for(int i=0;i<apiGraph.links.Count;i++){
            bool edgeVal = false;
            KeyValuePair<string, string> edge = new KeyValuePair<string, string>(apiGraph.links[i].source, apiGraph.links[i].target);
            solutionDict.TryGetValue(edge, out edgeVal);
            apiGraph.links[i].attribute1 = edgeVal.ToString();
        }

        List<string> parsedS = solution.TrimEnd().TrimStart().Replace("{","").Replace("}","").Split(',').ToList();

        for (int i = 0; i < apiGraph.nodes.Count; i++) {
            apiGraph.nodes[i].attribute1 = i.ToString();
            if(parsedS.IndexOf(apiGraph.nodes[i].name) % 2 == 0) {
                apiGraph.nodes[i].attribute2 = true.ToString();
            }
        }

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }
}


[ApiController]
[Route("[controller]")]
[Tags("Cut")]
#pragma warning disable CS1591
public class CutVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Cut Verifier </summary>
///<response code="200">Returns CutVerifier</response>

    [ProducesResponseType(typeof(CutVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CutVerifier verifier = new CutVerifier();

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
        CUT CUT_PROBLEM = new CUT(problemInstance);
        CutVerifier verifier = new CutVerifier();

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
[Tags("Cut")]
#pragma warning disable CS1591
public class CutBruteForceController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the Cut brute force solver </summary>
///<response code="200">Returns CutBruteSolver solver Object</response>

    [ProducesResponseType(typeof(CutBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CutBruteForce solver = new CutBruteForce();

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
        CUT problem = new CUT(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}