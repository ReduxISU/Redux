using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

// The steps Vertex Cover Bounded Search Tree records, as it branches on the two ends of an uncovered edge:
//   - an Accept for each end it takes into the cover;
//   - a Reject when the budget runs out with edges still uncovered;
//   - a Backtrack when it backs an end out again;
//   - the Done, with the cover or none.
class VertexCoverBoundedSearchTree : ISolver<VERTEXCOVER, NodeSet>, IIncrementalSolver {

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
    // Declared, not derived. Parameterized by the budget K, and search() branches on the two endpoints
    // of an uncovered edge, abandoning a branch once the budget runs out, which is Backtracking.
    public SolverType solverType { get; } = SolverType.Backtracking | SolverType.Parameterized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("2^K * (n + m)", ", K = target cover size, n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public VertexCoverBoundedSearchTree() {

    }

    // How the steps show the search: the cover built along the current branch, and the end just taken or backed out
    // (null: none).
    private static RunSteps<(List<string> Chosen, string? Node), NodeSet> NewSteps(StepRecorder<NodeSet> rec) => new(rec,
        // What every picture shows: the cover so far, and the node the step is about.
        partial: s => new NodeSet(s.Chosen),
        focus: s => s.Node == null ? [] : [s.Node]);

    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec);
        var edges = new List<KeyValuePair<string, string>>(G.edges);
        var result = search(edges, G.K, new List<string>(), steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (result == null) {
            steps.Done(new NodeSet([]), false, $"No vertex cover of size {G.K} or less exists.");
            return "{}";
        }
        steps.Done(new NodeSet(result), true, $"{GraphSubsetFrames.Braces(result)} covers every edge, within K = {G.K}.");
        return "{" + string.Join(",", result) + "}";
    }

    // Every cover has one end of every edge, so for the first uncovered edge (u,v): take u and search on with one less
    // budget; if that fails, take v instead. 'chosen' is the cover built along the current branch (a stack); the
    // returned list is built on the way back out.
    private List<string>? search(List<KeyValuePair<string, string>> edges, int k, List<string> chosen,
        RunSteps<(List<string> Chosen, string? Node), NodeSet> steps) {
        // At the time limit, unwind as a failed branch; Solve() reports the stop.
        if (timerHasExpired) {
            return null;
        }
        if (edges.Count == 0) {
            return new List<string>();
        }
        if (k <= 0) {
            steps.Reject((chosen, null), $"Out of budget with {edges.Count} edge{(edges.Count == 1 ? "" : "s")} still uncovered.");
            return null;
        }

        var e = edges[0];

        var withoutU = edges.Where(x => x.Key != e.Key && x.Value != e.Key).ToList();
        chosen.Add(e.Key);
        steps.Accept((chosen, e.Key), $"Edge {{{e.Key},{e.Value}}} isn't covered. Take {e.Key} ({k - 1} more allowed).");
        var resultU = search(withoutU, k - 1, chosen, steps);
        if (resultU != null) {
            resultU.Add(e.Key);
            return resultU;
        }
        chosen.RemoveAt(chosen.Count - 1);
        steps.Backtrack((chosen, e.Key), $"Back out {e.Key}: no cover within budget follows from taking it.");

        var withoutV = edges.Where(x => x.Key != e.Value && x.Value != e.Value).ToList();
        chosen.Add(e.Value);
        steps.Accept((chosen, e.Value), $"Take {e.Value} instead, the other end of {{{e.Key},{e.Value}}} ({k - 1} more allowed).");
        var resultV = search(withoutV, k - 1, chosen, steps);
        if (resultV != null) {
            resultV.Add(e.Value);
            return resultV;
        }
        chosen.RemoveAt(chosen.Count - 1);
        steps.Backtrack((chosen, e.Value), $"Back out {e.Value}: neither end of {{{e.Key},{e.Value}}} works here.");

        return null;
    }
}
