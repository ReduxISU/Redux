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

        /// <summary>Pull up everything we know about a contributor — their personal info plus every problem, solver, and reduction they've touched</summary>
        /// <param name="contributorName">Just pass in their full name, e.g. "Himanshu Jha"</param>
        /// <response code="200">Got it — here's their full profile</response>
        [ProducesResponseType(typeof(ContributorPortfolio), 200)]
        [HttpGet("{contributorName}")]
        public IActionResult GetContributorProfile(string contributorName) {
                try {
                        string projectSourcePath = ProjectSourcePath.Value;

                        // Grab their bio, email, GitHub, etc. from contributorInfo.json
                        var contributorInfo = GetContributorInfo(contributorName);

                        // Walk the Problems folder and see which NP-Complete problems they worked on
                        var allProblems = GetAllProblems(projectSourcePath, contributorName);

                        // Same idea — check every Solvers subfolder for their name
                        var allSolvers = GetAllSolvers(projectSourcePath, contributorName);

                        // And every ReduceTo subfolder for reductions they wrote
                        var allReductions = GetAllReductions(projectSourcePath, contributorName);

                        // Bundle everything together into one tidy portfolio object
                        var portfolio = new ContributorPortfolio {
                                ContributorName = contributorName,
                                Email = contributorInfo?.Email ?? "Not specified",
                                Education = contributorInfo?.Education ?? "Not specified",
                                Major = contributorInfo?.Major ?? "Not specified",
                                Bio = contributorInfo?.Bio ?? "Not specified",
                                GithubUsername = contributorInfo?.GithubUsername ?? "",
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

        /// <summary>Just the names please — returns every contributor name from contributorInfo.json so the About Us page can build its list without any hardcoding</summary>
        /// <response code="200">An array of names in the same order they appear in the JSON file</response>
        [ProducesResponseType(typeof(string[]), 200)]
        [HttpGet("names")]
        public IActionResult GetContributorNames() {
                try {
                        string infoFilePath = Path.Combine(ProjectSourcePath.Value, "wwwroot", "contributorInfo.json");

                        if (!System.IO.File.Exists(infoFilePath)) {
                                return NotFound(new { message = "contributorInfo.json not found" });
                        }

                        string jsonContent = System.IO.File.ReadAllText(infoFilePath);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var allContributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(jsonContent, options);

                        if (allContributors == null) {
                                return Ok(Array.Empty<string>());
                        }

                        return Ok(allContributors.Keys.ToArray());
                }
                catch (Exception ex) {
                        return BadRequest(new { error = ex.Message });
                }
        }

        /// <summary>One call to rule them all — returns each contributor's name and GitHub username together so the frontend can render the list and tooltips in a single fetch</summary>
        /// <response code="200">Array of { name, githubUsername } — githubUsername is an empty string if the contributor hasn't added theirs yet</response>
        [ProducesResponseType(typeof(ContributorDirectoryEntry[]), 200)]
        [HttpGet("directory")]
        public IActionResult GetContributorDirectory() {
                try {
                        string infoFilePath = Path.Combine(ProjectSourcePath.Value, "wwwroot", "contributorInfo.json");

                        if (!System.IO.File.Exists(infoFilePath)) {
                                return NotFound(new { message = "contributorInfo.json not found" });
                        }

                        string jsonContent = System.IO.File.ReadAllText(infoFilePath);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var allContributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(jsonContent, options);

                        if (allContributors == null) {
                                return Ok(Array.Empty<ContributorDirectoryEntry>());
                        }

                        var directory = allContributors.Select(kvp => new ContributorDirectoryEntry {
                                Name = kvp.Key,
                                GithubUsername = kvp.Value.GithubUsername ?? ""
                        }).ToArray();

                        return Ok(directory);
                }
                catch (Exception ex) {
                        return BadRequest(new { error = ex.Message });
                }
        }

        /// <summary>Returns all the problem folder names — mostly used for internal tooling, not the About Us page</summary>
        /// <response code="200">Array of folder names under the Problems directory</response>
        [ProducesResponseType(typeof(string[]), 200)]
        [HttpGet("all")]
        public IActionResult GetAllContributors() {
                try {
                        string projectSourcePath = ProjectSourcePath.Value;
                        string problemsPath = Path.Combine(projectSourcePath, "Problems");

                        if (!Directory.Exists(problemsPath)) {
                                return NotFound(new { message = "Problems directory not found" });
                        }

                        // Just grab the top-level folder names under Problems/
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
                        // Check the NPComplete folder first — that's where most problems live
                        string npcPath = Path.Combine(problemsPath, "NPComplete");
                        if (Directory.Exists(npcPath)) {
                                var npcProblemDirs = Directory.GetDirectories(npcPath);
                                foreach (var problemDir in npcProblemDirs) {
                                        string problemName = "NPC_" + Path.GetFileName(problemDir);
                                        if (ContributorWorkedOnProblem(problemDir, contributorName)) {
                                                problems.Add(problemName);
                                        }
                                }
                        }

                        // Also sweep any other complexity folders that aren't NPComplete
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
                        // Check the .cs files sitting directly in the problem folder
                        var csFiles = Directory.GetFiles(problemDir, "*.cs", SearchOption.TopDirectoryOnly);
                        foreach (var file in csFiles) {
                                string content = System.IO.File.ReadAllText(file);
                                if (content.Contains(contributorName, StringComparison.OrdinalIgnoreCase)) {
                                        return true;
                                }
                        }

                        // Check the Solvers subfolder
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

                        // Check the ReduceTo subfolder
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
                        string problemsPath = Path.Combine(projectSourcePath, "Problems");

                        if (!Directory.Exists(problemsPath)) {
                                return solvers;
                        }

                        // Dig into every problem's Solvers folder and look for their name
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
                        string problemsPath = Path.Combine(projectSourcePath, "Problems");

                        if (!Directory.Exists(problemsPath)) {
                                return reductions;
                        }

                        // Same approach — walk every ReduceTo folder and check for their name
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

        private ContributorInfo? GetContributorInfo(string contributorName) {
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
                                // Case-insensitive match so "himanshu jha" and "Himanshu Jha" both work
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

/// <summary>Everything about a contributor in one place — personal info and a full list of what they built</summary>
public class ContributorPortfolio {
        /// <summary>Their full name</summary>
        [JsonPropertyName("contributorName")]
        public string? ContributorName { get; set; }

        /// <summary>Their ISU (or other) email address</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>Where they studied</summary>
        [JsonPropertyName("education")]
        public string? Education { get; set; }

        /// <summary>What they studied</summary>
        [JsonPropertyName("major")]
        public string? Major { get; set; }

        /// <summary>A short blurb about them — filled in by the contributor themselves</summary>
        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        /// <summary>Their GitHub handle — the frontend uses this to build the avatar image and profile link</summary>
        [JsonPropertyName("githubUsername")]
        public string? GithubUsername { get; set; }

        /// <summary>Every NP-Complete problem they've touched</summary>
        [JsonPropertyName("problemsContributed")]
        public List<string> ProblemsContributed { get; set; } = new List<string>();

        /// <summary>Every solver they've written</summary>
        [JsonPropertyName("solversCreated")]
        public List<string> SolversCreated { get; set; } = new List<string>();

        /// <summary>Every reduction they've written</summary>
        [JsonPropertyName("reductionsCreated")]
        public List<string> ReductionsCreated { get; set; } = new List<string>();

        /// <summary>Quick total — problems + solvers + reductions combined</summary>
        [JsonPropertyName("totalContributions")]
        public int TotalContributions { get; set; }
}

/// <summary>What we store in contributorInfo.json for each person — edit this file to add or update contributor details</summary>
public class ContributorInfo {
        /// <summary>Their email address</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>Where they studied, e.g. "Idaho State University"</summary>
        [JsonPropertyName("education")]
        public string? Education { get; set; }

        /// <summary>Their field of study</summary>
        [JsonPropertyName("major")]
        public string? Major { get; set; }

        /// <summary>A short bio written by the contributor</summary>
        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        /// <summary>Their GitHub username — leave blank if they don't have one or haven't added it yet</summary>
        [JsonPropertyName("githubUsername")]
        public string? GithubUsername { get; set; }
}

/// <summary>Slim version used by the /directory endpoint — just enough for the About Us page to show names and GitHub links without loading full profiles</summary>
public class ContributorDirectoryEntry {
        /// <summary>Their full name</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>Their GitHub username — empty string if not set yet</summary>
        [JsonPropertyName("githubUsername")]
        public string GithubUsername { get; set; } = "";
}
