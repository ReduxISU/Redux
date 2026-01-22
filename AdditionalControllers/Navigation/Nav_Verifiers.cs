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
            jsonString = JsonSerializer.Serialize(NOT_FOUND_ERR_VERIFIER, options);

        }
        return jsonString;
    }
}