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


// Get all Solvers for a specific problem
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Solvers)")]
//Note: CALEB - should probably be removed with api refactor

#pragma warning disable CS1591

public class Problem_SolversController : ControllerBase {
#pragma warning restore CS1591
    
///<summary>Returns all solvers available for a given problem </summary>
///<param name="chosenProblem" example="NPC_SAT3">Problem name</param>
///<response code="200">Returns string array of solvers</response>

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
        string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/Solvers")
                            .Select(Path.GetFileName)
                            .ToArray();

        // Not completed. Needs to loop through these directories to get the rest of the problems
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subfiles, options);
        return jsonString;
    }
}

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

            foreach (string file in subfiles)
            {
                string fileNoExt = file.Split('.')[0]; //gets the file without the file extension
                subFilesList.Add(fileNoExt);
            }

             // Note -Caleb- the following is a temp solution to solve 3SAT using a clique solver remove, when
             // this is implemented to work for all problems

            //  if(chosenProblem == "SAT3"){
            //     subFilesList.Add("CliqueBruteForce - via SipserReduceToCliqueStandard");
            //  }

             jsonString = JsonSerializer.Serialize(subFilesList, options);


        }
        catch(System.IO.DirectoryNotFoundException){
            
            jsonString = JsonSerializer.Serialize(NOT_FOUND_ERR_SOLVER,options);
        }
        
        // Not completed. Needs to loop through these directories to get the rest of the problems
        return jsonString;
    }
}