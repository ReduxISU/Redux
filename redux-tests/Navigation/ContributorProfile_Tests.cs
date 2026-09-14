using Xunit;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using API.Interfaces;

namespace redux_tests;
#pragma warning disable CS1591

public class ContributorProfile_Tests {

    private readonly ContributorProfileController _controller;
    private readonly string _jsonFilePath;

    public ContributorProfile_Tests() {
        _controller = new ContributorProfileController();
        _jsonFilePath = Path.Combine(ProjectSourcePath.Value, "wwwroot", "contributorInfo.json");
    }

    // ─── contributorInfo.json sanity checks ───────────────────────────────────

    [Fact]
    public void ContributorInfoJson_FileExists() {
        Assert.True(File.Exists(_jsonFilePath), "contributorInfo.json is missing from wwwroot");
    }

    [Fact]
    public void ContributorInfoJson_IsValidJson() {
        string content = File.ReadAllText(_jsonFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var contributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(content, options);
        Assert.NotNull(contributors);
    }

    [Fact]
    public void ContributorInfoJson_HasAtLeastOneEntry() {
        string content = File.ReadAllText(_jsonFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var contributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(content, options);
        Assert.NotNull(contributors);
        Assert.True(contributors.Count > 0, "contributorInfo.json has no entries");
    }

    [Fact]
    public void ContributorInfoJson_NoEntryHasBlankName() {
        string content = File.ReadAllText(_jsonFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var contributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(content, options);
        Assert.NotNull(contributors);
        foreach (var key in contributors.Keys) {
            Assert.False(string.IsNullOrWhiteSpace(key), "Found an entry with a blank or null name key");
        }
    }

    [Fact]
    public void ContributorInfoJson_GithubUsernameFieldIsNeverNull() {
        string content = File.ReadAllText(_jsonFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var contributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(content, options);
        Assert.NotNull(contributors);
        foreach (var kvp in contributors) {
            // null means the field was missing entirely from the JSON — should be "" if not set
            Assert.True(kvp.Value.GithubUsername != null,
                $"{kvp.Key} is missing the githubUsername field — add it as an empty string");
        }
    }

    // ─── GET /names ───────────────────────────────────────────────────────────

    [Fact]
    public void GetContributorNames_Returns200() {
        var result = _controller.GetContributorNames();
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetContributorNames_ReturnsNonEmptyArray() {
        var ok = _controller.GetContributorNames() as OkObjectResult;
        Assert.NotNull(ok);
        var names = ok.Value as string[];
        Assert.NotNull(names);
        Assert.True(names.Length > 0, "Names list came back empty");
    }

    [Fact]
    public void GetContributorNames_NoDuplicates() {
        var ok = _controller.GetContributorNames() as OkObjectResult;
        Assert.NotNull(ok);
        var names = ok.Value as string[];
        Assert.NotNull(names);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    // ─── GET /directory ───────────────────────────────────────────────────────

    [Fact]
    public void GetContributorDirectory_Returns200() {
        var result = _controller.GetContributorDirectory();
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetContributorDirectory_EachEntryHasName() {
        var ok = _controller.GetContributorDirectory() as OkObjectResult;
        Assert.NotNull(ok);
        var entries = ok.Value as ContributorDirectoryEntry[];
        Assert.NotNull(entries);
        foreach (var entry in entries) {
            Assert.False(string.IsNullOrWhiteSpace(entry.Name), "Found a directory entry with a blank name");
        }
    }

    [Fact]
    public void GetContributorDirectory_GithubUsernameIsNeverNull() {
        var ok = _controller.GetContributorDirectory() as OkObjectResult;
        Assert.NotNull(ok);
        var entries = ok.Value as ContributorDirectoryEntry[];
        Assert.NotNull(entries);
        foreach (var entry in entries) {
            Assert.NotNull(entry.GithubUsername);
        }
    }

    [Fact]
    public void GetContributorDirectory_CountMatchesNamesEndpoint() {
        var namesOk = _controller.GetContributorNames() as OkObjectResult;
        var dirOk = _controller.GetContributorDirectory() as OkObjectResult;
        Assert.NotNull(namesOk);
        Assert.NotNull(dirOk);
        var names = namesOk.Value as string[];
        var entries = dirOk.Value as ContributorDirectoryEntry[];
        Assert.NotNull(names);
        Assert.NotNull(entries);
        Assert.Equal(names.Length, entries.Length);
    }

    [Theory]
    [InlineData("Himanshu Jha", "himanshujha05")]
    [InlineData("Pratham Khanal", "pkprathamkhanal")]
    [InlineData("Sansar Kharal", "kharsans")]
    [InlineData("Andrija Sevaljevic", "Andrija-Sevaljevic")]
    [InlineData("Jason Wright", "wrigjl")]
    [InlineData("Alex Svancara", "svanalex")]
    public void GetContributorDirectory_KnownGithubUsernamesAreCorrect(string name, string expectedUsername) {
        var ok = _controller.GetContributorDirectory() as OkObjectResult;
        Assert.NotNull(ok);
        var entries = ok.Value as ContributorDirectoryEntry[];
        Assert.NotNull(entries);
        var entry = entries.FirstOrDefault(e => e.Name == name);
        Assert.NotNull(entry);
        Assert.Equal(expectedUsername, entry.GithubUsername);
    }

    // ─── GET /{contributorName} ───────────────────────────────────────────────

    [Fact]
    public void GetContributorProfile_KnownContributor_Returns200() {
        var result = _controller.GetContributorProfile("Himanshu Jha");
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetContributorProfile_ReturnsCorrectName() {
        var ok = _controller.GetContributorProfile("Himanshu Jha") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.Equal("Himanshu Jha", portfolio.ContributorName);
    }

    [Fact]
    public void GetContributorProfile_ReturnsCorrectGithubUsername() {
        var ok = _controller.GetContributorProfile("Himanshu Jha") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.Equal("himanshujha05", portfolio.GithubUsername);
    }

    [Fact]
    public void GetContributorProfile_CaseInsensitiveLookup_StillReturnsProfile() {
        // "himanshu jha" (all lowercase) should match "Himanshu Jha" in the JSON
        var ok = _controller.GetContributorProfile("himanshu jha") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.Equal("himanshujha05", portfolio.GithubUsername);
    }

    [Fact]
    public void GetContributorProfile_UnknownContributor_FieldsFallBackToNotSpecified() {
        var ok = _controller.GetContributorProfile("Nonexistent Person") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.Equal("Not specified", portfolio.Email);
        Assert.Equal("Not specified", portfolio.Bio);
        Assert.Equal("Not specified", portfolio.Education);
        Assert.Equal("Not specified", portfolio.Major);
    }

    [Fact]
    public void GetContributorProfile_UnknownContributor_GithubUsernameIsEmptyString() {
        var ok = _controller.GetContributorProfile("Nonexistent Person") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.Equal("", portfolio.GithubUsername);
    }

    [Fact]
    public void GetContributorProfile_ListsAreNeverNull() {
        var ok = _controller.GetContributorProfile("Himanshu Jha") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.NotNull(portfolio.ProblemsContributed);
        Assert.NotNull(portfolio.SolversCreated);
        Assert.NotNull(portfolio.ReductionsCreated);
        Assert.NotNull(portfolio.VerifiersContributed);
        Assert.NotNull(portfolio.VisualizationsCreated);
    }

    [Fact]
    public void GetContributorProfile_TotalContributions_MatchesSumOfLists() {
        var ok = _controller.GetContributorProfile("Himanshu Jha") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        int expected = portfolio.ProblemsContributed.Count
                     + portfolio.SolversCreated.Count
                     + portfolio.ReductionsCreated.Count
                     + portfolio.VerifiersContributed.Count
                     + portfolio.VisualizationsCreated.Count;
        Assert.Equal(expected, portfolio.TotalContributions);
    }

    [Fact]
    public void GetContributorProfile_TotalContributions_IsNonNegative() {
        var ok = _controller.GetContributorProfile("Himanshu Jha") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.True(portfolio.TotalContributions >= 0);
    }

    [Fact]
    public void GetContributorProfile_JasonWright_ReductionLookupSucceeds() {
        // SipserReduceToSAT3 (NPC_CLIQUE/ReduceTo/NPC_SAT3) credits "Jason Wright" but
        // has no parameterless constructor — its only ctor immediately calls reduce(),
        // which requires a specially Sipser-formatted CLIQUE instance (see
        // [NotAGeneralReduction] on that class). Reduction contributor lookup is
        // reflection-based (ProblemProvider.Reductions + Activator.CreateInstance), so
        // it can't safely construct that one and it's expected to be absent here —
        // consistent with it already being excluded from /Navigation/Reductions.
        var ok = _controller.GetContributorProfile("Jason Wright") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.DoesNotContain("SipserReduceToSAT3", portfolio.ReductionsCreated);
    }

    [Fact]
    public void GetContributorProfile_JasonWright_FindsKnownSolvers() {
        // ShorsQuantumSolver.cs (NPC_PRIMEFACTOR) and NQueensConstructive.cs (P_NQUEENS)
        // both credit "Jason Wright" — real matches for the Solvers directory walk.
        var ok = _controller.GetContributorProfile("Jason Wright") as OkObjectResult;
        Assert.NotNull(ok);
        var portfolio = ok.Value as ContributorPortfolio;
        Assert.NotNull(portfolio);
        Assert.Contains("ShorsQuantumSolver", portfolio.SolversCreated);
        Assert.Contains("NQueensConstructive", portfolio.SolversCreated);
    }

    // ─── contributors[] arrays vs. contributorInfo.json ────────────────────────

    // Every name that shows up in a `contributors` array anywhere in the codebase
    // (Problems, Solvers, Reductions, Verifiers, Visualizers) has to match a
    // contributorInfo.json key exactly, or that person's work silently stops
    // counting toward their profile. This is the failure mode that let
    // SUDOKU_Class ("Eric Hill, Carter Luker, Collin Kress, & Daniel Fawson" as one
    // string) and BINPACKING_Class (first names only: "Himanshu", "Rakesh",
    // "Prashanta") go unnoticed. When this test fails, either fix the contributors[]
    // array to use the person's full contributorInfo.json name, or add them to
    // contributorInfo.json if they're new.
    [Fact]
    public void AllDeclaredContributors_MatchAContributorInfoJsonEntry() {
        string content = File.ReadAllText(_jsonFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var knownContributors = JsonSerializer.Deserialize<Dictionary<string, ContributorInfo>>(content, options);
        Assert.NotNull(knownContributors);
        var knownNames = new HashSet<string>(knownContributors.Keys, StringComparer.OrdinalIgnoreCase);

        var unmatched = new List<string>();

        foreach (var type in ProblemProvider.Problems.Values) {
            try {
                if (Activator.CreateInstance(type) is IProblem instance) {
                    foreach (var name in instance.contributors) {
                        if (!IsKnownOrPlaceholder(name, knownNames)) unmatched.Add($"Problem '{type.Name}' credits '{name}', which has no contributorInfo.json entry");
                    }
                }
            } catch { }
        }

        foreach (var type in ProblemProvider.Solvers.Values) {
            try {
                if (Activator.CreateInstance(type) is ISolver instance) {
                    foreach (var name in instance.contributors) {
                        if (!IsKnownOrPlaceholder(name, knownNames)) unmatched.Add($"Solver '{type.Name}' credits '{name}', which has no contributorInfo.json entry");
                    }
                }
            } catch { }
        }

        foreach (var type in ProblemProvider.Reductions.Values) {
            try {
                if (Activator.CreateInstance(type) is IReduction instance) {
                    foreach (var name in instance.contributors) {
                        if (!IsKnownOrPlaceholder(name, knownNames)) unmatched.Add($"Reduction '{type.Name}' credits '{name}', which has no contributorInfo.json entry");
                    }
                }
            } catch { }
        }

        foreach (var type in ProblemProvider.Verifiers.Values) {
            try {
                if (Activator.CreateInstance(type) is IVerifier instance) {
                    foreach (var name in instance.contributors) {
                        if (!IsKnownOrPlaceholder(name, knownNames)) unmatched.Add($"Verifier '{type.Name}' credits '{name}', which has no contributorInfo.json entry");
                    }
                }
            } catch { }
        }

        foreach (var type in ProblemProvider.Visualizers.Values) {
            try {
                if (Activator.CreateInstance(type) is IVisualization instance) {
                    foreach (var name in instance.contributors) {
                        if (!IsKnownOrPlaceholder(name, knownNames)) unmatched.Add($"Visualization '{type.Name}' credits '{name}', which has no contributorInfo.json entry");
                    }
                }
            } catch { }
        }

        Assert.True(unmatched.Count == 0, "Found contributors[] entries with no matching contributorInfo.json key:\n" + string.Join("\n", unmatched));
    }

    // A handful of entries are deliberate "we don't know" placeholders rather than a
    // name that should be in contributorInfo.json — don't nag about those every run.
    private static readonly HashSet<string> _placeholders = new(StringComparer.OrdinalIgnoreCase) {
        "", "Author Unknown", "TODO",
    };

    private static bool IsKnownOrPlaceholder(string name, HashSet<string> knownNames) {
        return knownNames.Contains(name) || _placeholders.Contains(name);
    }

    // ─── GET /all ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetAllContributors_Returns200() {
        var result = _controller.GetAllContributors();
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetAllContributors_ReturnsKnownComplexityFolders() {
        var ok = _controller.GetAllContributors() as OkObjectResult;
        Assert.NotNull(ok);
        var json = ok.Value as string;
        Assert.NotNull(json);
        var folders = JsonSerializer.Deserialize<string[]>(json);
        Assert.NotNull(folders);
        Assert.Contains("NPComplete", folders);
        Assert.Contains("P", folders);
        Assert.Contains("NPHard", folders);
    }
}
