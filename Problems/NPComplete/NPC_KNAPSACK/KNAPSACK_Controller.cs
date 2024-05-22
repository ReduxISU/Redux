using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_KNAPSACK;
using System.Text.Json;
using API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;
using API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
using System.Text.Json.Serialization;

namespace API.Problems.NPComplete.NPC_KNAPSACK;

[ApiController]
[Route("[controller]")]
[Tags("Knapsack")]

#pragma warning disable CS1591
public class KNAPSACKGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Knapsack problem object</summary>

    [ProducesResponseType(typeof(KNAPSACK), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new KNAPSACK(), options);
        return jsonString;
    }

///<summary>Returns a Knapsack problem object created from a given instance </summary>
///<param name="problemInstance" example="({(10,60),(20,100),(30,120)},50,220)">Knapsack problem instance string.</param>
///<response code="200">Returns Knapsack problem object</response>

    [ProducesResponseType(typeof(KNAPSACK), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery] string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new KNAPSACK(problemInstance), options);
        return jsonString;
    }



}


[ApiController]
[Route("[controller]")]
[Tags("Knapsack")]

#pragma warning disable CS1591
public class KnapsackVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Knapsack generic Verifier </summary>
///<response code="200">Returns GarrettVerifier object</response>

    [ProducesResponseType(typeof(KnapsackVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric(){
        var options = new JsonSerializerOptions {WriteIndented = true};
        KnapsackVerifier verifier = new KnapsackVerifier();
        string jsonString  = JsonSerializer.Serialize(verifier, options);
        return jsonString; 
    }

///<summary>Verifies if a given certificate is a solution to a given Knapsack problem</summary>
///<param name="certificate" example="{(30:120,20:100):220}">certificate solution to Knapsack problem.</param>
///<param name="problemInstance" example="{{10,20,30},{(10,60),(20,100),(30,120)},50}">Knapsack problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String getInstance([FromQuery]string certificate, [FromQuery] string problemInstance){
        var options = new JsonSerializerOptions {WriteIndented = true};
        KNAPSACK KNAPSACKProblem = new KNAPSACK(problemInstance);
        KnapsackVerifier verifier = new KnapsackVerifier();
        Boolean response = verifier.verify(KNAPSACKProblem, certificate);
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }
}


[ApiController]
[Route("[controller]")]
[Tags("Knapsack")]

#pragma warning disable CS1591
public class KnapsackBruteForceController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about Garrett's Knapsack solver </summary>
///<response code="200">Returns GarrettKnapsackSolver solver bject</response>

    [ProducesResponseType(typeof(KnapsackBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric(){
        var options = new JsonSerializerOptions {WriteIndented = true};
        KnapsackBruteForce solver = new KnapsackBruteForce();
        string jsonString  = JsonSerializer.Serialize(solver, options);
        return jsonString; 
    }

///<summary>Returns a solution to a given Knapsack problem instance </summary>
///<param name="problemInstance" example="{{10,20,30},{(10,60),(20,100),(30,120)},50}">Knapsack problem instance string.</param>
///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solvedInstance([FromQuery]string problemInstance) {
         
        var options = new JsonSerializerOptions { WriteIndented = true };
        KNAPSACK KNAPSACKProblem = new KNAPSACK(problemInstance);
        KnapsackBruteForce solver = new KnapsackBruteForce();
        string solvedInstance = solver.solve(KNAPSACKProblem);
        string jsonString = JsonSerializer.Serialize(solvedInstance, options);
        return jsonString;
    }


}
