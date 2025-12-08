using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Collections;


// Get all problems regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591
public class All_ProblemsController : ControllerBase {
#pragma warning restore CS1591
//Note: CALEB - should probably be renamed with api refactor


///<summary>Returns list of all available problem types </summary>
///<response code="200">Returns string array of problem types</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault() {
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems")
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
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPC_ProblemsController : ControllerBase {
#pragma warning restore CS1591
//Note: CALEB - should probably be removed with api refactor

///<summary>Returns all NP-Complete problems </summary>
///<response code="200">Returns string array of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault() {
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();
                            
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirs, options);

        //Response.Headers.Add("Access-Control-Allow-Origin", "http://127.0.0.1:5500");
        return jsonString;
    }

///<summary>Returns all NP-Complete problems </summary>
///<response code="200">Returns json dictionary of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("json")]
    public string getProblemsJson(){
        string projectSourcePath = ProjectSourcePath.Value;
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach(var problemInstance in subdirs){
            if (problemInstance is null)
                continue;
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
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPC_ProblemsRefactorController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns all NP-Complete problems </summary>
///<response code="200">Returns string array of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault() {
        // File system patch that should work on both Window/Linux enviroments
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"/Problems/NPComplete")
                        .Select(Path.GetFileName)
                        .ToArray();

        ArrayList subdirsNoPrefix = new ArrayList();
        foreach(var problemDirName in subdirs){
            if (problemDirName is null)
                continue;
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

///<summary>Returns all NP-Complete problems </summary>
///<response code="200">Returns json dictionary of all NP-Complete problems</response>
//Note: CALEB - should probably be removed with api refactor

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("json")]
    public string getProblemsJson(){
        string projectSourcePath = ProjectSourcePath.Value;
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach(var problemInstance in subdirs){
            if (problemInstance is null)
                continue;
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
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPC_NavGraph : ControllerBase {
#pragma warning restore CS1591


///<summary>Returns graph of all NP-Complete problems connected through reductions </summary>
///<response code="200">Returns json graph of all NP-Complete problems connected through reductions</response>
//Note: CALEB - should probably be renamed with api refactor

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("info")]
    public string getProblemGraph(){
        ProblemGraph nav_graph = new ProblemGraph();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(nav_graph.graph, options);

        return jsonString;

    }

    ///<summary>Returns all problems reachable from given problem via reductions </summary>
    ///<param name="chosenProblem" example="SAT3">NP-Complete problem name</param>
    ///<response code="200">Returns string array of NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("availableReductions")]
    public string getConnectedProblems([FromQuery]string chosenProblem){
        ProblemGraph nav_graph = new ProblemGraph();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(nav_graph.getConnectedProblems(chosenProblem.ToLower()), options);

        return jsonString;
    }

    ///<summary>Returns reduction path from a given problem to another given problem </summary>
    ///<param name="reducingFrom" example="SAT3">NP-Complete problem name</param>
    ///<param name="reducingTo" example="ARCSET">NP-Complete problem name</param>
    ///<response code="200">Returns string array of NP-Complete reductions</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("reductionPath")]
    public string getPaths([FromQuery]string reducingFrom, string reducingTo){
        ProblemGraph nav_graph = new ProblemGraph();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(nav_graph.getReductionPath(reducingFrom.ToLower(),reducingTo.ToLower()), options);

        return jsonString;
    }


}