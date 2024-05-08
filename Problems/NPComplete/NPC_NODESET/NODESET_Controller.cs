using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_NODESET.Solvers;
using API.Problems.NPComplete.NPC_NODESET.Verifiers;


namespace API.Problems.NPComplete.NPC_NODESET;

[ApiController]
[Route("[controller]")]
[Tags("Node Set")]

#pragma warning disable CS1591
public class NODESETGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Node Set object</summary>

    [ProducesResponseType(typeof(NODESET), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new NODESET(), options);
        return jsonString;
    }

///<summary>Returns a Node Set object created from a given instance </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Node Set problem instance string.</param>
///<response code="200">Returns NODESET problem object</response>

    [ProducesResponseType(typeof(NODESET), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new NODESET(problemInstance), options);
        return jsonString;
    }
    ///<summary>Returns a graph object used for dynamic visualization </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Cut problem instance string.</param>
///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        NODESET aSet = new NODESET(problemInstance);
        NodeSetGraph aGraph = aSet.nodeSetAsGraph;
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
        NODESET aSet = new NODESET(problemInstance);
        NodeSetGraph aGraph = aSet.nodeSetAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,aGraph.getEdgeList);

        List<string> solutionList = solution.Replace("{","").Replace("}","").Split(',').ToList();

        for (int i = 0; i < apiGraph.nodes.Count; i++) 
            if(solutionList.Contains(apiGraph.nodes[i].name))
                apiGraph.nodes[i].attribute1 = true.ToString();
        
        for(int i=0;i<apiGraph.links.Count;i++)
            if(solutionList.Contains(apiGraph.links[i].target) || solutionList.Contains(apiGraph.links[i].source))
                apiGraph.links[i].attribute1 = true.ToString();

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }

}


[ApiController]
[Route("[controller]")]
[Tags("Node Set")]
#pragma warning disable CS1591
public class NodeSetVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Node Set Verifier </summary>
///<response code="200">Returns NodeSetVerifier</response>

    [ProducesResponseType(typeof(NodeSetVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        NodeSetVerifier verifier = new NodeSetVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Node Set problem</summary>
///<param name="certificate" example="{{2,1},{2,3},{2,4},{5,3},{5,4}}">certificate solution to Node Set problem.</param>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}">Node Set problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        NODESET NODESET_PROBLEM = new NODESET(problemInstance);
        NodeSetVerifier verifier = new NodeSetVerifier();

        Boolean response = verifier.verify(NODESET_PROBLEM, certificate);
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
[Tags("Node Set")]
#pragma warning disable CS1591
public class NodeSetBruteForceController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the Node Set brute force solver </summary>
///<response code="200">Returns Node SetBruteSolver solver Object</response>

    [ProducesResponseType(typeof(NodeSetBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        NodeSetBruteForce solver = new NodeSetBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given  Node Set problem instance </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}},5}"> Node Set problem instance string.</param>
///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        NODESET problem = new NODESET(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}