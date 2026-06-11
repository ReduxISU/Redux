using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;

// Get all Verifiers regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Verifiers)")]
#pragma warning disable CS1591
//Note: CALEB - should probably be removed with api refactor

public class All_VerifiersController : ControllerBase {
#pragma warning restore CS1591
   
///<summary>Returns all verifiers available for a given problem </summary>
///<param name="chosenProblem" example="NPC_SAT3">Problem name</param>
///<response code="200">Returns string array of verifiers</response>

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
        string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/Verifiers")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subfiles, options);
        return jsonString;
    }
}
// Get all Verifiers for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Verifiers)")]
#pragma warning disable CS1591
//Note: CALEB - should probably be removed with api refactor

public class Problem_VerifiersController : ControllerBase {
#pragma warning restore CS1591
    
///<summary>Returns all verifiers available for a given problem </summary>
///<param name="chosenProblem" example="NPC_SAT3">Problem name</param>
///<response code="200">Returns string array of verifiers</response>

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
        string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/Verifiers")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subfiles, options);
        return jsonString;
    }
}

// Get all Verifiers for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Verifiers)")]
#pragma warning disable CS1591

public class Problem_VerifiersRefactorController : ControllerBase {
#pragma warning restore CS1591
    
///<summary>Returns all verifiers available for a given problem </summary>
///<param name="chosenProblem" example="SAT3">Problem name</param>
///<param name="problemType" example="NPC">Problem type</param>
///<response code="200">Returns string array of verifiers</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem,[FromQuery]string problemType) {
                string NOT_FOUND_ERR_VERIFIER = "entered a verifier that does not exist";

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
            string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + problemType + "_" + chosenProblem + "/Verifiers")
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

            // Not completed. Needs to loop through these directories to get the rest of the problems
            jsonString = JsonSerializer.Serialize(subFilesList, options);
        }
        catch (System.IO.DirectoryNotFoundException)
        {
            // Fallback: search every class directory under Problems/ for any folder
            // whose name ends with _{chosenProblem}, regardless of prefix.
            var fallback = FindVerifiersAcrossAllDirs(ProjectSourcePath.Value, chosenProblem);
            jsonString = fallback.Count > 0
                ? JsonSerializer.Serialize(fallback, options)
                : JsonSerializer.Serialize(NOT_FOUND_ERR_VERIFIER, options);
        }
        return jsonString;
    }

    private static List<string> FindVerifiersAcrossAllDirs(string root, string chosenProblem) {
        var result = new List<string>();
        var problemsRoot = Path.Combine(root, "Problems");
        if (!Directory.Exists(problemsRoot)) return result;
        foreach (var classDir in Directory.GetDirectories(problemsRoot)) {
            foreach (var problemDir in Directory.GetDirectories(classDir)) {
                var dirName = Path.GetFileName(problemDir) ?? "";
                var idx = dirName.IndexOf('_');
                if (idx >= 0 && dirName[(idx + 1)..] == chosenProblem) {
                    var verifiersPath = Path.Combine(problemDir, "Verifiers");
                    if (!Directory.Exists(verifiersPath)) continue;
                    foreach (var file in Directory.GetFiles(verifiersPath)) {
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