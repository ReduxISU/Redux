using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_VertexCover;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Interfaces.Graphs.GraphParser;


namespace API.Problems.NPComplete.NPC_CLIQUE;

[ApiController]
[Route("[controller]")]
[Tags("Clique")]
#pragma warning disable CS1591
public class CLIQUEGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Clique problem object</summary>

    [ProducesResponseType(typeof(CLIQUE), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new CLIQUE(), options);
        return jsonString;
    }

///<summary>Returns a Clique problem object created from a given instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)">Clique problem instance string.</param>
///<response code="200">Returns CLIQUE problem object</response>

    [ProducesResponseType(typeof(CLIQUE), 200)]
    [HttpGet("instance")]
    public String getDefault([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE devClique = new CLIQUE(problemInstance);
        string jsonString = JsonSerializer.Serialize(devClique, options);
        return jsonString;
    }

#pragma warning disable CS1591
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemInstance);
        CliqueGraph cGraph = clique.cliqueAsGraph;
        API_UndirectedGraphJSON apiFormat = new API_UndirectedGraphJSON(cGraph.getNodeList, cGraph.getEdgeList);

        string jsonString = JsonSerializer.Serialize(apiFormat, options);
        return jsonString;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    public String getSolvedVisualization([FromQuery]string problemInstance,string solution) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SipserClique sClique = new SipserClique(problemInstance);
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution); //Note, this is just a convenience string to list function.
        CliqueGraph cGraph = sClique.cliqueAsGraph;
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
[Tags("Clique")]
#pragma warning disable CS1591
public class sipserReduceToVCController : ControllerBase {


///<summary>Returns a reduction object with info for Sipser's Clique to Vertex Cover reduction </summary>
///<response code="200">Returns sipserReduction Object</response>

    [ProducesResponseType(typeof(sipserReduction), 200)]
    [HttpGet("info")]

    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE defaultCLIQUE = new CLIQUE();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        sipserReduction reduction = new sipserReduction(defaultCLIQUE);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Clique to Vertex Cover based on the given Clique instance  </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)">Clique problem instance string.</param>
///<response code="200">Returns Sipser's Clique to Vertex Cover SipserReduction object</response>

    [ProducesResponseType(typeof(SipserReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE defaultCLIQUE = new CLIQUE(problemInstance);
        sipserReduction reduction = new sipserReduction(defaultCLIQUE);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    #pragma warning disable CS1591

    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemInstance);
        CliqueGraph cGraph = clique.cliqueAsGraph;
        API_UndirectedGraphJSON apiGraphFrom = new API_UndirectedGraphJSON(cGraph.getNodeList,cGraph.getEdgeList);
        for(int i=0;i<apiGraphFrom.nodes.Count;i++){
            apiGraphFrom.nodes[i].attribute1 = i.ToString();
        }
        sipserReduction reduction = new sipserReduction(clique);
        VERTEXCOVER reducedVcov = reduction.reductionTo;
        VertexCoverGraph vGraph = reducedVcov.VCAsGraph;
        API_UndirectedGraphJSON apiGraphTo = new API_UndirectedGraphJSON(vGraph.getNodeList, vGraph.getEdgeList);
        API_UndirectedGraphJSON[] apiArr = new API_UndirectedGraphJSON[2];
        apiArr[0] = apiGraphFrom;
        apiArr[1] = apiGraphTo;
        string jsonString = JsonSerializer.Serialize(apiArr, options);
        return jsonString;
    }
    #pragma warning restore CS1591

///<summary>Returns a solution to the a Vertex Cover problem, wich has been reduced from Clique using Sipser's reduction  </summary>
///<param name="problemFrom" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)">Clique problem instance string.</param>
///<param name="problemTo" example="(({1,2,3,4},{{1,3}}),1)">Vertex Cover problem instance string reduced from Clique instance.</param>
///<param name="problemFromSolution" example=" {1,2,4}">Solution to Clique problem.</param>
///<response code="200">Returns solution to the reduced Vertex Cover instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("mapSolution")]
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemFrom);
        VERTEXCOVER vertexCover = new VERTEXCOVER(problemTo);
        sipserReduction reduction = new sipserReduction(clique);
        string mappedSolution = reduction.mapSolutions(clique,vertexCover,problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Clique")]
#pragma warning disable CS1591
public class CLIQUEDevController : ControllerBase
{

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE devClique = new CLIQUE();
        string jsonString = JsonSerializer.Serialize(devClique, options);
        return jsonString;
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("instance")]
    public String getDefault([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE devClique = new CLIQUE(problemInstance);
        GraphParser gParser = new GraphParser();
        List<string> nList = gParser.getNodeList(devClique.cliqueAsGraph.formalString());
        string jsonString = JsonSerializer.Serialize(nList, options);
        return jsonString;
    }


    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemInstance);
        CliqueGraph cGraph = clique.cliqueAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(cGraph.getNodeList, cGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

}
#pragma warning restore CS1591


[ApiController]
[Route("[controller]")]
[Tags("Clique")]
#pragma warning disable CS1591
public class CliqueBruteForceController : ControllerBase
#pragma warning restore CS1591

{

    // Return Generic Solver Class
///<summary>Returns information about the Clique brute force solver </summary>
///<response code="200">Returns CliqueBruteForce solver object</response>

    [ProducesResponseType(typeof(CliqueBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CliqueBruteForce solver = new CliqueBruteForce();
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given Clique problem instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)">Clique problem instance string.</param>
///<response code="200">Returns a solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE problem = new CLIQUE(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Clique")]
#pragma warning disable CS1591
public class CliqueVerifierController : ControllerBase {
    #pragma warning restore CS1591


///<summary>Returns information about the Clique generic Verifier </summary>
///<response code="200">Returns CliqueVerifier Object</response>

    [ProducesResponseType(typeof(CliqueVerifier), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CliqueVerifier verifier = new CliqueVerifier();
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Clique problem</summary>
///<param name="certificate" example="{1,2,4}">certificate solution to Clique problem.</param>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)">Clique problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
        public String verifyInstance([FromQuery]string problemInstance, string certificate){
        string jsonString = String.Empty;
        CLIQUE vClique = new CLIQUE(problemInstance);
        CliqueVerifier verifier = vClique.defaultVerifier;
        bool validClique = verifier.verify(vClique, certificate);
        var options = new JsonSerializerOptions { WriteIndented = true };
        jsonString = JsonSerializer.Serialize(validClique, options);
        return jsonString;
    }
}
