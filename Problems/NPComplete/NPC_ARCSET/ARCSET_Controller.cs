using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using API.Problems.NPComplete.NPC_ARCSET.Verifiers;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Problems.NPComplete.NPC_ARCSET;

[ApiController]
[Route("[controller]")]
[Tags("Feedback Arc Set")]
#pragma warning disable CS1591
public class ARCSETGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Feedback Arc Set problem object</summary>

    [ProducesResponseType(typeof(ARCSET), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new ARCSET(), options);
        return jsonString;
    }

///<summary>Returns an Arc Set problem object created from a given instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)">Feedback Arc Set problem instance string.</param>
///<response code="200">Returns ARCSET problem Object</response>

    [ProducesResponseType(typeof(ARCSET), 200)]
    [HttpGet("instance")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new ARCSET(problemInstance), options);
        return jsonString;
    }

///<summary>Returns a graph object used for dynamic visualization </summary>
///<param name="problemInstance" example="(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)">Feedback Arc Set problem instance string.</param>
///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET aSet = new ARCSET(problemInstance);
        ArcsetGraph aGraph = aSet.directedGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

///<summary>Returns a graph object used for dynamic solved visualization </summary>
///<param name="problemInstance" example="(({a0,a1,b0,b1,c0,c1,d0,d1,e0,e1},{(a0,a1),(a1,b0),(a1,c0),(a1,e0),(b0,b1),(b1,a0),(b1,e0),(c0,c1),(c1,a0),(c1,d0),(d0,d1),(d1,c0),(e0,e1),(e1,a0),(e1,b0)}),3)">Feedback Arc Set problem instance string.</param>
///<param name="solution" example="{(a0,a1),(b0,b1),(c0,c1)}">Feedback Arc Set solution instance string.</param>

///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    #pragma warning disable CS1591
    public String solvedVisualization([FromQuery]string problemInstance, string solution){
    #pragma warning restore CS1591
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET aSet = new ARCSET(problemInstance);
        ArcsetGraph aGraph = aSet.directedGraph;
        Dictionary<KeyValuePair<string,string>, bool> solutionDict = aSet.defaultSolver.getSolutionDict(problemInstance, solution);
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,aGraph.getEdgeList);

        for(int i=0;i<apiGraph.links.Count;i++){
            bool edgeVal = false;
            KeyValuePair<string, string> edge = new KeyValuePair<string, string>(apiGraph.links[i].source, apiGraph.links[i].target);
            solutionDict.TryGetValue(edge, out edgeVal);
            apiGraph.links[i].attribute1 = edgeVal.ToString();
        }

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }
}

[ApiController]
[Route("[controller]")]
[Tags("Feedback Arc Set")]
#pragma warning disable CS1591
public class AlexArcsetVerifierController : ControllerBase {
#pragma warning restore CS1591


///<summary>Returns information about Alex's Feedback Arc Set verifier </summary>
///<response code="200">Returns Feedback Arc Set verifier object</response>

