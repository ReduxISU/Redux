using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;

// Get all Visualizations regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Visualizations)")]
#pragma warning disable CS1591
//Note: CALEB - should probably be removed with api refactor

public class All_VisualizationsController : ControllerBase {
#pragma warning restore CS1591
   
///<summary>Returns all Visualizations available for a given problem </summary>
///<param name="chosenProblem" example="NPC_SAT3">Problem name</param>
///<response code="200">Returns string array of Visualizations</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem) {

        // Determine the directory to search based on prefix. chosenProblem expected to be a problemName like "NPC_PROBLEM"\
        string problemTypeDirectory = "";
        string problemType = chosenProblem.Split('_')[0];

        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }

        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/Visualizations")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subfiles, options);
        return jsonString;
    }
}
// Get all Visualizations for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Visualizations)")]
#pragma warning disable CS1591
//Note: CALEB - should probably be removed with api refactor

public class Problem_VisualizationsController : ControllerBase {
#pragma warning restore CS1591
    
///<summary>Returns all Visualizations available for a given problem </summary>
///<param name="chosenProblem" example="NPC_SAT3">Problem name</param>
///<response code="200">Returns string array of Visualizations</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem) {

        // Determine the directory to search based on prefix. chosenProblem expected to be a problemName like "NPC_PROBLEM"\
        string problemTypeDirectory = "";
        string problemType = chosenProblem.Split('_')[0];

        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/Visualizations")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subfiles, options);
        return jsonString;
    }
}

// Get all Visualizations for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Visualizations)")]
#pragma warning disable CS1591

public class Problem_VisualizationsRefactorController : ControllerBase {
#pragma warning restore CS1591
    
///<summary>Returns all Visualizations available for a given problem </summary>
///<param name="chosenProblem" example="SAT3">Problem name</param>
///<param name="problemType" example="NPC">Problem type</param>
///<response code="200">Returns string array of Visualizations</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem,[FromQuery]string problemType) {
        List<string> NOT_FOUND_ERR_Visualization = new();

        // Determine the directory to search based on prefix. chosenProblem expected to be a problemName like "NPC_PROBLEM"\
        string problemTypeDirectory = "";
        string jsonString = "";
        var options = new JsonSerializerOptions { WriteIndented = true };

        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }

        try
        {
            string projectSourcePath = ProjectSourcePath.Value;
            string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + problemType + "_" + chosenProblem + "/Visualizations")
                                .Select(Path.GetFileName)
                                .ToArray();

            ArrayList subFilesList = new ArrayList();

            foreach (string file in subfiles)
            {
                string fileNoExt = file.Split('.')[0]; //gets the file without the file extension
                subFilesList.Add(fileNoExt);
            }

            // Not completed. Needs to loop through these directories to get the rest of the problems
            jsonString = JsonSerializer.Serialize(subFilesList, options);
        }
        catch (System.IO.DirectoryNotFoundException)
        {
            jsonString = JsonSerializer.Serialize(NOT_FOUND_ERR_Visualization, options);
        }
        return jsonString;
    }
}