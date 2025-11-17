using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;

// Get all problems regardless of complexity class

[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591

public class All_ReductionsController : ControllerBase {
#pragma warning restore CS1591

//Note: CALEB - should probably be removed with api refactor

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
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591

//Note: CALEB - should probably be removed with api refactor

public class NPC_ReductionsController : ControllerBase {
#pragma warning restore CS1591
    
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
        return jsonString;
    }
}

// Get all problems we can reduce to for a specific problem
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591
//Note: CALEB - should probably be removed with api refactor

public class Problem_ReductionsController : ControllerBase {
#pragma warning restore CS1591

    const string NO_REDUCTIONS_ERROR = "{\"ERROR\": \"No Reductions Available\"}"; //API Response. 

///<summary>Returns all problems which a given problem directly reduces to </summary>
///<param name="chosenProblem" example="NPC_SAT3">Problem name</param>
///<response code="200">Returns string array of problems</response>

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

        string jsonString = "";
        var options = new JsonSerializerOptions { WriteIndented = true };
  
        try{
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/ReduceTo")
                            .Select(Path.GetFileName)
                            .ToArray();

        jsonString = JsonSerializer.Serialize(subdirs, options);
 
        }
        catch (System.IO.DirectoryNotFoundException dirNotFoundException){
            Console.WriteLine(NO_REDUCTIONS_ERROR + " directory not found, exception was thrown in Nav_Reductions.cs");
                        jsonString = NO_REDUCTIONS_ERROR;
            Console.WriteLine(dirNotFoundException.StackTrace);
        }
        return jsonString;
    }
}


// Get all problems we can reduce to for a specific problem
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591

public class Problem_ReductionsRefactorController : ControllerBase {
#pragma warning restore CS1591

    const string NO_REDUCTIONS_ERROR = "{\"ERROR\": \"No Reductions Available\"}"; //API Response. 

///<summary>Returns all problems which a given  problem directly reduces to </summary>
///<param name="chosenProblem" example="SAT3">Problem name</param>
///<param name="problemType" example="NPC">Problem type</param>
///<response code="200">Returns string array of problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem ,[FromQuery] string problemType) {
        

        // Determine the directory to search based on prefix. chosenProblem expected to be a problemName like "NPC_PROBLEM"\
        //This method uses a query param to check whether a problem is NPComplete or Polynomial, unlike the original method which checks a name prefix.

        string problemTypeDirectory = "";

        if (problemType == "NPC") { 
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }

        string jsonString = "";
        var options = new JsonSerializerOptions { WriteIndented = true };
  
        try{
        string projectSourcePath = ProjectSourcePath.Value;

        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/"+problemType+"_" + chosenProblem + "/ReduceTo")
                            .Select(Path.GetFileName)
                            .ToArray();

        ArrayList subdirsNoPrefix = new ArrayList();
        foreach(string problemDirName in subdirs){
            string[] splitStr = problemDirName.Split('_');
            string newName = splitStr[1];
            subdirsNoPrefix.Add(newName);
        }

        jsonString = JsonSerializer.Serialize(subdirsNoPrefix, options);
 
        }
        catch (System.IO.DirectoryNotFoundException){
            //Console.WriteLine(NO_REDUCTIONS_ERROR + " directory not found, exception was thrown in Nav_Reductions.cs");
                        jsonString = NO_REDUCTIONS_ERROR;
            //Console.WriteLine(dirNotFoundException.StackTrace);
        }
        finally{

        }
        return jsonString;
    }
}

// Get all reductions implemented for a specific problem
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591
//Note: CALEB - should probably be removed with api refactor

public class PossibleReductionsController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns al list of reductions from a given problem to another given problem </summary>
///<param name="reducingFrom" example="NPC_SAT3">Problem name</param>
///<param name="reducingTo" example="NPC_CLIQUE">Problem name</param>
///<response code="200">Returns string array of reductions</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string reducingFrom, [FromQuery]string reducingTo) {

        // Determine the directory to search based on prefix. reducingFrom and reducingTo are both expected to be a problemName like "NPC_PROBLEM"
        string problemTypeDirectory = "";
        string problemType = reducingFrom.Split('_')[0];

        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }

        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + reducingFrom + "/ReduceTo/" + reducingTo)
                            .Select(Path.GetFileName)
                            .ToArray();
                  
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subfiles, options);
        return jsonString;
    }
}

// Get all reductions implemented for a specific problem
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591

public class PossibleReductionsRefactorController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns al list of reductions from a given problem to another given problem </summary>
///<param name="reducingFrom" example="SAT3">Problem name</param>
///<param name="reducingTo" example="CLIQUE">Problem name</param>
///<param name="problemType" example="NPC">Problem type</param>
///<response code="200">Returns string array of reductions</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string reducingFrom, [FromQuery]string reducingTo,[FromQuery]string problemType) {
        string NOT_FOUND_ERR_REDUCTION = "entered a reduce from or to that does not exist";


        // Determine the directory to search based on prefix. reducingFrom and reducingTo are both expected to be a problemName like "NPC_PROBLEM"
        string problemTypeDirectory = "";

        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }
        else if (problemType == "P") {
            problemTypeDirectory = "Polynomial";
        }

        string jsonString = "";
        var options = new JsonSerializerOptions { WriteIndented = true };

        try
        {

            string projectSourcePath = ProjectSourcePath.Value;
            string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + problemType + "_" + reducingFrom + "/ReduceTo/NPC_" + reducingTo)
                                .Select(Path.GetFileName)
                                .ToArray();

            ArrayList subFilesList = new ArrayList();
            foreach (string file in subfiles)
            {
                string fileNoExt = file.Split('.')[0];
                subFilesList.Add(fileNoExt);
            }
            jsonString = JsonSerializer.Serialize(subFilesList, options);

        }
        catch(System.IO.DirectoryNotFoundException){
            Console.WriteLine(NOT_FOUND_ERR_REDUCTION);
            jsonString = JsonSerializer.Serialize(NOT_FOUND_ERR_REDUCTION, options);
        }
        finally{
            
        }
        return jsonString;
    }
}

// Get all problems from a chosen reduction
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591

public class Reverse_ReductionsController : ControllerBase {

//Note: CALEB - should probably be removed with api refactor
    // [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenReduction) {
        string projectSourcePath = ProjectSourcePath.Value;
        string?[] subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();
                            
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(subdirs, options);
        return jsonString;
    }
}
 #pragma warning restore CS1591
