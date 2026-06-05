using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using System.Text.Json;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_INTPROGRAMMING01;

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


[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class KarpReduceGRAPHCOLORINGController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a reduction object with info for Karps's 3SAT to Graph Coloring reduction </summary>
///<response code="200">Returns KarpReduction object</response>

    [ProducesResponseType(typeof(KarpReduceGRAPHCOLORING), 200)]
    [HttpGet("info")]
    public String getInfo(){

        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3();
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from 3SAT to Graph Coloring based on the given 3SAT instance  </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns Karp's 3SAT to Graph Coloring KarpReduction object</response>

    [ProducesResponseType(typeof(KarpReduceGRAPHCOLORING), 200)]
    [HttpPost("reduce")]
    public String getReduce([FromBody]string problemInstance){
         
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(new SAT3(problemInstance));
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }
    
    #pragma warning disable CS1591
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("mapSolution")]
    public String mapSolution([FromBody]Tools.ApiParameters.MapSolution mapSolution){
        var problemFrom = mapSolution.ProblemFrom;
        var problemTo = mapSolution.ProblemTo;
        var problemFromSolution = mapSolution.ProblemFromSolution;
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        GRAPHCOLORING graphColoring = new GRAPHCOLORING(problemTo);
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(sat3);
        string mappedSolution = reduction.mapSolutions(problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }
    #pragma warning restore CS1591
}

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class KarpIntProgStandardController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a reduction object with info for Karps's 3SAT to 0-1 Integer Programming reduction </summary>
///<response code="200">Returns KarpIntProgStandard object</response>

    [ProducesResponseType(typeof(KarpIntProgStandard), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3();
        KarpIntProgStandard reduction = new KarpIntProgStandard(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from 3SAT to 0-1 Integer Programming based on the given 3SAT instance  </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns Karps's 3SAT to 0-1 Integer Programming KarpIntProgStandard object</response>

    [ProducesResponseType(typeof(KarpIntProgStandard), 200)]
    [HttpPost("reduce")]
    public String getReduce([FromBody]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        KarpIntProgStandard reduction = new KarpIntProgStandard(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a solution vector to the a 0-1 Integer Programming problem, wich has been reduced from 3SAT using Karp's reduction  </summary>
///<param name="mapSolution">MapSolution object with ProblemFrom (3SAT instance), ProblemTo (0-1 Integer Programming instance), and ProblemFromSolution (3SAT solution).</param>
///<response code="200">Returns solution to the reduced 0-1 Integer Programming instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpPost("mapSolution")]
    public String mapSolution([FromBody]Tools.ApiParameters.MapSolution mapSolution){
        var problemFrom = mapSolution.ProblemFrom;
        var problemTo = mapSolution.ProblemTo;
        var problemFromSolution = mapSolution.ProblemFromSolution;
        Console.WriteLine(problemTo);
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        INTPROGRAMMING01 intProg = new INTPROGRAMMING01(problemTo);
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);
        string mappedSolution = reduction.mapSolutions(problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class GareyJohnsonController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a a reduction object with info for Garey and Johnsons's 3SAT to 3 Dimensional Matching reduction </summary>
///<response code="200">Returns GareyJohnson Object</response>

    [ProducesResponseType(typeof(GareyJohnson), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3();
        GareyJohnson reduction = new GareyJohnson(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from 3SAT to 3 Dimensional Matching based on the given 3SAT instance  </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns Garey and Johnson's 3SAT to 3 DImensional Matching GareyJohnson object</response>

    [ProducesResponseType(typeof(GareyJohnson), 200)]
    [HttpPost("reduce")]
    public String getReduce([FromBody]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        GareyJohnson reduction = new GareyJohnson(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a solution set to the 3 Dimensional Matching problem, wich has been reduced from 3SAT using Garey and Johnsons's reduction  </summary>
///<param name="mapSolution">MapSolution object with ProblemFrom (3SAT instance), ProblemTo (3DM instance), and ProblemFromSolution (3SAT solution).</param>
///<response code="200">Returns solution to the reduced 0-1 Integer Programming instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpPost("mapSolution")]
    public String mapSolution([FromBody]Tools.ApiParameters.MapSolution mapSolution){
        var problemFrom = mapSolution.ProblemFrom;
        var problemTo = mapSolution.ProblemTo;
        var problemFromSolution = mapSolution.ProblemFromSolution;
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        DM3 dm3 = new DM3(problemTo);
        GareyJohnson reduction = new GareyJohnson(sat3);
        string mappedSolution = reduction.mapSolutions(problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }
}