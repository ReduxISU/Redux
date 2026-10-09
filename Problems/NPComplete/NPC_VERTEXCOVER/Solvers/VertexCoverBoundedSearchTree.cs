using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class VertexCoverBoundedSearchTree : ISolver<VERTEXCOVER, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Bounded Search Tree";
    public string solverDefinition { get; } = "Recursively branches on a remaining budget k: if no edges"
 + " remain, returns the empty set as a solution; if k has been exhausted and edges remain, returns"
 + " failure for that branch; otherwise selects an uncovered edge (u,v), first recursing with u added"
 + " to the cover and the budget decremented, and if that branch fails, recursing with v added instead."
 + " Returns the first successful branch's cover, or failure if both branches fail, meaning no vertex "
 + " cover of size at most k exists.";
    public string source { get; } = "David S. Johnson. 1974. Approximation algorithms for combinatorial problems. J. Comput. Syst. Sci. 9, 3 (December, 1974), 256–278. https://doi.org/10.1016/S0022-0000(74)80044-9";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Parameterized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(2^K * (n + m)), K = target cover size, n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public VertexCoverBoundedSearchTree() {

    }

    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // Steps: take one end of an uncovered edge, run out of budget, back a choice out. Recorder state lives only in this call.
    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var edges = new List<KeyValuePair<string, string>>(G.edges);
        var result = search(edges, G.K, new List<string>(), rec);
        if (result == null) {
            rec.Done(new NodeSet([]), false, $"No vertex cover of size {G.K} or less exists.");
            return "{}";
        }
        rec.Done(new NodeSet(result), true, $"{GraphSubsetFrames.Braces(result)} covers every edge, within K = {G.K}.");
        return "{" + string.Join(",", result) + "}";
    }

    // 'chosen' is the cover built along the current branch (a stack); the returned list is built on the way back out.
    private List<string>? search(List<KeyValuePair<string, string>> edges, int k, List<string> chosen, StepRecorder<NodeSet> rec) {
        if (edges.Count == 0) {
            return new List<string>();
        }
        if (k <= 0) {
            rec.Reject(() => new NodeSet(chosen), () => $"Out of budget with {edges.Count} edge{(edges.Count == 1 ? "" : "s")} still uncovered.");
            return null;
        }

        var e = edges[0];

        var withoutU = edges.Where(x => x.Key != e.Key && x.Value != e.Key).ToList();
        chosen.Add(e.Key);
        rec.Accept(() => new NodeSet(chosen),
            () => $"Edge {{{e.Key},{e.Value}}} isn't covered. Take {e.Key} ({k - 1} more allowed).", e.Key);
        var resultU = search(withoutU, k - 1, chosen, rec);
        if (resultU != null) {
            resultU.Add(e.Key);
            return resultU;
        }
        chosen.RemoveAt(chosen.Count - 1);
        rec.Backtrack(() => new NodeSet(chosen), () => $"Back out {e.Key}: no cover within budget follows from taking it.", e.Key);

        var withoutV = edges.Where(x => x.Key != e.Value && x.Value != e.Value).ToList();
        chosen.Add(e.Value);
        rec.Accept(() => new NodeSet(chosen),
            () => $"Take {e.Value} instead, the other end of {{{e.Key},{e.Value}}} ({k - 1} more allowed).", e.Value);
        var resultV = search(withoutV, k - 1, chosen, rec);
        if (resultV != null) {
            resultV.Add(e.Value);
            return resultV;
        }
        chosen.RemoveAt(chosen.Count - 1);
        rec.Backtrack(() => new NodeSet(chosen), () => $"Back out {e.Value}: neither end of {{{e.Key},{e.Value}}} works here.", e.Value);

        return null;
    }
}
