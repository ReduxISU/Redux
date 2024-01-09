
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
using API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;
using API.Problems.NPComplete.NPC_HITTINGSET.ReduceTo.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_ExactCover;


namespace API.Problems.NPComplete.NPC_HITTINGSET;

[ApiController]
[Route("[controller]")]
[Tags("Hitting Set")]

#pragma warning disable CS1591
public class HITTINGSETGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Cut object</summary>

    [ProducesResponseType(typeof(HITTINGSET), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new HITTINGSET(), options);
        return jsonString;
    }

///<summary>Returns a Cut object created from a given instance </summary>
///<param name="problemInstance" example="({1,2,3,4},{{1,3},{2,3,4},{1,4}})">Cut problem instance string.</param>
///<response code="200">Returns CUT problem object</response>

    [ProducesResponseType(typeof(HITTINGSET), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new HITTINGSET(problemInstance), options);
        return jsonString;
    }

//    [ApiExplorerSettings(IgnoreApi = true)]
//    [HttpGet("visualize")]
//    public String getVisualization([FromQuery]string problemInstance) {
//        var options = new JsonSerializerOptions { WriteIndented = true };
//        HITTINGSET aSet = new Cut(problemInstance);
//        CutGraph aGraph = aSet.cutAsGraph;
//        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
//        string jsonString = JsonSerializer.Serialize(apiGraph, options);
//        return jsonString;
//    }
//
//    [ApiExplorerSettings(IgnoreApi = true)]
//    [HttpGet("solvedVisualization")]
//    #pragma warning disable CS1591
//    public String solvedVisualization([FromQuery]string problemInstance, string solution){
//    #pragma warning restore CS1591
//        var options = new JsonSerializerOptions { WriteIndented = true };
//        CUT aSet = new CUT(problemInstance);
//        CutGraph aGraph = aSet.cutAsGraph;
//        Dictionary<KeyValuePair<string,string>, bool> solutionDict = aSet.defaultSolver.getSolutionDict(problemInstance, solution);
//        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,aGraph.getEdgeList);
//
//        for(int i=0;i<apiGraph.links.Count;i++){
//            bool edgeVal = false;
//            KeyValuePair<string, string> edge = new KeyValuePair<string, string>(apiGraph.links[i].source, apiGraph.links[i].target);
//            solutionDict.TryGetValue(edge, out edgeVal);
//            apiGraph.links[i].attribute1 = edgeVal.ToString();
//        }
//
//        List<string> parsedS = solution.TrimEnd().TrimStart().Replace("{","").Replace("}","").Split(',').ToList();
//
//        for (int i = 0; i < apiGraph.nodes.Count; i++) {
//            apiGraph.nodes[i].attribute1 = i.ToString();
//            if(parsedS.IndexOf(apiGraph.nodes[i].name) % 2 == 0) {
//                apiGraph.nodes[i].attribute2 = true.ToString();
//            }
//        }
//
//        string jsonString = JsonSerializer.Serialize(apiGraph, options);
//        return jsonString;
//
//    }
}

[ApiController]
[Route("[controller]")]
[Tags("Hitting Set")]
#pragma warning disable CS1591
public class HittingSetVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Cut Verifier </summary>
///<response code="200">Returns CutVerifier</response>

    [ProducesResponseType(typeof(HittingSetVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        HittingSetVerifier verifier = new HittingSetVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Hitting Set problem</summary>
///<param name="certificate" example="{{2,1},{2,3},{2,4},{5,3},{5,4}}">certificate solution to Hitting Set problem.</param>
///<param name="problemInstance" example="({1,2,3,4},{{1,3},{2,3,4},{1,4}})">Hitting Set problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        HITTINGSET HITTINGSET_PROBLEM = new HITTINGSET(problemInstance);
        HittingSetVerifier verifier = new HittingSetVerifier(); 

        bool response = verifier.verify(HITTINGSET_PROBLEM, certificate);
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
[Tags("Hitting Set")]
#pragma warning disable CS1591
public class HittingSetBruteForceController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the Hitting Set brute force solver </summary>
///<response code="200">Returns CutBruteSolver solver Object</response>

    [ProducesResponseType(typeof(HittingSetBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        HittingSetBruteForce solver = new HittingSetBruteForce(); 

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given Hitting Set problem instance </summary>
///<param name="problemInstance" example="({1,2,3,4},{{1,3},{2,3,4},{1,4}})"> Hitting Set problem instance string.</param>
///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        HITTINGSET problem = new HITTINGSET(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }
}



[ApiController]
[Route("[controller]")]
[Tags("Hitting Set")]
#pragma warning disable CS1591
public class reduceToEXACTCOVERController : ControllerBase {


///<summary>Returns a reduction object with info for reduction to clique </summary>
///<response code="200">Returns sipserReduction Object</response>

    [ProducesResponseType(typeof(ExactCoverReduction), 200)]
    [HttpGet("info")]

    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        HITTINGSET defaultHITTINGSET = new HITTINGSET();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        ExactCoverReduction reduction = new ExactCoverReduction(defaultHITTINGSET);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Independent Set to Clique based on the given Independent Set instance  </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)">Independent Set problem instance string.</param>
///<response code="200">Returns Independent Set to CliqueReduction object</response>

    [ProducesResponseType(typeof(ExactCover), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        HITTINGSET defaultHITTINGSET = new HITTINGSET(problemInstance);
        ExactCoverReduction reduction = new ExactCoverReduction(defaultHITTINGSET);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

}