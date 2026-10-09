using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

class CarraghanPardalos : ISolver<CLIQUE, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Clique Carraghan-Pardalos Branch and Bound";
    public string solverDefinition { get; } = "Orders vertices by ascending degree, then performs a"
    + " depth-first branch-and-bound search: at each node it maintains a partial clique and a candidate"
    + " set (vertices adjacent to all clique members so far). Before recursing on a candidate, it computes"
    + " the induced candidate set restricted to that vertex's neighbors and prunes immediately if the"
    + " partial clique plus the remaining candidates cannot possibly reach size k, avoiding exploration of"
    + " branches that cannot succeed.";
    public string source { get; } = "Randy Carraghan and Panos M. Pardalos. 1990. An exact algorithm for the maximum clique problem. Oper. Res. Lett. 9, 6 (November, 1990), 375–382. https://doi.org/10.1016/0167-6377(90)90057-C";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } =
        "https://doi.org/10.1016/0167-6377(90)90057-C";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(2^n), n = |nodes|";

    // --- Methods Including Constructors ---
    public CarraghanPardalos() {

    }

    public string solve(CLIQUE clique) => Solve(clique, StepRecorder<NodeSet>.Off);

    // Steps: add a vertex to the growing clique, skip a vertex the bound rules out, back a vertex out again.
    // Recorder state lives only in this call.
    public string Solve(CLIQUE clique, StepRecorder<NodeSet> rec) {

        Dictionary<string, HashSet<string>> adj = BuildAdjacency(clique.nodes, clique.edges);

        // 1. Order vertices by ascending degree (per the original paper).
        List<string> order = adj.Keys.OrderBy(v => adj[v].Count).ToList();

        HashSet<string> candidates = new HashSet<string>(order);
        List<string> partial = new List<string>();

        HashSet<string>? found = Expand(candidates, partial, clique.K, adj, rec);

        if (found == null) {
            rec.Done(new NodeSet([]), false, $"No clique of size {clique.K} exists.");
            return "{}";
        }
        rec.Done(new NodeSet(found), true, $"{GraphSubsetFrames.Braces(found)} is a clique of size {clique.K}.");
        return "{" + string.Join(",", found) + "}";
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

    // Branch-and-bound search: extends 'partial' using vertices from 'candidates', pruning whenever
    // the partial clique plus remaining candidates can no longer reach size k.
    private HashSet<string>? Expand(
        HashSet<string> candidates, List<string> partial, int k, Dictionary<string, HashSet<string>> adj, StepRecorder<NodeSet> rec) {

        if (partial.Count == k) {
            return new HashSet<string>(partial);
        }

        if (partial.Count + candidates.Count < k) {
            return null;
        }

        foreach (string v in candidates.ToList()) {

            HashSet<string> newCandidates = new HashSet<string>(
                candidates.Where(u => u != v && adj[v].Contains(u))
            );

            // Bound: skip v if the resulting branch cannot possibly reach size k.
            if (partial.Count + 1 + newCandidates.Count < k) {
                rec.Reject(() => new NodeSet(partial),
                    () => $"Skip {v}: with it only {partial.Count + 1 + newCandidates.Count} vertices could ever join, but the clique needs {k}.", v);
                candidates.Remove(v);
                continue;
            }

            partial.Add(v);
            rec.Accept(() => new NodeSet(partial),
                () => $"Add {v} to the clique. {newCandidates.Count} vertices are joined to every member so far.", v);
            HashSet<string>? result = Expand(newCandidates, partial, k, adj, rec);
            if (result != null) {
                return result;
            }
            partial.RemoveAt(partial.Count - 1);
            rec.Backtrack(() => new NodeSet(partial), () => $"Back out {v}: no clique of size {k} grows from here.", v);
            candidates.Remove(v);
        }

        return null;
    }
}