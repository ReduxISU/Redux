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

// className -> declared ReductionCost's wire value. Built the same way as
// ComplexityClassCatalog (Nav_Problems.cs): iterate ProblemProvider.Reductions,
// Activator.CreateInstance per-type try/catch, read instance.cost.ToString() off the
// constructed IReduction. One reduction with a throwing/missing default constructor
// must not take down the whole catalog.
internal static class ReductionCostCatalog {
        internal static readonly Lazy<Dictionary<string, string>> ByClassName = new(Build);

        private static Dictionary<string, string> Build() {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (_, type) in ProblemProvider.Reductions) {
                        try {
                                if (Activator.CreateInstance(type) is IReduction instance)
                                        result[type.Name] = instance.cost.ToString();
                        }
                        catch {
                                // Skip a reduction that can't be default-constructed instead of failing
                                // the whole catalog. It falls back to Unclassified at the call site
                                // (ReductionGraphData.Build).
                        }
                }
                return result;
        }
}

// Top-level so Swagger emits "$ref": "#/components/schemas/ReductionEdge".
// Must not be nested inside ReductionGraphData — the CLR names nested types
// with '+' (e.g. "ReductionGraphData+Edge"), which is not a valid JSON pointer
// character and causes a Swagger UI resolver error.

/// <summary>
/// A reduction graph edge, representing a reduction from one problem type to another.
/// </summary>

public class ReductionEdge {
        /// <summary>The name of the class implementing the reduction.</summary>
        /// <example>KarpVertexCoverToSetCover</example>
        public string className { get; set; } = "";
        /// <summary>The HTTP method and relative path that performs the reduction.</summary>
        /// <example>POST /ProblemProvider/reduce?reduction=KarpVertexCoverToSetCover</example>
        public string endpoint { get; set; } = "";
        /// <summary>The input problem type for this reduction.</summary>
        /// <example>VERTEXCOVER</example>
        public string inputType { get; set; } = "";
        /// <summary>The output problem type this reduction produces.</summary>
        /// <example>SETCOVER</example>
        public string outputType { get; set; } = "";
        /// <summary>The declared <see cref="ComplexityClass"/> wire value of <see cref="inputType"/>. Additive field for Phase 7 faceting; purely informational here.</summary>
        /// <example>NPComplete</example>
        public string fromComplexity { get; set; } = "";
        /// <summary>The declared <see cref="ComplexityClass"/> wire value of <see cref="outputType"/>. Additive field for Phase 7 faceting; purely informational here.</summary>
        /// <example>NPComplete</example>
        public string toComplexity { get; set; } = "";
        /// <summary>The declared <see cref="ReductionCost"/> wire value: how much this reduction blows up instance size (output vs input). Already-stringified, same Newtonsoft-enum-as-int mitigation as <see cref="fromComplexity"/>/<see cref="toComplexity"/>.</summary>
        /// <example>Linear</example>
        public string cost { get; set; } = "";
}

/// <summary>
/// Represents a reduction graph data structure as a dictionary of dictionaries.
/// </summary>
public static class ReductionGraphData {
        // Alias so existing internal usages (Build, PathBetween, etc.) keep
        // compiling unchanged while the public API surface uses ReductionEdge.
        internal class Edge : ReductionEdge { }

        // from -> to -> [edges]. Keys are canonical IProblem class names
        // (e.g. "SAT3", "CLIQUE"). Built once at process start.
        /// <summary>
        /// A static dictionary of reduction edges organized by source, destination
        /// and having a list of possible reductions for a given source/destination.
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, List<ReductionEdge>>> Graph = Build();

        // Reads the declared ComplexityClass off a problem type, per-type try/catch so a
        // throwing/missing default constructor degrades to Unclassified instead of
        // failing the whole graph. Backed by ComplexityClassCatalog (Nav_Problems.cs) so
        // this can never disagree with the Navigation/*Problems* endpoints.
        private static string DeclaredComplexity(Type problemType) =>
            ComplexityClassCatalog.ByClassName.Value.TryGetValue(problemType.Name, out var cc)
                ? cc
                : nameof(API.Interfaces.ComplexityClass.Unclassified);

        private static string DeclaredCost(string className) =>
            ReductionCostCatalog.ByClassName.Value.TryGetValue(className, out var cost)
                ? cost
                : nameof(API.Interfaces.ReductionCost.Unclassified);

        // Ascending cost rank for sorting parallel edges (same from->to pair, multiple
        // reduction classes) cheapest-first, so a caller that just takes edges[0] gets the
        // cheapest *declared* reduction automatically. Unclassified sorts last — an
        // undeclared cost is not evidence of being cheap. This only orders parallel edges
        // between a single (from, to) pair; it is intentionally NOT a re-weighting of
        // PathBetween's BFS (which is unweighted, by hop count only) — a weighted,
        // cheapest-multi-hop PathBetween is a deferred follow-up phase, not in scope here.
        private static readonly Dictionary<string, int> CostRank = new(StringComparer.OrdinalIgnoreCase) {
                [nameof(API.Interfaces.ReductionCost.Linear)] = 0,
                [nameof(API.Interfaces.ReductionCost.Quadratic)] = 1,
                [nameof(API.Interfaces.ReductionCost.Cubic)] = 2,
                [nameof(API.Interfaces.ReductionCost.HigherPolynomial)] = 3,
                [nameof(API.Interfaces.ReductionCost.Unclassified)] = 4,
        };

        private static Dictionary<string, Dictionary<string, List<ReductionEdge>>> Build() {
                var graph = new Dictionary<string, Dictionary<string, List<ReductionEdge>>>();
                foreach (var (_, type) in ProblemProvider.Reductions) {
                        var generic = type.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>));
                        if (generic == null) continue;

