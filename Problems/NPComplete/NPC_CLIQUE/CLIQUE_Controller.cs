using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_VertexCover;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER;


namespace API.Problems.NPComplete.NPC_CLIQUE;

[ApiController]
[Route("[controller]")]
public class CLIQUEGenericController : ControllerBase {

    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new CLIQUE(), options);
        return jsonString;
    }

    [HttpGet("instance")]
    public String getDefault([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        CLIQUE devClique = new CLIQUE(problemInstance);
        
        string jsonString = JsonSerializer.Serialize(devClique, options);
        return jsonString;
    }

      [HttpGet("visualize")]
    public String getVisualization([FromQuery] string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemInstance);
        CliqueGraph cGraph = clique.cliqueAsGraph;
        API_UndirectedGraphJSON apiFormat = new API_UndirectedGraphJSON(cGraph.getNodeList, cGraph.getEdgeList);

        string jsonString = JsonSerializer.Serialize(apiFormat, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
public class sipserReduceToVCController : ControllerBase {

    // [HttpGet]
    // public String getDefault() {
    //     var options = new JsonSerializerOptions { WriteIndented = true };
    //     CLIQUE defaultCLIQUE = new CLIQUE();
    //     Clique_to_VertexCoverReduction reduction = new Clique_to_VertexCoverReduction(defaultCLIQUE);
    //     string jsonString = JsonSerializer.Serialize(reduction.reductionTo, options);
    //     return jsonString;
    // }

    // [HttpGet("{instance}")]
    // public String getInstance() {
    //     var options = new JsonSerializerOptions { WriteIndented = true };
    //     string jsonString = JsonSerializer.Serialize(new CLIQUE(), options);
    //     return jsonString;
    // }

    [HttpGet("info")]

    public String getInfo() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE defaultCLIQUE = new CLIQUE();
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        sipserReduction reduction = new sipserReduction(defaultCLIQUE);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }

    [HttpGet("reduce")]
    public String getReduce([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE defaultCLIQUE = new CLIQUE(problemInstance);
        //SAT3 defaultSAT3 = new SAT3(problemInstance);
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        sipserReduction reduction = new sipserReduction(defaultCLIQUE);
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;
    }
    [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemInstance);
        CliqueGraph cGraph = clique.cliqueAsGraph;
        API_UndirectedGraphJSON apiGraphFrom = new API_UndirectedGraphJSON(cGraph.getNodeList,cGraph.getEdgeList);
        for(int i=0;i<apiGraphFrom.nodes.Count;i++){
            apiGraphFrom.nodes[i].attribute1 = i.ToString();
        }
        //SAT3 defaultSAT3 = new SAT3(problemInstance);
        //SipserReduction reduction = new SipserReduction(defaultSAT3);
        sipserReduction reduction = new sipserReduction(clique);
        VERTEXCOVER reducedVcov = reduction.reductionTo;
        VertexCoverGraph vGraph = reducedVcov.VCAsGraph;
        API_UndirectedGraphJSON apiGraphTo = new API_UndirectedGraphJSON(vGraph.getNodeList, vGraph.getEdgeList);
        API_UndirectedGraphJSON[] apiArr = new API_UndirectedGraphJSON[2];
        apiArr[0] = apiGraphFrom;
        apiArr[1] = apiGraphTo;
        string jsonString = JsonSerializer.Serialize(apiArr, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
public class CLIQUEDevController : ControllerBase
{

    [HttpGet]
    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        CLIQUE devClique = new CLIQUE();
        
        string jsonString = JsonSerializer.Serialize(devClique, options);
        return jsonString;
    }
    
        [HttpGet("instance")]
    public String getDefault([FromQuery] string problemInstance)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        CLIQUE devClique = new CLIQUE(problemInstance);
        
        string jsonString = JsonSerializer.Serialize(devClique, options);
        return jsonString;
    }


     [HttpGet("visualize")]
    public String getVisualization([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        CLIQUE clique = new CLIQUE(problemInstance);
        CliqueGraph cGraph = clique.cliqueAsGraph;
        API_UndirectedGraphJSON apiGraph = new API_UndirectedGraphJSON(cGraph.getNodeList, cGraph.getEdgeList);
        string jsonString = JsonSerializer.Serialize(apiGraph, options);
        return jsonString;
    }

}