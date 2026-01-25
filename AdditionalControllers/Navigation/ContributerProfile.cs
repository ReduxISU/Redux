using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Contributor Profile)")]
#pragma warning disable CS1591
public class ContributorProfileController : ControllerBase {
#pragma warning restore CS1591

    /// <summary>Retrieve a contributor's full profile including their personal details and all contributions to the project</summary>
    /// <param name="contributorName">The name of the contributor whose profile you want to view</param>
    /// <response code="200">Successfully returns the contributor's complete profile with all their work</response>
    [ProducesResponseType(typeof(ContributorPortfolio), 200)]
    [HttpGet("{contributorName}")]
    public IActionResult GetContributorProfile(string contributorName) {
        try {
            string projectSourcePath = ProjectSourcePath.Value;
            
            // Load personal information from the contributor database
            var contributorInfo = GetContributorInfo(contributorName);
            
            // Find all NP-Complete problems this contributor worked on
            var allProblems = GetAllProblems(projectSourcePath, contributorName);
            
            // Find all solvers this contributor created
            var allSolvers = GetAllSolvers(projectSourcePath, contributorName);
            
            // Find all problem reductions this contributor created
            var allReductions = GetAllReductions(projectSourcePath, contributorName);
            
            // Combine all contributions and create their profile
            var portfolio = new ContributorPortfolio {
                ContributorName = contributorName,
                Email = contributorInfo?.Email ?? "Not specified",
                Education = contributorInfo?.Education ?? "Not specified",
                Major = contributorInfo?.Major ?? "Not specified",
                Bio = contributorInfo?.Bio ?? "Not specified",
                ProblemsContributed = allProblems.ToList(),
                SolversCreated = allSolvers.ToList(),
                ReductionsCreated = allReductions.ToList(),
                TotalContributions = allProblems.Count() + allSolvers.Count() + allReductions.Count()
            };

            return Ok(portfolio);
        }
        catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get a list of all contributors in the system</summary>
    /// <response code="200">Successfully returns the list of all contributor names</response>
    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("all")]
    public IActionResult GetAllContributors() {
        try {
            string projectSourcePath = ProjectSourcePath.Value;
            string problemsPath = Path.Combine(projectSourcePath, "Problems");
            
            if (!Directory.Exists(problemsPath)) {
                return NotFound(new { message = "Problems directory not found" });
            }

            // Get all problem folders
            var problemFolders = Directory.GetDirectories(problemsPath)
                .Select(Path.GetFileName)
                .ToArray();

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(problemFolders, options);

            return Ok(jsonString);
        }
        catch (Exception ex) {
            return BadRequest(new { error = ex.Message });
        }
    }

    private IEnumerable<string> GetAllProblems(string projectSourcePath, string contributorName) {
        string problemsPath = Path.Combine(projectSourcePath, "Problems");
        
        if (!Directory.Exists(problemsPath)) {
            return new List<string>();
        }

        var problems = new List<string>();
        
        try {
            // Scan the NP-Complete problems folder
            string npcPath = Path.Combine(problemsPath, "NPComplete");
            if (Directory.Exists(npcPath)) {
                var npcProblemDirs = Directory.GetDirectories(npcPath);
                foreach (var problemDir in npcProblemDirs) {
                    string problemName = "NPC_" + Path.GetFileName(problemDir);
                    // Check if the contributor's name appears in the problem files
                    if (ContributorWorkedOnProblem(problemDir, contributorName)) {
                        problems.Add(problemName);
                    }
                }
            }

            // Scan any other problem difficulty/complexity folders
            var otherFolders = Directory.GetDirectories(problemsPath)
                .Where(d => Path.GetFileName(d) != "NPComplete");
            
            foreach (var folder in otherFolders) {
                var folderName = Path.GetFileName(folder);
                var subProblemDirs = Directory.GetDirectories(folder);
                
                foreach (var subDir in subProblemDirs) {
                    string problemName = folderName + "_" + Path.GetFileName(subDir);
                    if (ContributorWorkedOnProblem(subDir, contributorName)) {
                        problems.Add(problemName);
                    }
                }
            }
        }
        catch { }

        return problems.Distinct();
    }

