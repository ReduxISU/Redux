using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.Solvers;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.Verifiers;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.ReduceTo.NPC_CLIQUE;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET;

[ApiController]
[Route("[controller]")]
[Tags("Independent Set")]
#pragma warning disable CS1591
public class INDEPENDENTSETGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Independent set problem object</summary>

    [ProducesResponseType(typeof(INDEPENDENTSET), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new INDEPENDENTSET(), options);
        return jsonString;
    }

///<summary>Returns a Independent Set problem object created from a given instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)">Clique problem instance string.</param>
///<response code="200">Returns CLIQUE problem object</response>

    [ProducesResponseType(typeof(INDEPENDENTSET), 200)]
    [HttpGet("instance")]
    public String getDefault([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        INDEPENDENTSET devIndependentSet = new INDEPENDENTSET(problemInstance);
        string jsonString = JsonSerializer.Serialize(devIndependentSet, options);
        return jsonString;
    }

#pragma warning disable CS1591
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        INDEPENDENTSET independentSet = new INDEPENDENTSET(problemInstance);
        IndependentSetGraph cGraph = independentSet.independentSetAsGraph;
        API_UndirectedGraphJSON apiFormat = new API_UndirectedGraphJSON(cGraph.getNodeList, cGraph.getEdgeList);

        string jsonString = JsonSerializer.Serialize(apiFormat, options);
        return jsonString;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    public String getSolvedVisualization([FromQuery]string problemInstance,string solution) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution); //Note, this is just a convenience string to list function.
        INDEPENDENTSET independentSet = new INDEPENDENTSET(problemInstance);
        IndependentSetGraph cGraph = independentSet.independentSetAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(cGraph.getNodeList,cGraph.getEdgeList);
        for(int i=0;i<apiGraph.nodes.Count;i++){
            apiGraph.nodes[i].attribute1 = i.ToString();
            if(solutionList.Contains(apiGraph.nodes[i].name)){ //we set the nodes as either having a true or false flag which will indicate to the frontend whether to highlight.
                apiGraph.nodes[i].attribute2 = true.ToString(); 
            }
            else{apiGraph.nodes[i].attribute2 = false.ToString();}
        }
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }
#pragma warning restore CS1591

}


[ApiController]
[Route("[controller]")]
[Tags("Independent Set")]
#pragma warning disable CS1591
public class reduceToCLIQUEController : ControllerBase {


///<summary>Returns a reduction object with info for reduction to clique </summary>
///<response code="200">Returns sipserReduction Object</response>

    [ProducesResponseType(typeof(CliqueReduction), 200)]
    [HttpGet("info")]

    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        INDEPENDENTSET defaultINDEPENDENTSET = new INDEPENDENTSET();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        CliqueReduction reduction = new CliqueReduction(defaultINDEPENDENTSET);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Independent Set to Clique based on the given Independent Set instance  </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)">Independent Set problem instance string.</param>
///<response code="200">Returns Independent Set to CliqueReduction object</response>

    [ProducesResponseType(typeof(CliqueReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        INDEPENDENTSET defaultINDEPENDENTSET = new INDEPENDENTSET(problemInstance);
        CliqueReduction reduction = new CliqueReduction(defaultINDEPENDENTSET);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Independent Set")]
#pragma warning disable CS1591
public class IndependentSetBruteForceController : ControllerBase
#pragma warning restore CS1591

{

    // Return Generic Solver Class
///<summary>Returns information about the Independent Set brute force solver </summary>
///<response code="200">Returns IndependentSetBruteForce solver object</response>

    [ProducesResponseType(typeof(IndependentSetBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        IndependentSetBruteForce solver = new IndependentSetBruteForce();
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given Independent Set problem instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)">Independent set problem instance string.</param>
///<response code="200">Returns a solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        INDEPENDENTSET problem = new INDEPENDENTSET(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Independent Set")]
#pragma warning disable CS1591
public class IndependentSetVerifierController : ControllerBase {
    #pragma warning restore CS1591


///<summary>Returns information about the Independent Set generic Verifier </summary>
///<response code="200">Returns IndependentSetVerifier Object</response>

    [ProducesResponseType(typeof(IndependentSetVerifier), 200)]
    [HttpGet("info")]
   public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        IndependentSetVerifier verifier = new IndependentSetVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Independent Set problem</summary>
///<param name="certificate" example="{1,3}">certificate solution to Independent Set problem.</param>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)">Independent problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
        
        public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        INDEPENDENTSET INDEPENDENTSET_PROBLEM = new INDEPENDENTSET(problemInstance);
        IndependentSetVerifier verifier = new IndependentSetVerifier();

        Boolean response = verifier.verify(INDEPENDENTSET_PROBLEM, certificate);
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
