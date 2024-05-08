using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;
using API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;
namespace API.Problems.NPComplete.NPC_SAT;




[ApiController]
[Route("[controller]")]
[Tags("SAT")]
#pragma warning disable CS1591
public class SATGenericController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a default SAT problem object</summary>

    [ProducesResponseType(typeof(SAT), 200)]
    [HttpGet]

    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SAT(), options);
        return jsonString;
    }

    ///<summary>Returns a SAT problem object created from a given instance </summary>
    ///<param name="problemInstance" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">SAT problem instance string.</param>
    ///<response code="200">Returns SAT problem object</response>

    [ProducesResponseType(typeof(SAT), 200)]
    [HttpGet("instance")]
    public String getInstance(string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SAT(problemInstance), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("SAT")]

#pragma warning disable CS1591
public class SATVerifierController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a info about the SAT generic Verifier </summary>
    ///<response code="200">Returns SATVerifier object</response>

    [ProducesResponseType(typeof(SATVerifier), 200)]
    [HttpGet("info")]
    public String getInfo()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SATVerifier verifier = new SATVerifier();
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    //[HttpGet("{certificate}/{problemInstance}")]
    ///<summary>Verifies if a given certificate is a solution to a given SAT problem</summary>
    ///<param name="certificate" example="(x1:True)">certificate solution to SAT problem.</param>
    ///<param name="problemInstance" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">SAT problem instance string.</param>
    ///<response code="200">Returns a boolean</response>

    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String getInstance([FromQuery] string certificate, [FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT SATProblem = new SAT(problemInstance);
        SATVerifier verifier = new SATVerifier();
        Boolean response = verifier.verify(SATProblem, certificate);
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("SAT")]

#pragma warning disable CS1591
public class SATBruteForceSolverController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a info about the SAT brute force solver </summary>
    ///<response code="200">Returns SATBruteForceSolver solver object</response>

    [ProducesResponseType(typeof(SATBruteForceSolver), 200)]
    [HttpGet("info")]
    public String getInfo()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SATBruteForceSolver solver = new SATBruteForceSolver();
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }
    ///<summary>Returns a solution to a given SAT problem instance </summary>
    ///<param name="problemInstance" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">SAT problem instance string.</param>
    ///<response code="200">Returns solution string </response>

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SATBruteForceSolver solver = new SATBruteForceSolver();
        string testString = solver.Solver(problemInstance);
        string jsonString = JsonSerializer.Serialize(testString, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Exact Cover")]


#pragma warning disable CS1591

public class KarpSATToSAT3Controller : ControllerBase
{
#pragma warning restore CS1591


    ///<summary>Returns a reduction object with info for Graph Coloring to CliqueCover Reduction </summary>
    ///<response code="200">Returns CliqueCoverReduction object</response>

    [ProducesResponseType(typeof(SATReduction), 200)]
    [HttpGet("info")]
    public String getInfo()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT defaultSAT = new SAT();
        SATReduction reduction = new SATReduction(defaultSAT);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

    ///<summary>Returns a reduction from Graph Coloring to CliqueCover based on the given Graph Coloring instance  </summary>
    ///<param name="problemInstance" example="{{1,7,12,15} : 28}">Graph Coloring problem instance string.</param>
    ///<response code="200">Returns Fengs's Graph Coloring to CliqueCover object</response>

    [ProducesResponseType(typeof(SATReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT defaultSAT = new SAT(problemInstance);
        SATReduction reduction = new SATReduction(defaultSAT);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }
}




