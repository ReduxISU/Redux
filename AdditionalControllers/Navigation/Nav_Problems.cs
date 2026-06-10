using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Collections;

//NOTE - Corbin: In order to add a controller for another file within /Problems, simply copy a specified controller (such as 'NPC_ProblemsRefactorController')
//               paste and rename, the only changes needed is the path which defines subdirs for both functions: getDefault and getProblemsJson and update comments

//NOTE - Corbin: All specific controllers should probably be removed with the addition of tags, instead of one function per folder it should probably query a tag (like the NagGraph does for chosenProblem)
//               in order to have one generalized specified problem controller

// Get all problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class ALL_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

///<summary>Returns all problems</summary>
///<response code = "200">Returns string array of all problems regardless of class</response>
    
    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        string projectSourcePath = ProjectSourcePath.Value;
        var allProblemDirNames = new List<string>();

        foreach (var classDir in Directory.GetDirectories(projectSourcePath + @"Problems"))
        {
            foreach (var problemDir in Directory.GetDirectories(classDir))
            {
                var name = Path.GetFileName(problemDir);
                if (name != null)
                    allProblemDirNames.Add(name);
            }
        }
        string?[] subdirs = allProblemDirNames.ToArray();
        ArrayList subdirsNoPrefix = new ArrayList();
        foreach (var problemDirName in subdirs)
        {
            if (problemDirName is null)
                continue;
            string[] splitStr = problemDirName.Split('_');
            string newName = splitStr[1];
            subdirsNoPrefix.Add(newName);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirsNoPrefix, options);
        return jsonString;
    }


///<summary>Returns all problems</summary>
///<response code = "200">Returns string array of all problems regardless of class</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("json")]
    public string getProblemsJson()
    {
        string projectSourcePath = ProjectSourcePath.Value;
        string objectPrefix = "problemName";

        var allProblemDirNames = new List<string>();

        foreach (var classDir in Directory.GetDirectories(projectSourcePath + @"Problems"))
        {
            foreach (var problemDir in Directory.GetDirectories(classDir))
            {
                var name = Path.GetFileName(problemDir);
                if (name != null)
                    allProblemDirNames.Add(name);
            }
        }

        ArrayList jsonedList = new ArrayList();
        foreach (var problemInstance in allProblemDirNames)
        {
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

public class NPC_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns all NP-Complete problems </summary>
    ///<response code="200">Returns string array of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        // File system patch that should work on both Window/Linux enviroments
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath + @"/Problems/NPComplete")
                        .Select(Path.GetFileName)
                        .ToArray();

        ArrayList subdirsNoPrefix = new ArrayList();
        foreach (var problemDirName in subdirs)
        {
            if (problemDirName is null)
                continue;
            string[] splitStr = problemDirName.Split('_');
            string newName = splitStr[1];
            subdirsNoPrefix.Add(newName);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirsNoPrefix, options);

        //NOTE - Corbin: below is someone elses commented out code, I am leaving it for now just in case it is needed.

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
    public string getProblemsJson()
    {
        string projectSourcePath = ProjectSourcePath.Value;
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories(projectSourcePath + @"Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach (var problemInstance in subdirs)
        {
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

// Get only P-Class problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class P_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns all P-Class problems </summary>
    ///<response code="200">Returns string array of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        // File system patch that should work on both Window/Linux enviroments
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath + @"/Problems/P")
                        .Select(Path.GetFileName)
                        .ToArray();

        ArrayList subdirsNoPrefix = new ArrayList();
        foreach (var problemDirName in subdirs)
        {
            if (problemDirName is null)
                continue;
            string[] splitStr = problemDirName.Split('_');
            string newName = splitStr[1];
            subdirsNoPrefix.Add(newName);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirsNoPrefix, options);
        return jsonString;
    }

    ///<summary>Returns all P-Class problems </summary>
    ///<response code="200">Returns json dictionary of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("json")]
    public string getProblemsJson()
    {
        string projectSourcePath = ProjectSourcePath.Value;
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories(projectSourcePath + @"Problems/P")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach (var problemInstance in subdirs)
        {
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

// Get only NP-Hard problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPHard_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns all NPHard problems </summary>
    ///<response code="200">Returns string array of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        // File system patch that should work on both Window/Linux enviroments
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath + @"/Problems/NPHard")
                        .Select(Path.GetFileName)
                        .ToArray();

        ArrayList subdirsNoPrefix = new ArrayList();
        foreach (var problemDirName in subdirs)
        {
            if (problemDirName is null)
                continue;
            string[] splitStr = problemDirName.Split('_');
            string newName = splitStr[1];
            subdirsNoPrefix.Add(newName);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirsNoPrefix, options);
        return jsonString;
    }

    ///<summary>Returns all NPHard problems </summary>
    ///<response code="200">Returns json dictionary of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("json")]
    public string getProblemsJson()
    {
        string projectSourcePath = ProjectSourcePath.Value;
        string objectPrefix = "problemName";
        string?[] subdirs = Directory.GetDirectories(projectSourcePath + @"Problems/NPHard")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList jsonedList = new ArrayList();
        foreach (var problemInstance in subdirs)
        {
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


    ///<summary>Returns all problems reachable from given problem via reductions </summary>
    ///<param name="chosenProblem" example="SAT3">NP-Complete problem name</param>
    ///<response code="200">Returns string array of NP-Complete problems</response>

    // Backed by ReductionGraphData (see Nav_Reductions.cs) so this view cannot
    // drift from /Navigation/Reductions. Response shape preserved for callers.
    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("availableReductions")]
    public string getConnectedProblems([FromQuery]string chosenProblem){
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ReductionGraphData.ReachableFrom(chosenProblem), options);
    }

    ///<summary>Returns reduction path from a given problem to another given problem </summary>
    ///<param name="reducingFrom" example="SAT3">NP-Complete problem name</param>
    ///<param name="reducingTo" example="ARCSET">NP-Complete problem name</param>
    ///<response code="200">Returns string array of NP-Complete reductions</response>

    // Backed by ReductionGraphData (see Nav_Reductions.cs).
    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("reductionPath")]
    public string getPaths([FromQuery]string reducingFrom, string reducingTo){
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ReductionGraphData.PathBetween(reducingFrom, reducingTo), options);
    }
}