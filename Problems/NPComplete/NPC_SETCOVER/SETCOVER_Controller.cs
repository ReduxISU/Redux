using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_SETCOVER.Verifiers;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Problems.NPComplete.NPC_SETCOVER;

[ApiController]
[Route("[controller]")]
[Tags("Set Cover")]

#pragma warning disable CS1591
public class SETCOVERGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Set Cover object</summary>

    [ProducesResponseType(typeof(SETCOVER), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SETCOVER(), options);
        return jsonString;
    }

///<summary>Returns Set Cover object created from a given instance </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3}">Set Cover problem instance string.</param>
///<response code="200">Returns Set Cover problem object</response>

    [ProducesResponseType(typeof(SETCOVER), 200)]
    [HttpGet("instance")]
    public String getInstance(string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SETCOVER(problemInstance), options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Set Cover")]

#pragma warning disable CS1591
public class SetCoverVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a info about the Set Cover verifier </summary>
///<response code="200">Returns Set Cover Verifier object</response>

    [ProducesResponseType(typeof(SetCoverVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SetCoverVerifier verifier = new SetCoverVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Set Cover problem</summary>
///<param name="certificate" example="{{1,2,3},{4,5}}">certificate solution to Set Cover problem.</param>
///<param name="problemInstance" example="{{1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3}">Set Cover problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SETCOVER SETCOVER_Problem = new SETCOVER(problemInstance);
        SetCoverVerifier verifier = new SetCoverVerifier();

        bool response = verifier.verify(SETCOVER_Problem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Set Cover")]
#pragma warning disable CS1591
public class SetCoverBruteForceController : ControllerBase {
#pragma warning restore CS1591

      // Return Generic Solver Class
///<summary>Returns information about the Set Cover brute force solver </summary>
///<response code="200">Returns SetCoverBruteForce solver object</response>

    [ProducesResponseType(typeof(SetCoverBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SetCoverBruteForce solver = new SetCoverBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

  // Solve a instance given a certificate
///<summary>Returns a solution to a given Set Cover problem instance </summary>
///<param name="problemInstance" example="{{1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3}">Set Cover problem instance string.</param>
///<response code="200">Returns a solution string </response>

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        SETCOVER problem = new SETCOVER(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}
    