                        // Some reductions (e.g. SipserReduceToSAT3) are solution-mapping companions to
                        // another reduction, not general A->B reductions valid for any A instance --
                        // [NotAGeneralReduction] (Interfaces/ReductionInterface.cs) lets them opt out of
                        // being advertised as a navigable graph edge, instead of a hardcoded skip-list
                        // here. Checked via the attribute (pure reflection over the type), not by
                        // constructing an instance: a class excluded for this reason can have a default
                        // constructor that itself isn't safely callable (SipserReduceToSAT3's chains
                        // through `new CLIQUE()` and immediately calls reduce() on it).
                        if (type.GetCustomAttributes(typeof(NotAGeneralReductionAttribute), inherit: false).Length > 0)
                                continue;

                        var args = generic.GetGenericArguments();
                        string from = args[0].Name;
                        string to = args[1].Name;
                        string className = type.Name;
                        var edge = new ReductionEdge {
                                className = className,
                                endpoint = $"POST /ProblemProvider/reduce?reduction={className}",
                                inputType = from,
                                outputType = to,
                                fromComplexity = DeclaredComplexity(args[0]),
                                toComplexity = DeclaredComplexity(args[1]),
                                cost = DeclaredCost(className),
                        };
                        if (!graph.ContainsKey(from))
                                graph[from] = new Dictionary<string, List<ReductionEdge>>();
                        if (!graph[from].ContainsKey(to))
                                graph[from][to] = new List<ReductionEdge>();
                        graph[from][to].Add(edge);
                }

                // Sort parallel edges (multiple reduction classes between the same from/to
                // pair) cheapest-first. Stable sort preserves relative order among equal-cost
                // edges. Does not affect which (from, to) pairs exist in the graph, and does
                // not touch PathBetween's hop-count BFS — see CostRank's comment above.
                foreach (var tos in graph.Values)
                        foreach (var to in tos.Keys.ToList())
                                tos[to] = tos[to]
                                    .OrderBy(e => CostRank.TryGetValue(e.cost, out var r) ? r : int.MaxValue)
                                    .ToList();

                return graph;
        }

        /// <summary>Resolve a caller-supplied name (any case) to its canonical key as it
        /// appears in Graph. Searches source nodes first, then target nodes.</summary>
        public static string? ResolveKey(string name) {
                foreach (var k in Graph.Keys)
                        if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return k;
                foreach (var outs in Graph.Values)
                        foreach (var k in outs.Keys)
                                if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return k;
                return null;
        }

        /// <summary>Transitively reachable problems from source via BFS. Excludes source.</summary>
        public static List<string> ReachableFrom(string source) {
                var result = new List<string>();
                string? start = ResolveKey(source);
                if (start == null) return result;

                var visited = new HashSet<string> { start };
                var queue = new Queue<string>();
                queue.Enqueue(start);
                while (queue.Count > 0) {
                        string cur = queue.Dequeue();
                        if (!Graph.TryGetValue(cur, out var outs)) continue;
                        foreach (var next in outs.Keys) {
                                if (visited.Add(next)) {
                                        result.Add(next);
                                        queue.Enqueue(next);
                                }
                        }
                }
                return result;
        }

        /// <summary>
        /// Shortest path (by hop count, via BFS) from source to target. Each hop is the
        /// list of reduction class names available between two consecutive problems.
        /// </summary>
        /// <param name="source">The source problem (class name, case-insensitive).</param>
        /// <param name="target">The destination problem (class name, case-insensitive).</param>
        /// <returns>
        /// The hops from source to target, or an empty list if either name is unknown,
        /// source equals target, or no path exists.
        /// </returns>
        public static List<List<string>> PathBetween(string source, string target) {
                string? s = ResolveKey(source);
                string? t = ResolveKey(target);
                if (s == null || t == null) return new List<List<string>>();
                if (string.Equals(s, t, StringComparison.OrdinalIgnoreCase)) return new List<List<string>>();

                var parent = new Dictionary<string, string>();
                var visited = new HashSet<string> { s };
                var queue = new Queue<string>();
                queue.Enqueue(s);
                bool found = false;
                while (queue.Count > 0 && !found) {
                        string cur = queue.Dequeue();
                        if (!Graph.TryGetValue(cur, out var outs)) continue;
                        foreach (var next in outs.Keys) {
                                if (!visited.Add(next)) continue;
                                parent[next] = cur;
                                if (string.Equals(next, t, StringComparison.OrdinalIgnoreCase)) {
                                        found = true;
                                        break;
                                }
                                queue.Enqueue(next);
                        }
                }
                if (!found) return new List<List<string>>();

                var hops = new List<List<string>>();
                string node = t;
                while (parent.TryGetValue(node, out string? prev)) {
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
public class ReductionsController : ControllerBase {
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
        public string getDefault([FromQuery] string? source = null, [FromQuery] string? target = null) {
                var options = new JsonSerializerOptions { WriteIndented = true };
                if (source == null && target == null)
                        return JsonSerializer.Serialize(ReductionGraphData.Graph, options);

                var result = new Dictionary<string, Dictionary<string, List<ReductionEdge>>>();
                foreach (var (from, tos) in ReductionGraphData.Graph) {
                        if (source != null && !string.Equals(from, source, StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (var (to, edges) in tos) {
                                if (target != null && !string.Equals(to, target, StringComparison.OrdinalIgnoreCase)) continue;
                                if (!result.ContainsKey(from)) result[from] = new Dictionary<string, List<ReductionEdge>>();
                                result[from][to] = edges;
                        }
                }
                return JsonSerializer.Serialize(result, options);
        }
}
