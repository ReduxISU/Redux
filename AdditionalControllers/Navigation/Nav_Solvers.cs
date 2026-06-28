using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;

// Get all Solvers regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Solvers)")]
#pragma warning disable CS1591

public class All_SolversController : ControllerBase {
//Note: CALEB - should probably be removed with api refactor

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getDefault() {
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"/Solvers")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirs, options);
        return jsonString;
    }
}
#pragma warning restore CS1591


// Get all Solvers for a specific problem (Refactored)
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Solvers)")]
#pragma warning disable CS1591

public class Problem_SolversRefactorController : ControllerBase {
#pragma warning restore CS1591

            string NOT_FOUND_ERR_SOLVER = "entered a solver that does not exist";

///<summary>Returns all solvers available for a given problem </summary>
///<param name="chosenProblem" example="SAT3">Problem name</param>
///<param name="problemType" example="NPC">Problem type</param>
///<response code="200">Returns string array of solvers</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem, [FromQuery]string problemType) {

        // Determine the directory to search based on prefix. chosenProblem expected to be a problemName like "NPC_PROBLEM"\
        string problemTypeDirectory = "";
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = "";

        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }


        try
        {
            string projectSourcePath = ProjectSourcePath.Value;
            string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + problemType + "_" + chosenProblem + "/Solvers")
                                .Select(Path.GetFileName)
                                .ToArray();

            ArrayList subFilesList = new ArrayList();

            foreach (var file in subfiles)
            {
                if (file is null)
                    continue;
                string fileNoExt = file.Split('.')[0]; //gets the file without the file extension
                subFilesList.Add(fileNoExt);
            }

             jsonString = JsonSerializer.Serialize(subFilesList, options);


        }
        catch(System.IO.DirectoryNotFoundException){
            // Fallback: search every class directory under Problems/ for any folder
            // whose name ends with _{chosenProblem}, regardless of prefix.
            var fallback = FindSolversAcrossAllDirs(ProjectSourcePath.Value, chosenProblem);
            jsonString = fallback.Count > 0
                ? JsonSerializer.Serialize(fallback, options)
                : JsonSerializer.Serialize(NOT_FOUND_ERR_SOLVER, options);
        }
        
        // Not completed. Needs to loop through these directories to get the rest of the problems
        return jsonString;
    }

    private static List<string> FindSolversAcrossAllDirs(string root, string chosenProblem) {
        var result = new List<string>();
        var problemsRoot = Path.Combine(root, "Problems");
        if (!Directory.Exists(problemsRoot)) return result;
        foreach (var classDir in Directory.GetDirectories(problemsRoot)) {
            foreach (var problemDir in Directory.GetDirectories(classDir)) {
                var dirName = Path.GetFileName(problemDir) ?? "";
                var idx = dirName.IndexOf('_');
                if (idx >= 0 && dirName[(idx + 1)..] == chosenProblem) {
                    var solversPath = Path.Combine(problemDir, "Solvers");
                    if (!Directory.Exists(solversPath)) continue;
                    foreach (var file in Directory.GetFiles(solversPath)) {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (name != null) result.Add(name);
                    }
                    return result;
                }
            }
        }
        return result;
    }
}