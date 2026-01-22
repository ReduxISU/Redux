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

    /// <summary>Returns the portfolio/profile of a specific contributor with all their contributions</summary>
    /// <param name="contributorName">Name of the contributor to fetch profile for</param>
    /// <response code="200">Returns contributor profile with all contributions</response>
    [ProducesResponseType(typeof(ContributorPortfolio), 200)]
    [HttpGet("{contributorName}")]
    public IActionResult GetContributorProfile(string contributorName) {
        try {
            string projectSourcePath = ProjectSourcePath.Value;
            
            // Get contributor info from JSON
            var contributorInfo = GetContributorInfo(contributorName);
            
            // Get problems for this specific contributor
            var allProblems = GetAllProblems(projectSourcePath, contributorName);
            
            // Get solvers for this specific contributor
            var allSolvers = GetAllSolvers(projectSourcePath, contributorName);
            
            // Get reductions for this specific contributor
            var allReductions = GetAllReductions(projectSourcePath, contributorName);
            
            // Build contributor portfolio
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

    /// <summary>Returns list of all contributors available in the system</summary>
    /// <response code="200">Returns list of all contributor names</response>
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
            // Scan NPComplete folder
            string npcPath = Path.Combine(problemsPath, "NPComplete");
            if (Directory.Exists(npcPath)) {
                var npcProblemDirs = Directory.GetDirectories(npcPath);
                foreach (var problemDir in npcProblemDirs) {
                    string problemName = "NPC_" + Path.GetFileName(problemDir);
                    // Check if contributor worked on this problem by looking at files
                    if (ContributorWorkedOnProblem(problemDir, contributorName)) {
                        problems.Add(problemName);
                    }
                }
            }

            // Scan any other complexity class folders
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
            // Check main problem files
            var csFiles = Directory.GetFiles(problemDir, "*.cs", SearchOption.TopDirectoryOnly);
            foreach (var file in csFiles) {
                string content = System.IO.File.ReadAllText(file);
                if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            
            // Check Solvers folder
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
            
            // Check ReduceTo folder
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
            // Scan all problem folders for Solvers subfolder
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
            // Scan all problem folders for ReduceTo subfolder
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
            
            if (allContributors != null && allContributors.ContainsKey(contributorName)) {
                return allContributors[contributorName];
            }
        }
        catch { }

        return null;
    }
}

/// <summary>Represents a contributor's portfolio/profile</summary>
public class ContributorPortfolio {
    /// <summary>Name of the contributor</summary>
    [JsonPropertyName("contributorName")]
    public string ContributorName { get; set; }

    /// <summary>Email of the contributor</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; }

    /// <summary>Education/University of the contributor</summary>
    [JsonPropertyName("education")]
    public string Education { get; set; }

    /// <summary>Major/Field of study</summary>
    [JsonPropertyName("major")]
    public string Major { get; set; }

    /// <summary>Bio/Description of the contributor</summary>
    [JsonPropertyName("bio")]
    public string Bio { get; set; }

    /// <summary>List of problems this contributor has worked on</summary>
    [JsonPropertyName("problemsContributed")]
    public List<string> ProblemsContributed { get; set; } = new List<string>();

    /// <summary>List of solvers this contributor has created</summary>
    [JsonPropertyName("solversCreated")]
    public List<string> SolversCreated { get; set; } = new List<string>();

    /// <summary>List of reductions this contributor has created</summary>
    [JsonPropertyName("reductionsCreated")]
    public List<string> ReductionsCreated { get; set; } = new List<string>();

    /// <summary>Total number of contributions</summary>
    [JsonPropertyName("totalContributions")]
    public int TotalContributions { get; set; }
}

/// <summary>Contributor information from JSON file</summary>
public class ContributorInfo {
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("education")]
    public string Education { get; set; }

    [JsonPropertyName("major")]
    public string Major { get; set; }

    [JsonPropertyName("bio")]
    public string Bio { get; set; }
}
