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
        var options = new JsonSerializerOptions { WriteIndented = true };

        try
        {
            string projectSourcePath = ProjectSourcePath.Value;
            string problemsRoot = Path.Combine(projectSourcePath, "Problems");

            // Search all complexity-class subdirectories for any folder ending with _<chosenProblem>
            // so that NPH, NPC, P, and any future types are all found regardless of the
            // problemType query parameter (which the Redux_GUI client hardcodes to "NPC").
            string[]? visFiles = null;
            foreach (string typeDir in Directory.GetDirectories(problemsRoot))
            {
                foreach (string probDir in Directory.GetDirectories(typeDir))
                {
                    string folderName = Path.GetFileName(probDir);
                    if (!folderName.EndsWith("_" + chosenProblem, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string visPath = Path.Combine(probDir, "Visualizations");
                    if (!Directory.Exists(visPath))
                        continue;

                    visFiles = Directory.GetFiles(visPath)
                                       .Select(Path.GetFileName)
                                       .Where(f => f is not null)
                                       .ToArray()!;
                    break;
                }
                if (visFiles is not null) break;
            }

            if (visFiles is null)
                return JsonSerializer.Serialize(NOT_FOUND_ERR_Visualization, options);

            var subFilesList = new ArrayList();
            foreach (var file in visFiles)
                subFilesList.Add(file!.Split('.')[0]); // strip .cs extension

            return JsonSerializer.Serialize(subFilesList, options);
        }
        catch (System.IO.DirectoryNotFoundException)
        {
            return JsonSerializer.Serialize(NOT_FOUND_ERR_Visualization, options);
        }
    }
}