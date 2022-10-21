using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Collections;


// Get all problems regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
public class All_ProblemsController : ControllerBase {
    
    public String getDefault() {
        string?[] subdirs = Directory.GetDirectories("Problems")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirs, options);

        return jsonString;
    }
}

// Get only NP-Complete problems
[ApiController]
[Route("Navigation/[controller]")]
public class NPC_ProblemsController : ControllerBase {
    
    public String getDefault() {
        string?[] subdirs = Directory.GetDirectories("Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();
                            
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirs, options);

        //Response.Headers.Add("Access-Control-Allow-Origin", "http://127.0.0.1:5500");
        return jsonString;
    }

    [HttpGet("json")]
    public string getProblemsJson(){
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories("Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach(string problemInstance in subdirs){
        string jsonPair = $"{{\"{objectPrefix}\" : \"{problemInstance}\"}}";
            jsonedList.Add(jsonPair);
        }
        
         var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(jsonedList, options);
        return jsonString;
    }
}


// Get only NP-Complete problems
[ApiController]
[Route("Navigation/[controller]")]
public class NPC_ProblemsRefactorController : ControllerBase {
    
    public String getDefault() {
        string?[] subdirs = Directory.GetDirectories("Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList subdirsNoPrefix = new ArrayList();
        foreach(string problemDirName in subdirs){
            string[] splitStr = problemDirName.Split('_');
            string newName = splitStr[1];
            subdirsNoPrefix.Add(newName);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirsNoPrefix, options);

        // ProblemGraph graph = new ProblemGraph();
        // graph.getConnectedNodes("SAT3");
        // string ring = JsonSerializer.Serialize(graph.getConnectedNodes("SAT3"), options);
        //  Console.WriteLine("\n"+ring );

        //Response.Headers.Add("Access-Control-Allow-Origin", "http://127.0.0.1:5500");
        return jsonString;
    }

    [HttpGet("json")]
    public string getProblemsJson(){
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories("Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach(string problemInstance in subdirs){
        string jsonPair = $"{{\"{objectPrefix}\" : \"{problemInstance}\"}}";
            jsonedList.Add(jsonPair);
        }
        
         var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(jsonedList, options);
        return jsonString;
    }
}

[ApiController]
[Route("Navigation/[controller]")]
public class NPC_NavGraph : ControllerBase {

    [HttpGet("info")]
    public string getProblemGraph(){
        ProblemGraph nav_graph = new ProblemGraph();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(nav_graph.graph, options);

        return jsonString;

    }

    [HttpGet("availableReductions")]
    public string getConnectedProblems([FromQuery]string chosenProblem){
        ProblemGraph nav_graph = new ProblemGraph();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(nav_graph.getConnectedProblems(chosenProblem.ToLower()), options);

        return jsonString;
    }

    [HttpGet("reductionPath")]
    public string getPaths([FromQuery]string reducingFrom, string reducingTo){
        ProblemGraph nav_graph = new ProblemGraph();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(nav_graph.getReductionPath(reducingFrom.ToLower(),reducingTo.ToLower()), options);

        return jsonString;
    }


}