using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Verifiers;


namespace API.Problems.NPComplete.NPC_CLIQUECOVER;

[ApiController]
[Route("[controller]")]
[Tags("CliqueCover")]

#pragma warning disable CS1591
public class CLIQUECOVERGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default CliqueCover object</summary>

    [ProducesResponseType(typeof(CLIQUECOVER), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new CLIQUECOVER(), options);
        return jsonString;
    }

///<summary>Returns a CliqueCover object created from a given instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)">CliqueCover problem instance string.</param>
///<response code="200">Returns CliqueCover problem object</response>

    [ProducesResponseType(typeof(CLIQUECOVER), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new CLIQUECOVER(problemInstance), options);
        return jsonString;
    }

///<summary>Returns a graph object used for dynamic visualization </summary>
///<param name="problemInstance" example="(({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}),5)">CliqueCover problem instance string.</param>
///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUECOVER aSet = new CLIQUECOVER(problemInstance);
        CliqueCoverGraph aGraph = aSet.cliqueCoverAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

///<summary>Returns a graph object used for dynamic solved visualization </summary>
///<param name="problemInstance" example="(({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}),5)">CliqueCover problem instance string.</param>
///<param name="solution" example="{{4,5},{1,2,3}}">CliqueCover instance string.</param>

///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    #pragma warning disable CS1591
    public String getSolvedVisualization([FromQuery]string problemInstance,string solution) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUECOVER sClique = new CLIQUECOVER(problemInstance);
        List<string> solutionList = solution.Replace("{{","").Replace("}}","").Split("},{").ToList();
        CliqueCoverGraph cGraph = sClique.cliqueCoverAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(cGraph.getNodeList,cGraph.getEdgeList);
        for(int i=0;i<apiGraph.nodes.Count;i++){
            int number = 0;
            apiGraph.nodes[i].attribute1 = i.ToString();
            foreach(var j in solutionList) {

            if(j.Split(',').Contains(apiGraph.nodes[i].name)){ //we set the nodes as either having a true or false flag which will indicate to the frontend whether to highlight.
                apiGraph.nodes[i].attribute2 = number.ToString(); 
            }

            number += 1;
            number = number % 8;    
        
            }
        }

        for (int i = 0; i < apiGraph.links.Count; i++)
        {
            int number = 0;
            foreach (var j in solutionList)
            {

                foreach (var source in j.Split(','))
                {
                    foreach (var target in j.Split(','))
                    {
                        if (apiGraph.links[i].source == source && apiGraph.links[i].target == target)
                        {
                            apiGraph.links[i].attribute1 = number.ToString();
                        }
                    }
                }

                number += 1;
                number = number % 8;
            }
        }
    
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
        // Console.WriteLine(sClique.clusterNodes.Count.ToString());
        
    }
}


[ApiController]
[Route("[controller]")]
[Tags("CliqueCover")]
#pragma warning disable CS1591
public class CliqueCoverVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the CliqueCover Verifier </summary>
///<response code="200">Returns CliqueCoverVerifier</response>

    [ProducesResponseType(typeof(CliqueCoverVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CliqueCoverVerifier verifier = new CliqueCoverVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given CliqueCover problem</summary>
///<param name="certificate" example="{{2,3,4},{1}}">certificate solution to CliqueCover problem.</param>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)">CliqueCover problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUECOVER CLIQUECOVER_PROBLEM = new CLIQUECOVER(problemInstance);
        CliqueCoverVerifier verifier = new CliqueCoverVerifier();

        Boolean response = verifier.verify(CLIQUECOVER_PROBLEM, certificate);
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
[Tags("CliqueCover")]
#pragma warning disable CS1591
public class CliqueCoverBruteForceController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the CliqueCover brute force solver </summary>
///<response code="200">Returns CliqueCoverBruteSolver solver Object</response>

    [ProducesResponseType(typeof(CliqueCoverBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CliqueCoverBruteForce solver = new CliqueCoverBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given  CliqueCover problem instance </summary>
///<param name="problemInstance" example="(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)"> CliqueCover problem instance string.</param>
///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUECOVER problem = new CLIQUECOVER(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}