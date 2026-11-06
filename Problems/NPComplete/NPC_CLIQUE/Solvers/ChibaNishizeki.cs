using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

// The steps Chiba-Nishizeki records:
//   - a Try for each start vertex, in descending degree order;
//   - an Accept for each vertex added to the growing clique, saying how many vertices are joined to every member so far;
//   - a Reject for a vertex the count rules out (too few vertices would be left to join it);
//   - a Backtrack when it backs a vertex out, and when it drops a start vertex that no clique grows from;
//   - the Done, with the clique or none.

class ChibaNishizeki : ISolver<CLIQUE, NodeSet>, IIncrementalSolver {

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
    public MathematicalFunction timeComplexity { get; } = new("k * a^(k-2) * m", ", k = clique size, a = arboricity, m = |edges|");

    // --- Methods Including Constructors ---
    public ChibaNishizeki() {

    }

    // How the steps show the search: the clique grown so far, and the vertex the step is about.
    private static RunSteps<(List<string> Clique, string Vertex), NodeSet> NewSteps(StepRecorder<NodeSet> rec) => new(rec,
        // What every picture shows: the clique so far, and the vertex started from, added, skipped or backed out.
        partial: s => new NodeSet(s.Clique),
        focus: s => [s.Vertex]);

    public string solve(CLIQUE clique) => Solve(clique, StepRecorder<NodeSet>.Off);

    public string Solve(CLIQUE clique, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec);

        Dictionary<string, HashSet<string>> adj = BuildAdjacency(clique.nodes, clique.edges);

        // 1. Sort vertices by degree, descending (per the original paper).
        List<string> order = adj.Keys.OrderByDescending(v => adj[v].Count).ToList();
        Dictionary<string, int> positionOf = order.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);

        HashSet<string> removed = new HashSet<string>();

        // 2. For each vertex v, in order, look for a (k-1)-clique among its later neighbors.
        foreach (string v in order) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            HashSet<string> laterNeighbors = new HashSet<string>(
                adj[v].Where(u => !removed.Contains(u) && positionOf[u] > positionOf[v])
            );

            List<string> partial = new List<string> { v };
            steps.Try((partial, v), $"Start from {v}: look for a clique of size {clique.K} among its {laterNeighbors.Count} later neighbors.");
            if (clique.K == 1) {
                steps.Done(new NodeSet([v]), true, $"{{{v}}} is a clique of size 1.");
                return "{" + v + "}";
            }

            HashSet<string>? found = FindClique(laterNeighbors, partial, clique.K - 1, adj, steps);
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            if (found != null) {
                steps.Done(new NodeSet(found), true, $"{GraphSubsetFrames.Braces(found)} is a clique of size {clique.K}.");
                return "{" + string.Join(",", found) + "}";
            }

            // Remove v from further consideration to avoid rediscovering cliques already ruled out.
            removed.Add(v);
            // Nothing is kept: the next start vertex begins a new clique.
            steps.Backtrack((partial, v), $"No clique of size {clique.K} contains {v}. Remove it and move on.", partial: () => new NodeSet([]));
        }

        steps.Done(new NodeSet([]), false, $"No clique of size {clique.K} exists.");
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
        Dictionary<string, HashSet<string>> adj, RunSteps<(List<string> Clique, string Vertex), NodeSet> steps) {

        if (remaining == 0) {
            return new HashSet<string>(partial);
        }

        foreach (string u in candidates.OrderByDescending(v => adj[v].Count(n => candidates.Contains(n)))) {
            if (timerHasExpired) return null; // unwind; solve() returns the timed-out answer

            HashSet<string> nextCandidates = new HashSet<string>(
                candidates.Where(w => w != u && adj[u].Contains(w))
            );

            // Prune: not enough remaining candidates to complete the clique.
            // (Here remaining - 1 is at least 1, and when it is 1 no vertex is left, so "only ..." always goes with "are".)
            if (nextCandidates.Count < remaining - 1) {
                if (nextCandidates.Count == 0)
                    steps.Reject((partial, u),
                        $"Skip {u}: no vertex would be left to join it, but {remaining - 1} {(remaining - 1 == 1 ? "is" : "are")} needed.");
                else
                    steps.Reject((partial, u),
                        $"Skip {u}: only {Words.Count(nextCandidates.Count, "vertex", "vertices")} would be left to join it, but {remaining - 1} are needed.");
                continue;
            }

            partial.Add(u);
            if (nextCandidates.Count == 0)
                steps.Accept((partial, u), $"Add {u} to the clique. No vertex is joined to every member so far.");
            else
                steps.Accept((partial, u),
                    $"Add {u} to the clique. {Words.Count(nextCandidates.Count, "vertex", "vertices")} {(nextCandidates.Count == 1 ? "is" : "are")} joined to every member so far.");
            HashSet<string>? result = FindClique(nextCandidates, partial, remaining - 1, adj, steps);
            if (result != null) {
                return result;
            }
            partial.RemoveAt(partial.Count - 1);
            steps.Backtrack((partial, u), $"Back out {u}: the clique cannot be completed from here.");
        }

        return null;
    }
}