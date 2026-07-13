using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

// Problem navigation resolved via reflection over ProblemProvider.Problems, so it no
// longer depends on the on-disk Problems/ folder layout. Mirrors the solver / verifier
// / visualization navigation (#317/#318/#331); those endpoints were converted earlier,
// this closes the gap for the problem-listing endpoints.
//
// A problem's complexity class is read from its namespace: every problem lives in
// API.Problems.<ComplexityClass>.<ProblemFolder> (e.g. API.Problems.NPComplete.NPC_SAT3).
// Only these top-level problems are listed; nested helper variants such as
// API.Problems.NPComplete.NPC_CLIQUE.Inherited (SipserClique) are excluded, matching the
// old top-level directory scan.
internal static class ProblemNavigationData
{
    internal class ProblemEntry
    {
        public string className { get; set; } = "";
        public string complexityClass { get; set; } = "";
    }

    // Built once from reflected problem types.
    internal static readonly List<ProblemEntry> Entries = Build();

    private static List<ProblemEntry> Build()
    {
        var entries = new List<ProblemEntry>();
        foreach (var (_, type) in ProblemProvider.Problems)
        {
            // Namespace is API . Problems . <ComplexityClass> . <ProblemFolder>.
            // Anything deeper (…<Folder>.Inherited, .ReduceTo.*, …) is a nested helper
            // problem, not a top-level listable one, so require exactly that shape.
            string[] ns = (type.Namespace ?? "").Split('.');
            int i = Array.IndexOf(ns, "Problems");
            if (i < 0 || ns.Length != i + 3) continue;

            entries.Add(new ProblemEntry
            {
                className = type.Name,
                complexityClass = ns[i + 1],
            });
        }

        return entries
            .GroupBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // All problem names, sorted (Entries is already sorted).
    internal static List<string> All() => Entries.Select(e => e.className).ToList();

    // Problem names for one complexity class (namespace segment: NPComplete / P / NPHard).
    internal static List<string> ByComplexity(string complexityClass) =>
        Entries
            .Where(e => string.Equals(e.complexityClass, complexityClass, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.className)
            .ToList();
}

// Get all problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class ALL_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

///<summary>Returns all problems</summary>
///<response code = "200">Returns string array of all problems regardless of class</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ProblemNavigationData.All(), options);
    }
}

// Get only NP-Complete problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPC_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns all NP-Complete problems </summary>
    ///<response code="200">Returns string array of all NP-Complete problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ProblemNavigationData.ByComplexity("NPComplete"), options);
    }
}

// Get only P-Class problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class P_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns all P-Class problems </summary>
    ///<response code="200">Returns string array of all P-Class problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ProblemNavigationData.ByComplexity("P"), options);
    }

}

// Get only NP-Hard problems
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPHard_ProblemsRefactorController : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns all NPHard problems </summary>
    ///<response code="200">Returns string array of all NP-Hard problems</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]

    public String getDefault()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ProblemNavigationData.ByComplexity("NPHard"), options);
    }

}

[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Problems)")]
#pragma warning disable CS1591

public class NPC_NavGraph : ControllerBase {
#pragma warning restore CS1591


    ///<summary>Returns all problems reachable from given problem via reductions </summary>
    ///<param name="chosenProblem" example="SAT3">NP-Complete problem name</param>
    ///<response code="200">Returns string array of NP-Complete problems</response>

    // Backed by ReductionGraphData (see Nav_Reductions.cs) so this view cannot
    // drift from /Navigation/Reductions. Response shape preserved for callers.
    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("availableReductions")]
    public string getConnectedProblems([FromQuery]string chosenProblem){
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ReductionGraphData.ReachableFrom(chosenProblem), options);
    }

    ///<summary>Returns reduction path from a given problem to another given problem </summary>
    ///<param name="reducingFrom" example="SAT3">NP-Complete problem name</param>
    ///<param name="reducingTo" example="ARCSET">NP-Complete problem name</param>
    ///<response code="200">Returns string array of NP-Complete reductions</response>

    // Backed by ReductionGraphData (see Nav_Reductions.cs).
    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet("reductionPath")]
    public string getPaths([FromQuery]string reducingFrom, string reducingTo){
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(ReductionGraphData.PathBetween(reducingFrom, reducingTo), options);
    }
}