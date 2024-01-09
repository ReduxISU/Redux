
using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using System.Text.Json;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_CLIQUECOVER;


namespace API.Problems.NPComplete.NPC_GRAPHCOLORING;


[ApiController]
[Route("[controller]")]
[Tags("Graph Coloring")]
#pragma warning disable CS1591
public class GRAPHCOLORINGGenericController : ControllerBase
{
#pragma warning restore CS1591


    ///<summary>Returns a default Graph Coloring object</summary>

    [ProducesResponseType(typeof(GRAPHCOLORING), 200)]
    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new GRAPHCOLORING(), options);
        return jsonString;
    }

    ///<summary>Returns a Graph Coloring object created from a given instance </summary>
    ///<param name="problemInstance" example="{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}">Graph Coloring problem instance string.</param>
    ///<response code="200">Returns GRAPHCOLORING problem object</response>

    [ProducesResponseType(typeof(GRAPHCOLORING), 200)]
    [HttpGet("instance")]
    public String getInstance([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new GRAPHCOLORING(problemInstance), options);
        return jsonString;
    }

    ///<summary>Returns a graph object used for dynamic visualization </summary>
    ///<param name="problemInstance" example="{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}">GraphColoring problem instance string.</param>
    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING aSet = new GRAPHCOLORING(problemInstance);
        GraphColoringGraph aGraph = aSet.graphColoringAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(aGraph.getNodeList, aGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

    ///<summary>Returns a graph object used for dynamic solved visualization </summary>
    ///<param name="problemInstance" example="{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}">GraphColoring problem instance string.</param>
    ///<param name="solution" example="{(a:0,b:1,c:2,d:1,e:2,f:1,g:2,h:1,i:2):3}">GraphColoring instance string.</param>

    ///<response code="200">Returns graph object</response>

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("solvedVisualization")]
#pragma warning disable CS1591
    public String getSolvedVisualization([FromQuery] string problemInstance, string solution)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING gColor = new GRAPHCOLORING(problemInstance);
        GraphColoringGraph cGraph = gColor.graphColoringAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(cGraph.getNodeList, cGraph.getEdgeList);
        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            apiGraph.nodes[i].attribute1 = i.ToString();
            List<string> parsing = solution.TrimStart('{').TrimStart('(').Split("):").ToList();
            List<string> nodeColoring = parsing[0].Split(",").ToList();

            foreach (var j in nodeColoring)
            {

                if (j.Split(':')[0].Contains(apiGraph.nodes[i].name))
                {
                    apiGraph.nodes[i].attribute2 = j.Split(':')[1].ToString();
                   
                }
            }
            
        }

        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;

    }

}


[ApiController]
[Route("[controller]")]
[Tags("Graph Coloring")]


#pragma warning disable CS1591

public class GraphColoringToCliqueCoverController : ControllerBase {
#pragma warning restore CS1591

  
///<summary>Returns a reduction object with info for Graph Coloring to CliqueCover Reduction </summary>
///<response code="200">Returns CliqueCoverReduction object</response>

    [ProducesResponseType(typeof(CliqueCoverReduction), 200)]
    [HttpGet("info")]
    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING defaultGRAPHCOLORING = new GRAPHCOLORING();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        CliqueCoverReduction reduction = new CliqueCoverReduction(defaultGRAPHCOLORING);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

///<summary>Returns a reduction from Graph Coloring to CliqueCover based on the given Graph Coloring instance  </summary>
///<param name="problemInstance" example="{{1,7,12,15} : 28}">Graph Coloring problem instance string.</param>
///<response code="200">Returns Fengs's Graph Coloring to CliqueCover object</response>

