using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;

// Get all problems regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
public class All_ReductionsController : ControllerBase {

    
    [ApiExplorerSettings(IgnoreApi = true)]
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
public class NPC_ReductionsController : ControllerBase {
    
    [ApiExplorerSettings(IgnoreApi = true)]
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
public class Problem_ReductionsController : ControllerBase {

    public const string NO_REDUCTIONS_ERROR = "{\"ERROR\": \"No Reductions Available\"}"; //API Response. 

    [ApiExplorerSettings(IgnoreApi = true)]
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
public class Problem_ReductionsRefactorController : ControllerBase {

    public const string NO_REDUCTIONS_ERROR = "{\"ERROR\": \"No Reductions Available\"}"; //API Response. 

    [ApiExplorerSettings(IgnoreApi = true)]
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
        catch (System.IO.DirectoryNotFoundException dirNotFoundException){
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
public class PossibleReductionsController : ControllerBase {
    
    [ApiExplorerSettings(IgnoreApi = true)]
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
public class PossibleReductionsRefactorController : ControllerBase {

    [ApiExplorerSettings(IgnoreApi = true)]
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
        catch(System.IO.DirectoryNotFoundException notFoundEx){
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
public class Reverse_ReductionsController : ControllerBase {
    
    [ApiExplorerSettings(IgnoreApi = true)]
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