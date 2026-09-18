using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

class ChibaNishizeki : ISolver<CLIQUE> {

    // --- Fields ---
    public string solverName { get; } = "Chiba-Nishizeki k-Clique Listing";
    public string solverDefinition { get; } = "Sorts vertices by degree in descending order. For each vertex v"
    + " (in that order), builds the induced subgraph on v's neighbors that appear later in the ordering, then"
    + " recurses into that induced subgraph searching for a clique of size k-1, which combined with v forms a"
    + " clique of size k. After v is fully processed, it is removed from the graph to avoid rediscovering the"
    + " same clique through a different starting vertex. Runs in O(k * a^(k-2) * m) time, where a is the graph's"
    + " arboricity and m is the number of edges.";
    public string source { get; } = "Chiba, N., & Nishizeki, T. (1985). Arboricity and subgraph listing"
    + " algorithms. SIAM Journal on Computing, 14(1), 210-223. https://doi.org/10.1137/0214017";
    public string sourceLink { get; } =
        "https://doi.org/10.1137/0214017";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(k * a^(k-2) * m), k = clique size, a = arboricity, m = |edges|";

    // --- Methods Including Constructors ---
    public ChibaNishizeki() {

    }

    public string solve(CLIQUE clique) {

        Dictionary<string, HashSet<string>> adj = BuildAdjacency(clique.nodes, clique.edges);

        // 1. Sort vertices by degree, descending (per the original paper).
        List<string> order = adj.Keys.OrderByDescending(v => adj[v].Count).ToList();
        Dictionary<string, int> positionOf = order.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);

        HashSet<string> removed = new HashSet<string>();

        // 2. For each vertex v, in order, look for a (k-1)-clique among its later neighbors.
        foreach (string v in order) {
            HashSet<string> laterNeighbors = new HashSet<string>(
                adj[v].Where(u => !removed.Contains(u) && positionOf[u] > positionOf[v])
            );

            if (clique.K == 1) {
                return "{" + v + "}";
            }

            List<string> partial = new List<string> { v };
            HashSet<string>? found = FindClique(laterNeighbors, partial, clique.K - 1, adj, removed, positionOf);

            if (found != null) {
                return "{" + string.Join(",", found) + "}";
            }

            // Remove v from further consideration to avoid rediscovering cliques already ruled out.
            removed.Add(v);
        }

        return "{}";
    }

    private Dictionary<string, HashSet<string>> BuildAdjacency(
        List<string> nodes, List<KeyValuePair<string, string>> edges) {

        Dictionary<string, HashSet<string>> adj = nodes.ToDictionary(n => n, n => new HashSet<string>());
        foreach (KeyValuePair<string, string> e in edges) {
            adj[e.Key].Add(e.Value);
            adj[e.Value].Add(e.Key);
        }
        return adj;
    }

    // Recursively searches the induced subgraph 'candidates' for a clique of size 'remaining',
    // extending 'partial' with the result if found.
    private HashSet<string>? FindClique(
        HashSet<string> candidates, List<string> partial, int remaining,
        Dictionary<string, HashSet<string>> adj, HashSet<string> removed,
        Dictionary<string, int> positionOf) {

        if (remaining == 0) {
            return new HashSet<string>(partial);
        }

        foreach (string u in candidates.OrderByDescending(v => adj[v].Count(n => candidates.Contains(n)))) {

            HashSet<string> nextCandidates = new HashSet<string>(
                candidates.Where(w => w != u && adj[u].Contains(w))
            );

            // Prune: not enough remaining candidates to complete the clique.
            if (nextCandidates.Count < remaining - 1) continue;

            partial.Add(u);
            HashSet<string>? result = FindClique(nextCandidates, partial, remaining - 1, adj, removed, positionOf);
            if (result != null) {
                return result;
            }
            partial.RemoveAt(partial.Count - 1);
        }

        return null;
    }
}