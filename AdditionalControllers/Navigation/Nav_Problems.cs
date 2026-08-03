using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

// Problem navigation resolved via reflection over ProblemProvider.Problems, so it no
// longer depends on the on-disk Problems/ folder layout. Mirrors the solver / verifier
// / visualization navigation (#317/#318/#331); those endpoints were converted earlier,
// this closes the gap for the problem-listing endpoints.
//
// A problem's complexity class is DECLARED (IProblem.complexityClass), not derived from
// its namespace. The namespace (API.Problems.<Folder>.<ProblemFolder>) used to be treated
// as the source of truth, but Problems/NPComplete/ misfiles at least a dozen entries (six
// are actually P, one is NPIntermediate, five are quantum query-complexity promise
// problems that aren't classical-hierarchy citizens at all) — the folder is a filing
// convention, not a correctness claim. See ComplexityClassCatalog below.
//
// Only top-level problems are listed; nested helper variants such as
// API.Problems.NPComplete.NPC_CLIQUE.Inherited (SipserClique) are excluded, matching the
// old top-level directory scan. That namespace-shape filter is still correct and is kept
// as-is — only the complexity-class *value* changed from derived to declared.

// className -> declared ComplexityClass's wire value. Built by instantiating every
// reflected problem type inside a per-type try/catch, exactly like
// VisualizationTypeCatalog (Nav_Visualizations.cs) and Nav_Batch.cs's InfoJson: one
// problem with a throwing/missing default constructor must not take down navigation
// at startup. Deliberately Lazy<>, never a static readonly field initializer that
// eagerly instantiates every problem type, for the same reason.
internal static class ComplexityClassCatalog
{
    internal static readonly Lazy<Dictionary<string, string>> ByClassName = new(Build);

    private static Dictionary<string, string> Build()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, type) in ProblemProvider.Problems)
        {
            try
            {
                if (Activator.CreateInstance(type) is IProblem instance)
                    result[type.Name] = instance.complexityClass.ToString();
            }
            catch
            {
                // Skip a problem that can't be default-constructed instead of
                // failing the whole catalog. It falls back to Unclassified at the
                // call site (ProblemNavigationData.Build).
            }
        }
        return result;
    }
}

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
            // (This namespace check is only used to decide which types are top-level
            // listable problems — it is no longer used to derive the complexity class.)
            string[] ns = (type.Namespace ?? "").Split('.');
            int i = Array.IndexOf(ns, "Problems");
            if (i < 0 || ns.Length != i + 3) continue;

            entries.Add(new ProblemEntry
            {
                className = type.Name,
                complexityClass = ComplexityClassCatalog.ByClassName.Value.TryGetValue(type.Name, out var cc)
                    ? cc
                    : nameof(ComplexityClass.Unclassified),
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