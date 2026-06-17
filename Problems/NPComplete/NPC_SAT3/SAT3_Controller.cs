using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_SAT3;


[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class SipserReduceToCliqueStandardController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a reduction object with info for Sipser's 3SAT to Clique reduction </summary>
///<response code="200">Returns SipserReduction object</response>

    [ProducesResponseType(typeof(SipserReduceToCliqueStandard), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3();
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from 3SAT to Clique based on the given 3SAT instance  </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns Sipser's 3SAT to Clique SipserReduction object</response>

    [ProducesResponseType(typeof(SipserReduceToCliqueStandard), 200)]
    [HttpPost("reduce")]
    public String getReduce([FromBody]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }



///<summary>Returns a solution to the a Clique problem, wich has been reduced from 3SAT using Sipser's reduction  </summary>
///<param name="mapSolution">MapSolution object with ProblemFrom (3SAT instance), ProblemTo (Clique instance), and ProblemFromSolution (3SAT solution).</param>
///<response code="200">Returns solution to the reduced Clique instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpPost("mapSolution")]
    public String mapSolution([FromBody]Tools.ApiParameters.MapSolution mapSolution){
        var problemFrom = mapSolution.ProblemFrom;
        var problemTo = mapSolution.ProblemTo;
        var problemFromSolution = mapSolution.ProblemFromSolution;
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        SipserClique clique = new SipserClique(problemTo);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        string mappedSolution = reduction.mapSolutions(problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }

///<summary>Returns a solution to the a 3SAT problem, based on a Sipser's redution Clique solution. </summary>
///<param name="mapSolution">MapSolution object with ProblemFrom (3SAT instance), ProblemTo (Clique instance), and ProblemFromSolution (Clique solution).</param>
///<response code="200">Returns solution to the reduced Clique instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpPost("reverseMappedSolution")]
    public String reverseMappedSolution([FromBody]Tools.ApiParameters.MapSolution mapSolution){
        var problemFrom = mapSolution.ProblemFrom;
        var problemTo = mapSolution.ProblemTo;
        var problemToSolution = mapSolution.ProblemFromSolution;
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        SipserClique clique = new SipserClique(problemTo);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        string mappedSolution = reduction.reverseMapSolutions(sat3,clique,problemToSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }

}