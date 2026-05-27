using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

// Reflection-derived reduction graph and the endpoint that exposes it.
// Replaces the prior filesystem-walking controllers (All_Reductions,
// NPC_Reductions, Problem_Reductions[Refactor], PossibleReductions[Refactor],
// Reverse_Reductions). Type system is the source of truth; the map shape
// matches how consumers (LLM or GUI) traverse reductions.
//
// ReductionGraphData is also the backing store for the legacy
// NPC_NavGraph/availableReductions and NPC_NavGraph/reductionPath
// sub-endpoints (see Nav_Problems.cs) so views cannot drift.

// Top-level so Swagger emits "$ref": "#/components/schemas/ReductionEdge".
// Must not be nested inside ReductionGraphData — the CLR names nested types
// with '+' (e.g. "ReductionGraphData+Edge"), which is not a valid JSON pointer
// character and causes a Swagger UI resolver error.
public class ReductionEdge
{
    public string className { get; set; } = "";
    public string endpoint { get; set; } = "";
    public string inputType { get; set; } = "";
    public string outputType { get; set; } = "";
}

public static class ReductionGraphData
{
    // Alias so existing internal usages (Build, PathBetween, etc.) keep
    // compiling unchanged while the public API surface uses ReductionEdge.
    internal class Edge : ReductionEdge { }

    // from -> to -> [edges]. Keys are canonical IProblem class names
    // (e.g. "SAT3", "CLIQUE"). Built once at process start.
    public static readonly Dictionary<string, Dictionary<string, List<ReductionEdge>>> Graph = Build();

    private static Dictionary<string, Dictionary<string, List<ReductionEdge>>> Build()
    {
        var graph = new Dictionary<string, Dictionary<string, List<ReductionEdge>>>();
        foreach (var (_, type) in ProblemProvider.Reductions)
        {
            var generic = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>));
            if (generic == null) continue;
            var args = generic.GetGenericArguments();
            string from = args[0].Name;
            string to = args[1].Name;
            string className = type.Name;
            var edge = new ReductionEdge
            {
                className = className,
                endpoint = $"POST /ProblemProvider/reduce?reduction={className}",
                inputType = from,
                outputType = to,
            };
            if (!graph.ContainsKey(from))
                graph[from] = new Dictionary<string, List<ReductionEdge>>();
            if (!graph[from].ContainsKey(to))
                graph[from][to] = new List<ReductionEdge>();
            graph[from][to].Add(edge);
        }
        return graph;
    }

    // Resolve a caller-supplied name (any case) to its canonical key as it
    // appears in Graph. Searches source nodes first, then target nodes.
    public static string? ResolveKey(string name)
    {
        foreach (var k in Graph.Keys)
            if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return k;
        foreach (var outs in Graph.Values)
            foreach (var k in outs.Keys)
                if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return k;
        return null;
    }

    // Transitively reachable problems from source via BFS. Excludes source.
    public static List<string> ReachableFrom(string source)
    {
        var result = new List<string>();
        string? start = ResolveKey(source);
        if (start == null) return result;

        var visited = new HashSet<string> { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string cur = queue.Dequeue();
            if (!Graph.TryGetValue(cur, out var outs)) continue;
            foreach (var next in outs.Keys)
            {
                if (visited.Add(next))
                {
                    result.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return result;
    }

    // Shortest path from source to target as a list of hops; each hop is the
    // list of reduction class names available between consecutive problems.
    // Empty list if no path exists.
    public static List<List<string>> PathBetween(string source, string target)
    {
        string? s = ResolveKey(source);
        string? t = ResolveKey(target);
        if (s == null || t == null) return new List<List<string>>();
        if (string.Equals(s, t, StringComparison.OrdinalIgnoreCase)) return new List<List<string>>();

        var parent = new Dictionary<string, string>();
        var visited = new HashSet<string> { s };
        var queue = new Queue<string>();
        queue.Enqueue(s);
        bool found = false;
        while (queue.Count > 0 && !found)
        {
            string cur = queue.Dequeue();
            if (!Graph.TryGetValue(cur, out var outs)) continue;
            foreach (var next in outs.Keys)
            {
                if (!visited.Add(next)) continue;
                parent[next] = cur;
                if (string.Equals(next, t, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
                queue.Enqueue(next);
            }
        }
        if (!found) return new List<List<string>>();

        var hops = new List<List<string>>();
        string node = t;
        while (parent.TryGetValue(node, out string? prev))
        {
            hops.Add(Graph[prev][node].Select(e => e.className).ToList());
            node = prev;
        }
        hops.Reverse();
        return hops;
    }
}

[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Reductions)")]
#pragma warning disable CS1591
public class ReductionsController : ControllerBase
{
#pragma warning restore CS1591

    /// <summary>Returns the reduction graph as an adjacency map: from -> to -> list of reduction edges.</summary>
    /// <remarks>
    /// Each edge is self-describing (className, endpoint, inputType, outputType) so an LLM
    /// can chain reductions by matching outputType of step n to inputType of step n+1.
    /// Omit both source and target to get the full graph for multi-step planning.
    /// </remarks>
    /// <param name="source">Optional. Filter to edges originating at this problem (class name, e.g. "CLIQUE"). Case-insensitive.</param>
    /// <param name="target">Optional. Filter to edges ending at this problem (class name, e.g. "SAT3"). Case-insensitive.</param>
    /// <response code="200">Adjacency map of reduction edges.</response>
    [ProducesResponseType(typeof(Dictionary<string, Dictionary<string, List<ReductionEdge>>>), 200)]
    [HttpGet]
    public string getDefault([FromQuery] string? source = null, [FromQuery] string? target = null)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        if (source == null && target == null)
            return JsonSerializer.Serialize(ReductionGraphData.Graph, options);

        var result = new Dictionary<string, Dictionary<string, List<ReductionEdge>>>();
        foreach (var (from, tos) in ReductionGraphData.Graph)
        {
            if (source != null && !string.Equals(from, source, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (to, edges) in tos)
            {
                if (target != null && !string.Equals(to, target, StringComparison.OrdinalIgnoreCase)) continue;
                if (!result.ContainsKey(from)) result[from] = new Dictionary<string, List<ReductionEdge>>();
                result[from][to] = edges;
            }
        }
        return JsonSerializer.Serialize(result, options);
    }
}
