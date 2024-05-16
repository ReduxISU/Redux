using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;

using System.Text.Json;
using System.Text.Json.Serialization;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION;

namespace API.Problems.NPComplete.NPC_SUBSETSUM;

[ApiController]
[Route("[controller]")]
[Tags("Subset Sum")]


#pragma warning disable CS1591

public class SUBSETSUMGenericController : ControllerBase {
#pragma warning restore CS1591


///<summary>Returns a default Subset Sum problem object</summary>

    [ProducesResponseType(typeof(SUBSETSUM), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SUBSETSUM(), options);
        return jsonString;
    }

///<summary>Returns a Subset Sum problem object created from a given instance </summary>
///<param name="problemInstance" example="{{1,7,12,15} : 28}">Subset Sum problem instance string.</param>
///<response code="200">Returns SUBSETSUM problem object</response>

    [ProducesResponseType(typeof(SUBSETSUM), 200)]
    [HttpGet("instance")]
    public String getInstance(string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SUBSETSUM(problemInstance), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Subset Sum")]


#pragma warning disable CS1591

public class FengController : ControllerBase {
#pragma warning restore CS1591

  
///<summary>Returns a reduction object with info for Feng's Subset Sum to Knapsack reduction </summary>
///<response code="200">Returns FengReduction bbject</response>

    [ProducesResponseType(typeof(FengReduction), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM defaultSUBSETSUM = new SUBSETSUM();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        FengReduction reduction = new FengReduction(defaultSUBSETSUM);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Subset Sum to Knapsack based on the given Subset Sum instance  </summary>
///<param name="problemInstance" example="{{1,7,12,15} : 28}">Subset Sum problem instance string.</param>
///<response code="200">Returns Fengs's Subset Sum to Knapsack FengReduction object</response>

    [ProducesResponseType(typeof(FengReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM defaultSUBSETSUM = new SUBSETSUM(problemInstance);
        FengReduction reduction = new FengReduction(defaultSUBSETSUM);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("mapSolution")]
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        Console.WriteLine(problemTo);
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM sSum = new SUBSETSUM(problemFrom);
        PARTITION part = new PARTITION(problemTo);
        PartitionReduction reduction = new PartitionReduction(sSum);
        string mappedSolution = reduction.mapSolutions(sSum,part,problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }
}


[ApiController]
[Route("[controller]")]
[Tags("Subset Sum")]


#pragma warning disable CS1591

public class SubsetSumToPartitionReductionController : ControllerBase {
#pragma warning restore CS1591

  
///<summary>Returns a reduction object with info for Subset Sum to Partition Reduction </summary>
///<response code="200">Returns PartitionReduction object</response>

    [ProducesResponseType(typeof(PartitionReduction), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM defaultSUBSETSUM = new SUBSETSUM();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        PartitionReduction reduction = new PartitionReduction(defaultSUBSETSUM);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Subset Sum to Partition based on the given Subset Sum instance  </summary>
///<param name="problemInstance" example="{{1,7,12,15} : 28}">Subset Sum problem instance string.</param>
///<response code="200">Returns Fengs's Subset Sum to Partition object</response>

    [ProducesResponseType(typeof(PartitionReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM defaultSUBSETSUM = new SUBSETSUM(problemInstance);
        PartitionReduction reduction = new PartitionReduction(defaultSUBSETSUM);
        string jsonString = JsonSerializer.Serialize(reduction, options);
       // Console.WriteLine("reduced form is: "+ jsonString);
        return jsonString;
    }

    ///<summary>Returns a solution set to the 3 Dimensional Matching problem, wich has been reduced from 3SAT using Garey and Johnsons's reduction  </summary>
///<param name="problemFrom" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">3SAT problem instance string.</param>
///<param name="problemTo" example="{a[x1][1],a[x1][2],a[x1][3],a[x2][1],a[x2][2],a[x2][3],a[x3][1],a[x3][2],a[x3][3],g1[1],g1[2],g1[3],g1[4],g1[5],g1[6],s1[1],s1[2],s1[3]}{b[x1][1],b[x1][2],b[x1][3],b[x2][1],b[x2][2],b[x2][3],b[x3][1],b[x3][2],b[x3][3],g2[1],g2[2],g2[3],g2[4],g2[5],g2[6],s2[1],s2[2],s2[3]}{[!x1][1],[x1][1],[!x1][2],[x1][2],[!x1][3],[x1][3],[!x2][1],[x2][1],[!x2][2],[x2][2],[!x2][3],[x2][3],[!x3][1],[x3][1],[!x3][2],[x3][2],[!x3][3],[x3][3]}{a[x1][1],b[x1][1],[!x1][1]}{a[x1][2],b[x1][1],[x1][1]}{g1[1],g2[1],[x1][1]}{g1[1],g2[1],[!x1][1]}{g1[2],g2[2],[x1][1]}{g1[2],g2[2],[!x1][1]}{g1[3],g2[3],[x1][1]}{g1[3],g2[3],[!x1][1]}{g1[4],g2[4],[x1][1]}{g1[4],g2[4],[!x1][1]}{g1[5],g2[5],[x1][1]}{g1[5],g2[5],[!x1][1]}{g1[6],g2[6],[x1][1]}{g1[6],g2[6],[!x1][1]}{a[x1][2],b[x1][2],[!x1][2]}{a[x1][3],b[x1][2],[x1][2]}{g1[1],g2[1],[x1][2]}{g1[1],g2[1],[!x1][2]}{g1[2],g2[2],[x1][2]}{g1[2],g2[2],[!x1][2]}{g1[3],g2[3],[x1][2]}{g1[3],g2[3],[!x1][2]}{g1[4],g2[4],[x1][2]}{g1[4],g2[4],[!x1][2]}{g1[5],g2[5],[x1][2]}{g1[5],g2[5],[!x1][2]}{g1[6],g2[6],[x1][2]}{g1[6],g2[6],[!x1][2]}{a[x1][3],b[x1][3],[!x1][3]}{a[x1][1],b[x1][3],[x1][3]}{g1[1],g2[1],[x1][3]}{g1[1],g2[1],[!x1][3]}{g1[2],g2[2],[x1][3]}{g1[2],g2[2],[!x1][3]}{g1[3],g2[3],[x1][3]}{g1[3],g2[3],[!x1][3]}{g1[4],g2[4],[x1][3]}{g1[4],g2[4],[!x1][3]}{g1[5],g2[5],[x1][3]}{g1[5],g2[5],[!x1][3]}{g1[6],g2[6],[x1][3]}{g1[6],g2[6],[!x1][3]}{a[x2][1],b[x2][1],[!x2][1]}{a[x2][2],b[x2][1],[x2][1]}{g1[1],g2[1],[x2][1]}{g1[1],g2[1],[!x2][1]}{g1[2],g2[2],[x2][1]}{g1[2],g2[2],[!x2][1]}{g1[3],g2[3],[x2][1]}{g1[3],g2[3],[!x2][1]}{g1[4],g2[4],[x2][1]}{g1[4],g2[4],[!x2][1]}{g1[5],g2[5],[x2][1]}{g1[5],g2[5],[!x2][1]}{g1[6],g2[6],[x2][1]}{g1[6],g2[6],[!x2][1]}{a[x2][2],b[x2][2],[!x2][2]}{a[x2][3],b[x2][2],[x2][2]}{g1[1],g2[1],[x2][2]}{g1[1],g2[1],[!x2][2]}{g1[2],g2[2],[x2][2]}{g1[2],g2[2],[!x2][2]}{g1[3],g2[3],[x2][2]}{g1[3],g2[3],[!x2][2]}{g1[4],g2[4],[x2][2]}{g1[4],g2[4],[!x2][2]}{g1[5],g2[5],[x2][2]}{g1[5],g2[5],[!x2][2]}{g1[6],g2[6],[x2][2]}{g1[6],g2[6],[!x2][2]}{a[x2][3],b[x2][3],[!x2][3]}{a[x2][1],b[x2][3],[x2][3]}{g1[1],g2[1],[x2][3]}{g1[1],g2[1],[!x2][3]}{g1[2],g2[2],[x2][3]}{g1[2],g2[2],[!x2][3]}{g1[3],g2[3],[x2][3]}{g1[3],g2[3],[!x2][3]}{g1[4],g2[4],[x2][3]}{g1[4],g2[4],[!x2][3]}{g1[5],g2[5],[x2][3]}{g1[5],g2[5],[!x2][3]}{g1[6],g2[6],[x2][3]}{g1[6],g2[6],[!x2][3]}{a[x3][1],b[x3][1],[!x3][1]}{a[x3][2],b[x3][1],[x3][1]}{g1[1],g2[1],[x3][1]}{g1[1],g2[1],[!x3][1]}{g1[2],g2[2],[x3][1]}{g1[2],g2[2],[!x3][1]}{g1[3],g2[3],[x3][1]}{g1[3],g2[3],[!x3][1]}{g1[4],g2[4],[x3][1]}{g1[4],g2[4],[!x3][1]}{g1[5],g2[5],[x3][1]}{g1[5],g2[5],[!x3][1]}{g1[6],g2[6],[x3][1]}{g1[6],g2[6],[!x3][1]}{a[x3][2],b[x3][2],[!x3][2]}{a[x3][3],b[x3][2],[x3][2]}{g1[1],g2[1],[x3][2]}{g1[1],g2[1],[!x3][2]}{g1[2],g2[2],[x3][2]}{g1[2],g2[2],[!x3][2]}{g1[3],g2[3],[x3][2]}{g1[3],g2[3],[!x3][2]}{g1[4],g2[4],[x3][2]}{g1[4],g2[4],[!x3][2]}{g1[5],g2[5],[x3][2]}{g1[5],g2[5],[!x3][2]}{g1[6],g2[6],[x3][2]}{g1[6],g2[6],[!x3][2]}{a[x3][3],b[x3][3],[!x3][3]}{a[x3][1],b[x3][3],[x3][3]}{g1[1],g2[1],[x3][3]}{g1[1],g2[1],[!x3][3]}{g1[2],g2[2],[x3][3]}{g1[2],g2[2],[!x3][3]}{g1[3],g2[3],[x3][3]}{g1[3],g2[3],[!x3][3]}{g1[4],g2[4],[x3][3]}{g1[4],g2[4],[!x3][3]}{g1[5],g2[5],[x3][3]}{g1[5],g2[5],[!x3][3]}{g1[6],g2[6],[x3][3]}{g1[6],g2[6],[!x3][3]}{s1[1],s2[1],[x1][1]}{s1[1],s2[1],[!x2][1]}{s1[1],s2[1],[x3][1]}{s1[2],s2[2],[!x1][2]}{s1[2],s2[2],[x3][2]}{s1[2],s2[2],[x1][2]}{s1[3],s2[3],[x2][3]}{s1[3],s2[3],[!x3][3]}{s1[3],s2[3],[x1][3]}">3 Dimensional Matching problem instance string reduced from 3SAT instance.</param>
///<param name="problemFromSolution" example="(x1:True)">Solution to 3SAT problem.</param>
///<response code="200">Returns solution to the reduced 0-1 Integer Programming instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("mapSolution")]
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM sSum = new SUBSETSUM(problemFrom);
        PARTITION partition = new PARTITION(problemTo);
        PartitionReduction reduction = new PartitionReduction(sSum);
        string mappedSolution = reduction.mapSolutions(sSum,partition,problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
public class SubSetSumVerifierController : ControllerBase {
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SubsetSumVerifier verifier = new SubsetSumVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    [HttpGet("verify")]
    public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM subsetSum = new SUBSETSUM(problemInstance);
        SubsetSumVerifier verifier = new SubsetSumVerifier();

        bool response = verifier.verify(subsetSum,certificate);

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
public class SubsetSumBruteForceController : ControllerBase {

    // Return Generic Solver Class
    [HttpGet("info")]
    public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SubsetSumBruteForce solver = new SubsetSumBruteForce();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve an instance given a certificate
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) {
        // Implement solver here
        var options = new JsonSerializerOptions { WriteIndented = true };
        SUBSETSUM problem = new SUBSETSUM(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }
    

}

