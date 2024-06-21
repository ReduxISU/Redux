using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_ExactCover.Verifiers;
using API.Problems.NPComplete.NPC_ExactCover.Solvers;

using System.Text.Json;
using API.Problems.NPComplete.NPC_ExactCover.ReduceTo.NPC_SUBSETSUM;

namespace API.Problems.NPComplete.NPC_ExactCover;

[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]

#pragma warning disable CS1591
public class ExactCoverGenericController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a default Exact Cover object</summary>

    [ProducesResponseType(typeof(ExactCover), 200)]
    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new ExactCover(), options);
        return jsonString;
    }

    ///<summary>Returns an Exact Cover object created from a given instance </summary>{
    ///<param name="problemInstance" example="{{ (), (1 &amp; 3), (2 &amp; 3), (2 &amp; 4)} : {1,2,3,4} : {(1 &amp; 3), (2 &amp; 4)}}">Exact Cover problem instance string.</param>
    ///<response code="200">Returns ExactCover problem object</response>

    [ProducesResponseType(typeof(ExactCover), 200)]
    [HttpGet("instance")]
    public String getInstance(string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new ExactCover(problemInstance), options);
        return jsonString;

    }

}




[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]
#pragma warning disable CS1591
public class ExactCoverVerifierController : ControllerBase {
#pragma warning restore CS1591


    ///<summary>Returns a info about the Exact Cover generic Verifier </summary>
    ///<response code="200">Returns ExactCoverVerifier Object</response>

    [ProducesResponseType(typeof(ExactCoverVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCoverVerifier verifier = new ExactCoverVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    ///<summary>Verifies if a given certificate is a solution to a given Exact Cover problem</summary>
    ///<param name="certificate" example="{{2,3},{4,1}}">certificate solution to Exact Cover problem.</param>
    ///<param name="problemInstance" example="{{1,2,3},{2,3},{4,1} : {1,2,3,4}}">Exact Cover problem instance string.</param>
    ///<response code="200">Returns a boolean</response>

    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCover problem = new ExactCover(problemInstance);
        ExactCoverVerifier verifier = new ExactCoverVerifier();

        bool response = verifier.verify(problem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]
#pragma warning disable CS1591
public class ExactCoverBruteForceController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns a info about the Exact Cover brute force solver </summary>
    ///<response code="200">Returns ExactCoverBruteForce solver Object</response>

    [ProducesResponseType(typeof(ExactCoverBruteForce), 200)]    
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCoverBruteForce solver = new ExactCoverBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    ///<summary>Returns a solution to a given  Exact Cover problem instance </summary>
    ///<param name="problemInstance" example="{{1,2,3},{2,3},{4,1} : {1,2,3,4}}">Exact Cover problem instance string.</param>
    ///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCover problem = new ExactCover(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]
#pragma warning disable CS1591
public class ExactCoverRecursiveController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns a info about the Exact Cover brute force solver </summary>
    ///<response code="200">Returns ExactCoverBruteForce solver Object</response>

    [ProducesResponseType(typeof(ExactCoverRecursive), 200)]    
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCoverRecursive solver = new ExactCoverRecursive();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    ///<summary>Returns a solution to a given  Exact Cover problem instance </summary>
    ///<param name="problemInstance" example="{{1,2,3},{2,3},{4,1} : {1,2,3,4}}">Exact Cover problem instance string.</param>
    ///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCover problem = new ExactCover(problemInstance);
        string solution = new ExactCoverRecursive().solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]
#pragma warning disable CS1591
public class DancingLinksController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns a info about the Exact Cover brute force solver </summary>
    ///<response code="200">Returns ExactCoverBruteForce solver Object</response>

    [ProducesResponseType(typeof(DancingLinks), 200)]    
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        DancingLinks solver = new DancingLinks();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    ///<summary>Returns a solution to a given  Exact Cover problem instance </summary>
    ///<param name="problemInstance" example="{{1,2,3},{2,3},{4,1} : {1,2,3,4}}">Exact Cover problem instance string.</param>
    ///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCover problem = new ExactCover(problemInstance);
        string solution = new DancingLinks().solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]


#pragma warning disable CS1591

public class KarpExactCoverToSubsetSumController : ControllerBase {
#pragma warning restore CS1591

  
///<summary>Returns a reduction object with info for Graph Coloring to CliqueCover Reduction </summary>
///<response code="200">Returns CliqueCoverReduction object</response>

    [ProducesResponseType(typeof(SubsetSumReduction), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCover defaultExactCover = new ExactCover();
        SubsetSumReduction reduction = new SubsetSumReduction(defaultExactCover);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Graph Coloring to CliqueCover based on the given Graph Coloring instance  </summary>
///<param name="problemInstance" example="{{1,7,12,15} : 28}">Graph Coloring problem instance string.</param>
///<response code="200">Returns Fengs's Graph Coloring to CliqueCover object</response>

    [ProducesResponseType(typeof(SubsetSumReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ExactCover defaultExactCover = new ExactCover(problemInstance);
        SubsetSumReduction reduction = new SubsetSumReduction(defaultExactCover);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

}