    private bool ContributorWorkedOnProblem(string problemDir, string contributorName) {
        try {
            // Search for the contributor's name in the main problem files
            var csFiles = Directory.GetFiles(problemDir, "*.cs", SearchOption.TopDirectoryOnly);
            foreach (var file in csFiles) {
                string content = System.IO.File.ReadAllText(file);
                if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            
            // Search in the Solvers folder for this problem
            string solversPath = Path.Combine(problemDir, "Solvers");
            if (Directory.Exists(solversPath)) {
                var solverFiles = Directory.GetFiles(solversPath, "*.cs");
                foreach (var file in solverFiles) {
                    string content = System.IO.File.ReadAllText(file);
                    if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }
            
            // Search in the ReduceTo folder for this problem
            string reducePath = Path.Combine(problemDir, "ReduceTo");
            if (Directory.Exists(reducePath)) {
                var reduceFiles = Directory.GetFiles(reducePath, "*.cs");
                foreach (var file in reduceFiles) {
                    string content = System.IO.File.ReadAllText(file);
                    if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }
        }
        catch { }
        
        return false;
    }

    private IEnumerable<string> GetAllSolvers(string projectSourcePath, string contributorName) {
        var solvers = new List<string>();
        
        try {
            // Scan through all problems to find solvers created by this contributor
            string problemsPath = Path.Combine(projectSourcePath, "Problems");
            
            if (!Directory.Exists(problemsPath)) {
                return solvers;
            }

            var allProblemDirs = Directory.GetDirectories(problemsPath, "*", SearchOption.AllDirectories);
            
            foreach (var problemDir in allProblemDirs) {
                string solversPath = Path.Combine(problemDir, "Solvers");
                if (Directory.Exists(solversPath)) {
                    var solverFiles = Directory.GetFiles(solversPath, "*.cs");
                    foreach (var file in solverFiles) {
                        string content = System.IO.File.ReadAllText(file);
                        if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                            solvers.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }
            }
        }
        catch { }

        return solvers.Distinct();
    }

    private IEnumerable<string> GetAllReductions(string projectSourcePath, string contributorName) {
        var reductions = new List<string>();
        
        try {
            // Scan through all problems to find reductions created by this contributor
            string problemsPath = Path.Combine(projectSourcePath, "Problems");
            
            if (!Directory.Exists(problemsPath)) {
                return reductions;
            }

            var allProblemDirs = Directory.GetDirectories(problemsPath, "*", SearchOption.AllDirectories);
            
            foreach (var problemDir in allProblemDirs) {
                string reductionsPath = Path.Combine(problemDir, "ReduceTo");
                if (Directory.Exists(reductionsPath)) {
                    var reductionFiles = Directory.GetFiles(reductionsPath, "*.cs");
                    foreach (var file in reductionFiles) {
                        string content = System.IO.File.ReadAllText(file);
                        if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                            reductions.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }
            }
        }
        catch { }

        return reductions.Distinct();
    }

    private ContributorInfo GetContributorInfo(string contributorName) {
        try {
            string projectSourcePath = ProjectSourcePath.Value;
            string infoFilePath = Path.Combine(projectSourcePath, "wwwroot", "contributorInfo.json");
            
            if (!System.IO.File.Exists(infoFilePath)) {
                return null;
            }

            string jsonContent = System.IO.File.ReadAllText(infoFilePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var allContributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(jsonContent, options);
            
            if (allContributors != null) {
                // Perform case-insensitive search to find the contributor regardless of capitalization
                var contributor = allContributors.FirstOrDefault(x => 
                    x.Key.Equals(contributorName, StringComparison.OrdinalIgnoreCase));
                
                if (!string.IsNullOrEmpty(contributor.Key)) {
                    return contributor.Value;
                }
            }
        }
        catch { }

        return null;
    }
}

/// <summary>Represents a contributor's complete profile with personal details and all contributions</summary>
public class ContributorPortfolio {
    /// <summary>The contributor's full name</summary>
    [JsonPropertyName("contributorName")]
    public string? ContributorName { get; set; }

    /// <summary>The contributor's email address</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>The contributor's education institution</summary>
    [JsonPropertyName("education")]
    public string? Education { get; set; }

    /// <summary>The contributor's field of study or major</summary>
    [JsonPropertyName("major")]
    public string? Major { get; set; }

    /// <summary>A short biography describing the contributor's expertise</summary>
    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    /// <summary>List of all NP-Complete problems this contributor has worked on</summary>
    [JsonPropertyName("problemsContributed")]
    public List<string> ProblemsContributed { get; set; } = new List<string>();

    /// <summary>List of all algorithm solvers this contributor has created</summary>
    [JsonPropertyName("solversCreated")]
    public List<string> SolversCreated { get; set; } = new List<string>();

    /// <summary>List of all problem reductions this contributor has created</summary>
    [JsonPropertyName("reductionsCreated")]
    public List<string> ReductionsCreated { get; set; } = new List<string>();

    /// <summary>Total count of all contributions (problems + solvers + reductions)</summary>
    [JsonPropertyName("totalContributions")]
    public int TotalContributions { get; set; }
}

/// <summary>Stores basic information about a contributor from the database</summary>
public class ContributorInfo {
    /// <summary>The contributor's email address</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>The contributor's educational institution</summary>
    [JsonPropertyName("education")]
    public string? Education { get; set; }

    /// <summary>The contributor's field of study</summary>
    [JsonPropertyName("major")]
    public string? Major { get; set; }

    /// <summary>A short biography about the contributor</summary>
    [JsonPropertyName("bio")]
    public string? Bio { get; set; }
}
