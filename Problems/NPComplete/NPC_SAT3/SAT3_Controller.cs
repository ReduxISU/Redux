using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_3DM;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_INTPROGRAMMING01;

namespace API.Problems.NPComplete.NPC_SAT3;

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class SAT3GenericController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a default 3SAT problem object</summary>

    [ProducesResponseType(typeof(SAT3), 200)]
    [HttpGet()]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SAT3(), options);
        return jsonString;
    }

///<summary>Returns a 3SAT problem object created from a given instance </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns SAT3 problem object</response>

    [ProducesResponseType(typeof(SAT3), 200)]
    [HttpGet("instance")]
    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new SAT3(problemInstance), options);
        return jsonString;
    }


}

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class SipserReduceToCliqueStandardController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns a reduction object with info for Sipser's 3SAT to Clique reduction </summary>
///<response code="200">Returns SipserReduction object</response>

    [ProducesResponseType(typeof(SipserReduction), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3();
        SipserReduction reduction = new SipserReduction(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from 3SAT to Clique based on the given 3SAT instance  </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns Sipser's 3SAT to Clique SipserReduction object</response>

    [ProducesResponseType(typeof(SipserReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        SipserReduction reduction = new SipserReduction(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
    #pragma warning disable CS1591
    public String getSolvedVisualization([FromQuery]string problemInstance, string solution) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        Sat3BacktrackingSolver solver = defaultSAT3.defaultSolver;
        SipserReduction reduction = new SipserReduction(defaultSAT3);
        SipserClique reducedClique = reduction.reduce();
        //Turn string into solution dictionary
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution);
        SipserClique sClique = reduction.solutionMappedToClusterNodes(reducedClique,solutionList);
        string jsonString = JsonSerializer.Serialize(sClique.clusterNodes, options);
        return jsonString;
    }
    #pragma warning disable CS1591



///<summary>Returns a solution to the a Clique problem, wich has been reduced from 3SAT using Sipser's reduction  </summary>
///<param name="problemFrom" example="(x1|!x2|x3)&amp;(!x1|!x3|!x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<param name="problemTo" example="{{x1,!x2,x3,!x1,!x3,!x1_1,x2,!x3_1,x1_1},{{x1,!x3},{x1,!x1_1},{x1,x2},{x1,!x3_1},{x1,x1_1},{!x2,!x1},{!x2,!x3},{!x2,!x1_1},{!x2,!x3_1},{!x2,x1_1},{x3,!x1},{x3,!x1_1},{x3,x2},{x3,!x3_1},{x3,x1_1},{!x1,!x2},{!x1,x3},{!x1,x2},{!x1,!x3_1},{!x1,x1_1},{!x3,x1},{!x3,!x2},{!x3,x2},{!x3,!x3_1},{!x3,x1_1},{!x1_1,x1},{!x1_1,!x2},{!x1_1,x3},{!x1_1,x2},{!x1_1,!x3_1},{x2,x1},{x2,x3},{x2,!x1},{x2,!x3},{x2,!x1_1},{!x3_1,x1},{!x3_1,!x2},{!x3_1,x3},{!x3_1,!x1},{!x3_1,!x3},{!x3_1,!x1_1},{x1_1,x1},{x1_1,!x2},{x1_1,x3},{x1_1,!x1},{x1_1,!x3}},3}">Clique problem instance string reduced from 3SAT instance.</param>
///<param name="problemFromSolution" example="(x1:True,x3:False)">Solution to 3SAT problem.</param>
///<response code="200">Returns solution to the reduced Clique instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("mapSolution")]
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        SipserClique clique = new SipserClique(problemTo);
        SipserReduction reduction = new SipserReduction(sat3);
        string mappedSolution = reduction.mapSolutions(sat3,clique,problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }

///<summary>Returns a solution to the a 3SAT problem, based on a Sipser's redution Clique solution. </summary>
///<param name="problemFrom" example="(x1|!x2|x3)&amp;(!x1|!x3|!x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<param name="problemTo" example="{{x1,!x2,x3,!x1,!x3,!x1_1,x2,!x3_1,x1_1},{{x1,!x3},{x1,!x1_1},{x1,x2},{x1,!x3_1},{x1,x1_1},{!x2,!x1},{!x2,!x3},{!x2,!x1_1},{!x2,!x3_1},{!x2,x1_1},{x3,!x1},{x3,!x1_1},{x3,x2},{x3,!x3_1},{x3,x1_1},{!x1,!x2},{!x1,x3},{!x1,x2},{!x1,!x3_1},{!x1,x1_1},{!x3,x1},{!x3,!x2},{!x3,x2},{!x3,!x3_1},{!x3,x1_1},{!x1_1,x1},{!x1_1,!x2},{!x1_1,x3},{!x1_1,x2},{!x1_1,!x3_1},{x2,x1},{x2,x3},{x2,!x1},{x2,!x3},{x2,!x1_1},{!x3_1,x1},{!x3_1,!x2},{!x3_1,x3},{!x3_1,!x1},{!x3_1,!x3},{!x3_1,!x1_1},{x1_1,x1},{x1_1,!x2},{x1_1,x3},{x1_1,!x1},{x1_1,!x3}},3}">Clique problem instance string reduced from 3SAT instance.</param>
///<param name="problemToSolution" example="{x1,!x3,!x3_1}">Solution to 3SAT problem.</param>
///<response code="200">Returns solution to the reduced Clique instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("reverseMappedSolution")]
    public String reverseMappedSolution([FromQuery]string problemFrom, string problemTo, string problemToSolution){
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        SipserClique clique = new SipserClique(problemTo);
        SipserReduction reduction = new SipserReduction(sat3);
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

    [ProducesResponseType(typeof(KarpReduction), 200)]
    [HttpGet("info")]
    public String getInfo(){

        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3();
        KarpReduction reduction = new KarpReduction(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from 3SAT to Graph Coloring based on the given 3SAT instance  </summary>
///<param name="problemInstance" example="(x1|!x2|x3)&amp;(!x1|x3|x1)&amp;(x2|!x3|x1)">3SAT problem instance string.</param>
///<response code="200">Returns Karp's 3SAT to Graph Coloring KarpReduction object</response>

    [ProducesResponseType(typeof(KarpReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance){
         
        KarpReduction reduction = new KarpReduction(new SAT3(problemInstance));
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("mapSolution")]
    #pragma warning disable CS1591
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        GRAPHCOLORING graphColoring = new GRAPHCOLORING(problemTo);
        KarpReduction reduction = new KarpReduction(sat3);
        string mappedSolution = reduction.mapSolutions(sat3,graphColoring,problemFromSolution);
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
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        KarpIntProgStandard reduction = new KarpIntProgStandard(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a solution vector to the a 0-1 Integer Programming problem, wich has been reduced from 3SAT using Karp's reduction  </summary>
///<param name="problemFrom" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">3SAT problem instance string.</param>
///<param name="problemTo" example="( -1 1 -1 ),( 0 0 -1 ),( -1 -1 1 )&lt;=( 0 0 0 )">0-1 Integer Programming problem instance string reduced from 3SAT instance.</param>
///<param name="problemFromSolution" example="(x1:True)">Solution to 3SAT problem.</param>
///<response code="200">Returns solution to the reduced 0-1 Integer Programming instance</response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("mapSolution")]
    public String mapSolution([FromQuery]string problemFrom, string problemTo, string problemFromSolution){
        Console.WriteLine(problemTo);
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 sat3 = new SAT3(problemFrom);
        INTPROGRAMMING01 intProg = new INTPROGRAMMING01(problemTo);
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);
        string mappedSolution = reduction.mapSolutions(sat3,intProg,problemFromSolution);
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
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 defaultSAT3 = new SAT3(problemInstance);
        GareyJohnson reduction = new GareyJohnson(defaultSAT3);
        string jsonString = JsonSerializer.Serialize(reduction, options);
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
        SAT3 sat3 = new SAT3(problemFrom);
        DM3 dm3 = new DM3(problemTo);
        GareyJohnson reduction = new GareyJohnson(sat3);
        string mappedSolution = reduction.mapSolutions(sat3,dm3,problemFromSolution);
        string jsonString = JsonSerializer.Serialize(mappedSolution, options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class SAT3VerifierController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns information about Kadens Simple Verifier </summary>
///<response code="200">Returns SAT3Verifier verifier Object</response>

    [ProducesResponseType(typeof(SAT3Verifier), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3Verifier verifier = new SAT3Verifier();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }

    // Verify a instance given a certificate

///<summary>Verifies if a given certificate is a solution to a given 3SAT problem </summary>
///<param name="certificate" example="(x1:True)">certificate solution to 3SAT problem.</param>
///<param name="problemInstance" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">3SAT problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String getInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 SAT3Problem = new SAT3(problemInstance);
        SAT3Verifier verifier = new SAT3Verifier();

        Boolean response = verifier.verify(SAT3Problem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class Sat3BacktrackingSolverController : ControllerBase {
#pragma warning restore CS1591


    // Return Generic Solver Class
///<summary>Returns information about the 3SAT Backtracking Solver </summary>
///<response code="200">Returns Sat3BacktrackingSolver solver Object</response>

    [ProducesResponseType(typeof(Sat3BacktrackingSolver), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();

        // Send back to API user
        string jsonString = JsonSerializer.Serialize(solver, options);
        return jsonString;
    }

    // Solve a instance given a certificate
///<summary>Returns a solution to a given 3SAT instance </summary>
///<param name="problemInstance" example="(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">3SAT problem instance string.</param>
///<response code="200">Returns a string </response>
    
    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solveInstance([FromQuery]string problemInstance) { //FromQuery]string certificate, 
        var options = new JsonSerializerOptions { WriteIndented = true };
        SAT3 SAT3_PROBLEM = new SAT3(problemInstance);
        Dictionary<string, bool> solution = SAT3_PROBLEM.defaultSolver.solve(SAT3_PROBLEM);

        string solutionString;
        string jsonString;
        if(solution == null){
            solutionString = "No Solution";
            jsonString = JsonSerializer.Serialize(solutionString, options);
            return jsonString;
        }

        else{
            //ALEX NOTE: This is a temporary fix. This logic should be moved to the SAT3 solver class soon. 
            solutionString = "(";
            foreach(KeyValuePair<string,bool> kvp in solution){
                solutionString = solutionString + kvp.Key + ":" + kvp.Value.ToString() + ",";
            }
            solutionString = solutionString.TrimEnd(',');
            solutionString = solutionString + ")"; 
            jsonString = JsonSerializer.Serialize(solutionString, options);
            return jsonString;
        }

    }

}

[ApiController]
[Route("[controller]")]
[Tags("3 SAT")]
#pragma warning disable CS1591
public class testSART3InstanceController : ControllerBase {

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getSingleInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        
        string returnString = certificate + problemInstance;
        return returnString;
    }

}
#pragma warning restore CS1591

