using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

using System.Collections;


namespace API.Problems.NPComplete.NPC_VERTEXCOVER;


[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
[Tags("Vertex Cover")]
#pragma warning disable CS1591

public class testController : ControllerBase {

    [HttpGet]

    public String test() {


        VERTEXCOVER testObj = new VERTEXCOVER();

        if (testObj.instance == null) {
            return testObj.defaultInstance;
        }
        else {
            return "REALLY? API!";
        }
    }
}
#pragma warning restore CS1591



[ApiController]
[Route("[controller]")]
[Tags("Vertex Cover")]
#pragma warning disable CS1591
public class VERTEXCOVERGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Vertex Cover object</summary>

    [ProducesResponseType(typeof(VERTEXCOVER), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        // VERTEXCOVER v = new VERTEXCOVER();
        // Console.Write(v.defaultSolver.Solve("{{a,b,c,d,e,f,g} : {(a,b) & (a,c) & (c,d) & (c,e) & (d,f) & (e,f) & (e,g)}}"));
        string jsonString = JsonSerializer.Serialize(new VERTEXCOVER(), options);
        return jsonString;
    }
    
///<summary>Returns a Vertex Cover object created from a given instance </summary>
///<param name="problemInstance" example="(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)">Vertex Cover problem instance string.</param>
///<response code="200">Returns VERTEXCOVER problem object</response>

    [ProducesResponseType(typeof(VERTEXCOVER), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance,string kArgument="") {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new VERTEXCOVER(problemInstance), options);
        return jsonString;
    }

#pragma warning disable CS1591

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        VERTEXCOVER vCov = new VERTEXCOVER(problemInstance);
        VertexCoverGraph vGraph = vCov.VCAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(vGraph.getNodeList, vGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    public String solvedVisualization([FromQuery]string problemInstance, string solution){

        var options = new JsonSerializerOptions { WriteIndented = true };
        VERTEXCOVER vCover = new VERTEXCOVER(problemInstance);
        VertexCoverGraph vGraph = vCover.VCAsGraph;
        Dictionary<string, bool> solutionDict = vCover.defaultSolver.getSolutionDict(problemInstance, solution);
        
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(vGraph.getNodeList,vGraph.getEdgeList);
        for(int i=0;i<apiGraph.nodes.Count;i++){
            apiGraph.nodes[i].attribute1 = i.ToString();
            bool nodeVal = false;
            solutionDict.TryGetValue(apiGraph.nodes[i].name, out nodeVal);
            apiGraph.nodes[i].attribute2 = nodeVal.ToString();
        }

        
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }
    #pragma warning restore CS1591


}

[ApiController]
[Route("[controller]")]
[Tags("Vertex Cover")]
#pragma warning disable CS1591
public class VCVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Vertex Cover generic verifier </summary>
///<response code="200">Returns VCVerifierJanita verifier object</response>

    [ProducesResponseType(typeof(VCVerifierJanita), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        VCVerifierJanita verifier = new VCVerifierJanita();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Vertex Cover problem </summary>
///<param name="certificate" example="{a,b,c}">certificate solution to Vertex Cover problem.</param>
///<param name="problemInstance" example="(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)">Vertex Cover problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String verifyInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        VERTEXCOVER VCProblem = new VERTEXCOVER(problemInstance);
        VCVerifierJanita verifier = new VCVerifierJanita();

        Boolean response = verifier.Verify(VCProblem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
[Tags("Vertex Cover")]
#pragma warning disable CS1591

public class testVCInstanceController : ControllerBase {

    [HttpGet]
    public String getSingleInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        
        string returnString = certificate + problemInstance;
        return returnString;
    }
    
}
#pragma warning restore CS1591


[ApiController]
[Route("[controller]")]
[Tags("Vertex Cover")]
#pragma warning disable CS1591
public class VertexCoverBruteForceController : ControllerBase {
#pragma warning restore CS1591


///<summary>Returns information about the Vertex Cover brute force solver </summary>
///<response code="200">Returns VertexCoverBruteForce solver object</response>

    [ProducesResponseType(typeof(VertexCoverBruteForce), 200)]
    [HttpGet("info")]
    public String getInstance() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        VertexCoverBruteForce solver = new VertexCoverBruteForce();
        string jsonString = JsonSerializer.Serialize(solver, options);

     return jsonString;

    }

///<summary>Returns a solution to a given Vertex Cover instance </summary>
///<param name="problemInstance" example="(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)">Vertex Cover problem instance string.</param>
///<response code="200">Returns a solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance){
        var options = new JsonSerializerOptions { WriteIndented = true };
        VERTEXCOVER problem = new VERTEXCOVER(problemInstance);
        string solution = problem.defaultSolver.Solve(problem);
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

#pragma warning disable CS1591
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance, [FromQuery]string solutionString) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        VERTEXCOVER vCov = new VERTEXCOVER(problemInstance);
        VertexCoverGraph vGraph = vCov.VCAsGraph;
        API_UndirectedGraphJSON apiGraph = vGraph.visualizeSolution(solutionString);

        string jsonString = JsonSerializer.Serialize(apiGraph, options);

     return jsonString;

    }
#pragma warning restore CS1591

    

}


[ApiController]
[Route("[controller]")]
[Tags("Vertex Cover")]
#pragma warning disable CS1591
public class LawlerKarpController : ControllerBase {
#pragma warning restore CS1591


///<summary>Returns a reduction object with info for Lawler and Karp's Vertex Cover to Feedback Arc Set reduction </summary>
///<response code="200">Returns LawlerKarp reduction object</response>

    [ProducesResponseType(typeof(LawlerKarp), 200)]
    [HttpGet("info")] // url parameter

      public String getInfo(){
            var options = new JsonSerializerOptions { WriteIndented = true };
            LawlerKarp reduction = new LawlerKarp();
    
            String jsonString = JsonSerializer.Serialize(reduction,options);
            return jsonString;
      }

    
///<summary>Returns a reduction from Vertex Cover to Feedback Arc Set based on the given Vertex Cover instance  </summary>
///<param name="problemInstance" example="({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)">Vertex Cover problem instance string.</param>
///<response code="200">Returns Lawler and Karp's Vertex Cover to Feedback Arc Set reduction object</response>

    [ProducesResponseType(typeof(LawlerKarp), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        //from query is a query parameter

        var options = new JsonSerializerOptions { WriteIndented = true };
        //UndirectedGraph UG = new UndirectedGraph(problemInstance);
        //string reduction = UG.reduction();
        //Boolean response = verifier.verify(ARCSETProblem,certificate);
        // Send back to API user
        VERTEXCOVER vCover = new VERTEXCOVER(problemInstance);
        LawlerKarp reduction = new LawlerKarp(vCover);
       // ARCSET reducedArcset = reduction.reduce();
        //string reducedStr = reducedArcset.instance;

        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;

    }

///<summary>Returns a solution to the a Feedback Arc Set problem, wich has been reduced from Vertex Cover using Lawler and Karp's reduction  </summary>
///<param name="problemFrom" example="({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)">3SAT problem instance string.</param>
///<param name="problemTo" example="(({a0,a1,b0,b1,c0,c1,d0,d1,e0,e1},{(a0,a1),(a1,b0),(a1,c0),(a1,e0),(b0,b1),(b1,a0),(b1,e0),(c0,c1),(c1,a0),(c1,d0),(d0,d1),(d1,c0),(e0,e1),(e1,a0),(e1,b0)}),3)">Feedback Arc Set problem instance string reduced from Vertex Cover instance.</param>
///<param name="problemFromSolution" example="{a,b,c}">Solution to Vertex Cover problem.</param>
///<response code="200">Returns solution to the reduced Feedback Arc Set problem instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("mapSolution")]
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        var options = new JsonSerializerOptions { WriteIndented = true };
        VERTEXCOVER vertexCover = new VERTEXCOVER(problemFrom);
        ARCSET arcset = new ARCSET(problemTo);
        LawlerKarp reduction = new LawlerKarp();
        string mappedSolution = reduction.mapSolutions(vertexCover,arcset,problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }

}

