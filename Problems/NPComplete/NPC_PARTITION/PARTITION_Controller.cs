using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;
using API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;


namespace API.Problems.NPComplete.NPC_PARTITION;

[ApiController]
[Route("[controller]")]
[Tags("Partition")]

#pragma warning disable CS1591
public class PARTITIONGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Partition object</summary>

    [ProducesResponseType(typeof(PARTITION), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new PARTITION(), options);
        return jsonString;
    }

///<summary>Returns a Partition object created from a given instance </summary>
///<param name="problemInstance" example="{1,7,12,15,33,12,11,5,6,9,21,18}">Partition problem instance string.</param>
///<response code="200">Returns Partition problem object</response>

    [ProducesResponseType(typeof(PARTITION), 200)]
    [HttpGet("{instance}")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new PARTITION(problemInstance), options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("Partition")]


#pragma warning disable CS1591

public class KarpPartitionToCutController : ControllerBase {
#pragma warning restore CS1591

  
///<summary>Returns a reduction object with info for Graph Coloring to CliqueCover Reduction </summary>
///<response code="200">Returns CliqueCoverReduction object</response>

    [ProducesResponseType(typeof(WEIGHTEDCUTReduction), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        PARTITION defaultGRAPHCOLORING = new PARTITION();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        WEIGHTEDCUTReduction reduction = new WEIGHTEDCUTReduction(defaultGRAPHCOLORING);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Graph Coloring to CliqueCover based on the given Graph Coloring instance  </summary>
///<param name="problemInstance" example="{{1,7,12,15} : 28}">Graph Coloring problem instance string.</param>
///<response code="200">Returns Fengs's Graph Coloring to CliqueCover object</response>

    [ProducesResponseType(typeof(WEIGHTEDCUTReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        PARTITION defaultGRAPHCOLORING = new PARTITION(problemInstance);
        WEIGHTEDCUTReduction reduction = new WEIGHTEDCUTReduction(defaultGRAPHCOLORING);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Partition")]
#pragma warning disable CS1591
public class PartitionVerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about the Partition Verifier </summary>
///<response code="200">Returns PartitionVerifier</response>

    [ProducesResponseType(typeof(PartitionVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        PartitionVerifier verifier = new PartitionVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Partition problem</summary>
///<param name="certificate" example="{(7,12,33,12,11),(1,15,5,6,9,21,18)}">certificate solution to Partition problem.</param>
///<param name="problemInstance" example="{1,7,12,15,33,12,11,5,6,9,21,18}">Partition problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        PARTITION PARTITION_PROBLEM = new PARTITION(problemInstance);
        PartitionVerifier verifier = new PartitionVerifier();

        Boolean response = verifier.verify(PARTITION_PROBLEM, certificate);
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
[Tags("Partition")]
#pragma warning disable CS1591
public class PartitionBruteForceController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the Partition brute force solver </summary>
///<response code="200">Returns PartitionBruteSolver solver Object</response>

    [ProducesResponseType(typeof(PartitionBruteForce), 200)]
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        PartitionBruteForce solver = new PartitionBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given  Partition problem instance </summary>
///<param name="problemInstance" example="{1,7,12,15,33,12,11,5,6,9,21,18}"> Partition problem instance string.</param>
///<response code="200">Returns solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        PARTITION problem = new PARTITION(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}