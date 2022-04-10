using Microsoft.AspNetCore.Mvc;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using API.Problems.NPComplete.NPC_ARCSET.Verifiers;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;

namespace API.Problems.NPComplete.NPC_ARCSET;

[ApiController]
[Route("[controller]")]
public class ARCSETGenericController : ControllerBase {

    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new ARCSET(), options);
        return jsonString;
    }

    [HttpGet("{instance}")]
    public String getInstance() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(new ARCSET(), options);
        return jsonString;
    }
}

[ApiController]
[Route("[controller]")]
public class ArcsetVerifierController : ControllerBase {

      [HttpGet]
    public String getInstance([FromQuery]string certificate, [FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET ARCSETProblem = new ARCSET(problemInstance);
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
        Boolean response = verifier.verify(ARCSETProblem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(response.ToString(), options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
public class ArcsetSolverController : ControllerBase {

    [HttpGet("info")]
    public String getDefault(){
        
        var options = new JsonSerializerOptions { WriteIndented = true };

        ARCSET ARCSETProblem = new ARCSET();
        AlexNaiveSolver solver = new AlexNaiveSolver();
        string graphSolvedInstance = solver.solve(ARCSETProblem);
        string prettySolvedInstance = solver.prettySolve(ARCSETProblem);
        string[] totalSolvedInstance  = new string[2];
        totalSolvedInstance[0] = graphSolvedInstance;
        totalSolvedInstance[1] =  prettySolvedInstance;
        //Boolean response = verifier.verify(ARCSETProblem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(totalSolvedInstance, options);
        return jsonString;

    }

      //[HttpGet("solve")]
          [HttpGet]

    public String getInstance([FromQuery]string problemInstance) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET ARCSETProblem = new ARCSET(problemInstance);
        AlexNaiveSolver solver = new AlexNaiveSolver();
        string graphSolvedInstance = solver.solve(ARCSETProblem);
        string prettySolvedInstance = solver.prettySolve(ARCSETProblem);
        string[] totalSolvedInstance  = new string[2];
        totalSolvedInstance[0] = graphSolvedInstance;
        totalSolvedInstance[1] =  prettySolvedInstance;
        //Boolean response = verifier.verify(ARCSETProblem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(totalSolvedInstance, options);
        return jsonString;
    }

}

[ApiController]
[Route("[controller]")]
public class NCOV_TO_ARCSETController : ControllerBase {

      [HttpGet("info")] // url parameter

      public String getDefault(){
            var options = new JsonSerializerOptions { WriteIndented = true };

            VERTEXCOVER vCov = new VERTEXCOVER();
            String undirectedGraphStr = vCov.defaultInstance;
            UndirectedGraph ug = new UndirectedGraph(undirectedGraphStr);
            String reduction = ug.reduction();
            String jsonString = JsonSerializer.Serialize(reduction,options);
            return jsonString;
      }

    
      [HttpGet]
    public String getInstance([FromQuery]string problemInstance) {
        
        //from query is a query parameter
        
        Console.WriteLine(problemInstance);
        var options = new JsonSerializerOptions { WriteIndented = true };
        UndirectedGraph UG = new UndirectedGraph(problemInstance);
        string reduction = UG.reduction();
        //Boolean response = verifier.verify(ARCSETProblem,certificate);
        // Send back to API user
        string jsonString = JsonSerializer.Serialize(reduction, options);
        return jsonString;

    }

}

[ApiController]
[Route("[controller]")]
public class ArcsetJsonPayloadController : ControllerBase {

      [HttpGet]
    public String getInstance([FromQuery]string listType) {
               // Console.WriteLine("RECEIVED REQUEST");

        var options = new JsonSerializerOptions { WriteIndented = true };
        ARCSET defaultArcset = new ARCSET();
        DirectedGraph defaultGraph = defaultArcset.directedGraph;
        string jsonString = "";
        List<Edge> edgeList = defaultGraph.getEdgeList;
        List<Node> nodeList = defaultGraph.getNodeList;


                if(listType.Equals("nodes")){

                    jsonString = JsonSerializer.Serialize(nodeList,options);
                }
                else if(listType.Equals("edges")){
                    jsonString = JsonSerializer.Serialize(edgeList,options);
                }
                else{
                    jsonString = JsonSerializer.Serialize("BAD INPUT, choose edges or nodes for listType. You chose: "+listType,options);

                }

        //string jsonString = JsonSerializer.Serialize(totalString, options);
        return jsonString;
    }

    }
