using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_DIRHAMILTONIAN;
using API.Problems.NPComplete.NPC_DIRHAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_DIRHAMILTONIAN.Verifiers;


namespace API.Problems.NPComplete.NPC_DIRHAMILTONIAN;

[ApiController]
[Route("[controller]")]
[Tags("Directed Hamiltonian")]

#pragma warning disable CS1591
public class DIRHAMILTONIANGenericController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a default Hamiltonian object</summary>

    [ProducesResponseType(typeof(DIRHAMILTONIAN), 200)]
    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new DIRHAMILTONIAN(), options);
        return jsonString;
    }

    ///<summary>Returns a Hamiltonian object created from a given instance </summary>
    ///<param name="problemInstance" example="({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})">Hamiltonian problem instance string.</param>
    ///<response code="200">Returns Hamiltonian problem object</response>

    [ProducesResponseType(typeof(DIRHAMILTONIAN), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new DIRHAMILTONIAN(problemInstance), options);
        return jsonString;
    }
    ///<summary>Returns a graph object used for dynamic visualization </summary>
    ///<param name="problemInstance" example="({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})">Hamiltonian problem instance string.</param>
    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        DIRHAMILTONIAN aSet = new DIRHAMILTONIAN(problemInstance);
        DirectedHamiltonianGraph aGraph = aSet.directedHamiltonianAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

///<summary>Returns a graph object used for dynamic solved visualization </summary>
///<param name="problemInstance" example="({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})">Directed Hamiltonian problem instance string.</param>
///<param name="solution" example="{1,3,5,4,2,1}">Directed Hamiltonian solution instance string.</param>

///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    #pragma warning disable CS1591
    public String solvedVisualization([FromQuery]string problemInstance, string solution){
    #pragma warning restore CS1591
        var options = new JsonSerializerOptions { WriteIndented = true };
        DIRHAMILTONIAN aSet = new DIRHAMILTONIAN(problemInstance);
        DirectedHamiltonianGraph aGraph = aSet.directedHamiltonianAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList,aGraph.getEdgeList);

        List<string> solutionList = solution.Replace("{", "").Replace("}", "").Split(",").ToList();
        int counter = 0;
        for(int j = 0; j < solutionList.Count - 1; j++)
        {
            for (int i = 0; i < apiGraph.nodes.Count; i++)
            {
                if (apiGraph.nodes[i].name == solutionList[j])
                {
                    apiGraph.nodes[i].attribute1 = i.ToString();
                    apiGraph.nodes[i].attribute2 = true.ToString();
                    apiGraph.nodes[i].attribute3 = (counter * 5000 / apiGraph.nodes.Count).ToString();
                }
            }
 
            counter++;

            for (int i = 0; i < apiGraph.links.Count; i++)
            {
                if (apiGraph.links[i].source == solutionList[j] && apiGraph.links[i].target == solutionList[j+1] )
                {
                    apiGraph.links[i].attribute1 = true.ToString();
                    apiGraph.links[i].attribute2 = (counter * 1000).ToString();
                }
            }
        }

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }
}

[ApiController]
[Route("[controller]")]
[Tags("Hamiltonian")]
#pragma warning disable CS1591
public class DirectedHamiltonianVerifierController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns information about the Hamiltonian Verifier </summary>
    ///<response code="200">Returns HamiltonianVerifier</response>

    [ProducesResponseType(typeof(DirectedHamiltonianVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        DirectedHamiltonianVerifier verifier = new DirectedHamiltonianVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    ///<summary>Verifies if a given certificate is a solution to a given Hamiltonian problem</summary>
    ///<param name="certificate" example="{1,3,5,4,2,1}">certificate solution to Hamiltonian problem.</param>
    ///<param name="problemInstance" example="({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})">Hamiltonian problem instance string.</param>
    ///<response code="200">Returns a boolean</response>

    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery] string certificate, [FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        DIRHAMILTONIAN HAMILTONIAN_PROBLEM = new DIRHAMILTONIAN(problemInstance);
        DirectedHamiltonianVerifier verifier = new DirectedHamiltonianVerifier();

        Boolean response = verifier.verify(HAMILTONIAN_PROBLEM, certificate);
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
[Tags("Directed Hamiltonian")]
#pragma warning disable CS1591
public class DirectedHamiltonianBruteForceController : ControllerBase
{
#pragma warning restore CS1591


    // Return Generic Solver Class
    ///<summary>Returns information about the Hamiltonian brute force solver </summary>
    ///<response code="200">Returns HamiltonianBruteSolver solver Object</response>

    [ProducesResponseType(typeof(DirectedHamiltonianBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        DirectedHamiltonianBruteForce solver = new DirectedHamiltonianBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
    ///<summary>Returns a solution to a given  Hamiltonian problem instance </summary>
    ///<param name="problemInstance" example="({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})"> Hamiltonian problem instance string.</param>
    ///<response code="200">Returns solution string </response>

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        DIRHAMILTONIAN problem = new DIRHAMILTONIAN(problemInstance);
        string solution = problem.defaultSolver.solve(problem);

        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}