using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;
using API.Interfaces.Graphs.GraphParser;


namespace API.Problems.NPComplete.NPC_JOBSEQ;

[ApiController]
[Route("[controller]")]
[Tags("Job Sequencing")]
#pragma warning disable CS1591
public class JOBSEQGenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default Job Sequencing problem object</summary>

    [ProducesResponseType(typeof(JOBSEQ), 200)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new JOBSEQ(), options);
        return jsonString;
    }

///<summary>Returns a Job Sequencing problem object created from a given instance </summary>
///<param name="problemInstance" example="((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)">Clique problem instance string.</param>
///<response code="200">Returns CLIQUE problem object</response>

    [ProducesResponseType(typeof(JOBSEQ), 200)]
    [HttpGet("instance")]
    public String getDefault([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JOBSEQ devIndependentSet = new JOBSEQ(problemInstance);
        string jsonString = JsonSerializer.Serialize(devIndependentSet, options);
        return jsonString;
    }

#pragma warning disable CS1591
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JOBSEQ jobseq = new JOBSEQ(problemInstance);
        throw new NotImplementedException();
        //string jsonString = JsonSerializer.Serialize(apiFormat, options);
        //return jsonString;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    public String getSolvedVisualization([FromQuery]string problemInstance,string solution) {
        throw new NotImplementedException();
       // List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution); //Note, this is just a convenience string to list function.
       // JOBSEQ independentSet = new JOBSEQ(problemInstance);
       // IndependentSetGraph cGraph = independentSet.independentSetAsGraph;
       // API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(cGraph.getNodeList,cGraph.getEdgeList);
       // for(int i=0;i<apiGraph.nodes.Count;i++){
       //     apiGraph.nodes[i].attribute1 = i.ToString();
       //     if(solutionList.Contains(apiGraph.nodes[i].name)){ //we set the nodes as either having a true or false flag which will indicate to the frontend whether to highlight.
       //         apiGraph.nodes[i].attribute2 = true.ToString(); 
       //     }
       //     else{apiGraph.nodes[i].attribute2 = false.ToString();}
       // }
       // string jsonString = JsonSerializer.Serialize(apiGraph, options);
       // return jsonString;
    }
#pragma warning restore CS1591

}


//[ApiController]
//[Route("[controller]")]
//[Tags("Job Sequencing")]
//#pragma warning disable CS1591
//public class reduceToCLIQUEController : ControllerBase {
//
//
/////<summary>Returns a reduction object with info for reduction to clique </summary>
/////<response code="200">Returns sipserReduction Object</response>
//
//    [ProducesResponseType(typeof(CliqueReduction), 200)]
//    [HttpGet("info")]
//
//    public String getInfo() {
//        var options = new JsonSerializerOptions { WriteIndented = true };
//        JOBSEQ defaultJOBSEQ = new JOBSEQ();
//        //SipserReduction reduction = new SipserReduction(defaultSAT3);
//        CliqueReduction reduction = new CliqueReduction(defaultJOBSEQ);
//        string jsonString = JsonSerializer.Serialize(reduction, options);
//        return jsonString;
//    }
//
/////<summary>Returns a reduction from Job Sequencing to Clique based on the given Job Sequencing instance  </summary>
/////<param name="problemInstance" example="((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)">Job Sequencing problem instance string.</param>
/////<response code="200">Returns Job Sequencing to CliqueReduction object</response>
//
//    [ProducesResponseType(typeof(CliqueReduction), 200)]
//    [HttpGet("reduce")]
//    public String getReduce([FromQuery]string problemInstance) {
//        var options = new JsonSerializerOptions { WriteIndented = true };
//        JOBSEQ defaultJOBSEQ = new JOBSEQ(problemInstance);
//        CliqueReduction reduction = new CliqueReduction(defaultJOBSEQ);
//        string jsonString = JsonSerializer.Serialize(reduction, options);
//        return jsonString;
//    }
//
//}

[ApiController]
[Route("[controller]")]
[Tags("JobSeq")]
#pragma warning disable CS1591
public class JobSeqBruteForceController : ControllerBase
#pragma warning restore CS1591

{

    // Return Generic Solver Class
///<summary>Returns information about the Job Sequencing brute force solver </summary>
///<response code="200">Returns JobSeqBruteForce solver object</response>

    [ProducesResponseType(typeof(JOBSEQ), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JobSeqBruteForce solver = new JobSeqBruteForce();
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given Job Sequencing problem instance </summary>
///<param name="problemInstance" example="((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)">Job Sequencing problem instance string.</param>
///<response code="200">Returns a solution string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JOBSEQ problem = new JOBSEQ(problemInstance);
        string solution = problem.defaultSolver.solve(problem);
        string jsonString = JsonSerializer.Serialize(solution, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("Job Sequencing")]
#pragma warning disable CS1591
public class JobSeqVerifierController : ControllerBase {
    #pragma warning restore CS1591


///<summary>Returns information about the Job Sequencing generic Verifier </summary>
///<response code="200">Returns IndependentSetVerifier Object</response>

    [ProducesResponseType(typeof(JobSeqVerifier), 200)]
    [HttpGet("info")]
   public String getGeneric() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JobSeqVerifier verifier = new JobSeqVerifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

///<summary>Verifies if a given certificate is a solution to a given Job Sequencing problem</summary>
///<param name="certificate" example="{1,3}">certificate solution to Job Sequencing problem.</param>
///<param name="problemInstance" example="((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)"> Job Sequencing problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
        
        public String solveInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JOBSEQ JOBSEQ_PROBLEM = new JOBSEQ(problemInstance);
        JobSeqVerifier verifier = new JobSeqVerifier();

        Boolean response = verifier.verify(JOBSEQ_PROBLEM, certificate);
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