    [ProducesResponseType(typeof(AlexArcsetVerifier), 200)]
    [HttpGet("info")]
    public String getInstance(){
        var options = new JsonSerializerOptions{WriteIndented = true};
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
        string jsonString = JsonSerializer.Serialize(verifier,options);
        return jsonString;
    }    

///<summary>Verifies if a given certificate is a solution to a given Feedback Arc Set problem</summary>
///<param name="certificate" example="{(2,4)}">certificate solution to Feedback Arc Set problem.</param>
///<param name="problemInstance" example="(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)">Feedback Arc Set problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String getInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET ARCSETProblem = new ARCSET(problemInstance);
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
        Boolean response = verifier.verify(ARCSETProblem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

// Comented out as we're transitioning to desision problem solvers
// The algorithm for this is in NPC_ARCSET/ NPHSolver,and can be reused when we 
// begin implementing NPHard problems

// [ApiController]                    
// [Route("[controller]")]
// public class AlexNaiveSolverController : ControllerBase {

//     [HttpGet("info")]
//     //without params, just returns the solver.
//     public String getDefault(){
        
//         var options = new JsonSerializerOptions { WriteIndented = true };

//         ARCSET ARCSETProblem = new ARCSET();
//         AlexNaiveSolver solver = new AlexNaiveSolver();
        
//         // Send back to API user
//         string jsonString = JsonSerializer.Serialize(solver, options);
//         return jsonString;
//     }

//       [HttpGet("solve")]
//      //With query.
//     public String getInstance([FromQuery]string problemInstance) {
//         var options = new JsonSerializerOptions { WriteIndented = true };
//         ARCSET ARCSETProblem = new ARCSET(problemInstance);
//         AlexNaiveSolver solver = new AlexNaiveSolver();


//         string solvedInstance = solver.solve(ARCSETProblem);
//         //Boolean response = verifier.verify(ARCSETProblem,certificate);
//         // Send back to API user
//         string jsonString = JsonSerializer.Serialize(solvedInstance, options);
//         return jsonString;
//     }

// }

[ApiController]                    
[Route("[controller]")]
[Tags("Feedback Arc Set")]
#pragma warning disable CS1591
public class ArcSetBruteForceController : ControllerBase {
#pragma warning restore CS1591


///<summary>Returns information about the Feedback Arc Set brute force solver </summary>
///<response code="200">Returns ArcSetBruteForce solver object</response>

    [ProducesResponseType(typeof(ArcSetBruteForce), 200)]
    [HttpGet("info")]
    public String getDefault(){
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET ARCSETProblem = new ARCSET();
        ArcSetBruteForce solver = new ArcSetBruteForce();
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

///<summary>Returns a solution to a given Feedback Arc Set problem instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)">Feedback Arc Set problem instance string.</param>
///<response code="200">Returns a string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET ARCSETProblem = new ARCSET(problemInstance);
        ArcSetBruteForce solver = new ArcSetBruteForce();
        string solvedInstance = solver.solve(ARCSETProblem);
        string jsonString = JsonSerializer.Serialize(solvedInstance, options);
        return jsonString;
    }

}



[ApiController]
[Route("[controller]")]
[Tags("Feedback Arc Set")]
#pragma warning disable CS1591
public class ArcsetJsonPayloadController : ControllerBase {

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getInstance([FromQuery]string listType) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET defaultArcset = new ARCSET();
        ArcsetGraph defaultGraph = defaultArcset.directedGraph;
        string jsonString = "";
        List<Edge> edgeList = defaultGraph.getEdgeList;
        List<Node> nodeList = defaultGraph.getNodeList;
        if(listType.Equals("nodes")){

            jsonString = JsonSerializer.Serialize(nodeList,options);
        }
        else if(listType.Equals("edges")){
            jsonString = JsonSerializer.Serialize(edgeList,options);
        }
        else{
            jsonString = JsonSerializer.Serialize("BAD INPUT, choose edges or nodes for listType. You chose: "+listType,options);

        }
        return jsonString;
    }

}
#pragma warning restore CS1591
    
[ApiController]
[Route("[controller]")]
[Tags("Feedback Arc Set")]
#pragma warning disable CS1591
public class ARCSETDevController : ControllerBase {

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getDefault() {
        ARCSET arcTest = new ARCSET();
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
        bool vOut = verifier.verify(arcTest, "(3,2),(4,1)");
        var options = new JsonSerializerOptions { WriteIndented = true };
        string printString2 = JsonSerializer.Serialize(vOut, options);
        return printString2;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("instance")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        
        ARCSET arcset = new ARCSET(problemInstance);
        ArcsetGraph arcGraph = arcset.directedGraph;
        String jsonString = arcGraph.toDotJson();

        return jsonString;
    }
}
#pragma warning restore CS1591


[ApiController]
[Route("[controller]")]
[Tags("Feedback Arc Set")]
#pragma warning disable CS1591
public class ARCSETVisualizerController : ControllerBase {

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getDefault() {
        ARCSET arcProblem = new ARCSET();
        ArcsetGraph arcsetGraph = arcProblem.directedGraph;
        string dotStr = arcsetGraph.toDotJson();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(dotStr, options);
        return jsonString;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getInstance([FromQuery]string problemInstance) {
        ARCSET arcProblem = new ARCSET(problemInstance);
        ArcsetGraph arcsetGraph = arcProblem.directedGraph;
        string dotStr = arcsetGraph.toDotJson();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(dotStr, options);
        return jsonString;
    }
}
#pragma warning restore CS1591



    