    [ProducesResponseType(typeof(CliqueCoverReduction), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING defaultGRAPHCOLORING = new GRAPHCOLORING(problemInstance);
        CliqueCoverReduction reduction = new CliqueCoverReduction(defaultGRAPHCOLORING);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

}


[ApiController]
[Route("[controller]")]
[Tags("Graph Coloring")]
#pragma warning disable CS1591
public class IgbokweVerifierController : ControllerBase
{
#pragma warning restore CS1591


    //string testVerifyString = "{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}";

    ///<summary>Returns information about the Graph Coloring Igbokwe Verifier </summary>
    ///<response code="200">Returns IgbokweVerifier Object</response>

    [ProducesResponseType(typeof(IgbokweVerifier), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        IgbokweVerifier verifier = new IgbokweVerifier();
        string jsonString = JsonSerializer.Serialize(verifier, options);
        return jsonString;
    }


///<summary>Verifies if a given certificate is a solution to a given Graph Coloring problem</summary>
///<param name="certificate" example="{(a:0,b:1,c:2,d:1,e:2,f:1,g:2,h:1,i:2):3}">certificate solution to Graph Coloring problem.</param>
///<param name="problemInstance" example="{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}">Graph Coloring problem instance string.</param>
///<response code="200">Returns a boolean</response>
    
    [ProducesResponseType(typeof(Boolean), 200)]
    [HttpGet("verify")]
    public String getInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING GRAPHCOLORINGProblem = new GRAPHCOLORING(problemInstance);
        IgbokweVerifier verifier = new IgbokweVerifier();
        Boolean response = verifier.verify(GRAPHCOLORINGProblem, certificate);
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;

    }

}

[ApiController]
[Route("[controller]")]
[Tags("Graph Coloring")]
#pragma warning disable CS1591
public class DanielBrelazSolverController : ControllerBase
{
#pragma warning restore CS1591


    ///<summary>Returns information about the Graph Coloring Daniel Brelaz solver </summary>
    ///<response code="200">Returns DanielBrelazSolver solver Object</response>

    [ProducesResponseType(typeof(DanielBrelazSolver), 200)]
    [HttpGet("info")]
    public String getGeneric()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        DanielBrelazSolver solver = new DanielBrelazSolver();
        string jsonString  = JsonSerializer.Serialize(solver, options);
        return jsonString; 
    }

    ///<summary>Returns a solution to a given  Graph Coloring problem instance </summary>
    ///<param name="problemInstance" example="{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}"> Graph Coloring problem instance string.</param>
    ///<response code="200">Returns solution string </response>

    [ProducesResponseType(typeof(string), 200)]
    [HttpGet("solve")]
    public String solvedInstance([FromQuery] string problemInstance)
    {

        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING GRAPHCOLORINGProblem = new GRAPHCOLORING(problemInstance);
        DanielBrelazSolver solver = new DanielBrelazSolver();
        string solvedInstance = solver.solve(GRAPHCOLORINGProblem);
        string jsonString = JsonSerializer.Serialize(solvedInstance, options);
        return jsonString;
    }


}


[ApiController]
[Route("[controller]")]
[Tags("Graph Coloring")]
#pragma warning disable CS1591
public class KarpReduceSATController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns a reduction object with info for Karps's Graph Coloring to SAT reduction </summary>
    ///<response code="200">Returns KarpReduceSAT reduction object</response>

    [ProducesResponseType(typeof(KarpReduceSAT), 200)]
    [HttpGet("info")]
    public String getInfo(){
        var options = new JsonSerializerOptions { WriteIndented = true };
        GRAPHCOLORING defaultGRAPHCOLORING = new GRAPHCOLORING();
        KarpReduceSAT reduction = new KarpReduceSAT(defaultGRAPHCOLORING);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

    ///<summary>Returns a reduction from Graph Coloring to AT based on the given Graph Coloring instance  </summary>
    ///<param name="problemInstance" example="{{a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}},3}">Graph Coloring problem instance string.</param>
    ///<response code="200">Returns Karps's Graph Coloring to SAT KarpReduceSAT reduction object</response>

    [ProducesResponseType(typeof(KarpReduceSAT), 200)]
    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance){
        KarpReduceSAT reduction = new KarpReduceSAT(new GRAPHCOLORING(problemInstance));
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

}

