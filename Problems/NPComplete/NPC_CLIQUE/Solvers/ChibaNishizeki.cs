using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

class ChibaNishizeki : ISolver<CLIQUE, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Chiba-Nishizeki k-Clique Listing Algorithm";
    public string solverDefinition { get; } = "Sorts vertices by degree in descending order. For each vertex v"
    + " (in that order), builds the induced subgraph on v's neighbors that appear later in the ordering, then"
    + " recurses into that induced subgraph searching for a clique of size k-1, which combined with v forms a"
    + " clique of size k. After v is fully processed, it is removed from the graph to avoid rediscovering the"
    + " same clique through a different starting vertex. Runs in O(k * a^(k-2) * m) time, where a is the graph's"
    + " arboricity and m is the number of edges.";
    public string source { get; } = "Chiba, N., & Nishizeki, T. (1985). Arboricity and subgraph listing"
    + " algorithms. SIAM Journal on Computing, 14(1), 210-223. https://doi.org/10.1137/0214017";
    public string sourceFile { get; } = SourceFile.Path();
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

    public string solve(CLIQUE clique) => Solve(clique, StepRecorder<NodeSet>.Off);

    // Steps: start from a vertex, add a vertex to the growing clique, skip one the count rules out, back one out,
    // and drop a start vertex once nothing grows from it. Recorder state lives only in this call.
    public string Solve(CLIQUE clique, StepRecorder<NodeSet> rec) {

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

            rec.Try(() => new NodeSet([v]),
                () => $"Start from {v}: look for a clique of size {clique.K} among its {laterNeighbors.Count} later neighbors.", v);
            if (clique.K == 1) {
                rec.Done(new NodeSet([v]), true, $"{{{v}}} is a clique of size 1.");
                return "{" + v + "}";
            }

            List<string> partial = new List<string> { v };
            HashSet<string>? found = FindClique(laterNeighbors, partial, clique.K - 1, adj, rec);

            if (found != null) {
                rec.Done(new NodeSet(found), true, $"{GraphSubsetFrames.Braces(found)} is a clique of size {clique.K}.");
                return "{" + string.Join(",", found) + "}";
            }

            // Remove v from further consideration to avoid rediscovering cliques already ruled out.
            removed.Add(v);
            rec.Backtrack(() => new NodeSet([]), () => $"No clique of size {clique.K} contains {v}. Remove it and move on.", v);
        }

        rec.Done(new NodeSet([]), false, $"No clique of size {clique.K} exists.");
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
        Dictionary<string, HashSet<string>> adj, StepRecorder<NodeSet> rec) {

        if (remaining == 0) {
            return new HashSet<string>(partial);
        }

        foreach (string u in candidates.OrderByDescending(v => adj[v].Count(n => candidates.Contains(n)))) {

            HashSet<string> nextCandidates = new HashSet<string>(
                candidates.Where(w => w != u && adj[u].Contains(w))
            );

            // Prune: not enough remaining candidates to complete the clique.
            if (nextCandidates.Count < remaining - 1) {
                rec.Reject(() => new NodeSet(partial),
                    () => $"Skip {u}: only {nextCandidates.Count} vertices would be left to join it, but {remaining - 1} are needed.", u);
                continue;
            }

            partial.Add(u);
            rec.Accept(() => new NodeSet(partial),
                () => $"Add {u} to the clique. {nextCandidates.Count} vertices are joined to every member so far.", u);
            HashSet<string>? result = FindClique(nextCandidates, partial, remaining - 1, adj, rec);
            if (result != null) {
                return result;
            }
            partial.RemoveAt(partial.Count - 1);
            rec.Backtrack(() => new NodeSet(partial), () => $"Back out {u}: the clique cannot be completed from here.", u);
        }

        return null;
    }
}