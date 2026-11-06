using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

// The steps Minimum Vertex Cover Max-Degree Greedy records: an Accept per node taken, saying how many uncovered edges it
// covers, then the Done. Vertex Cover Greedy (GreedyVertexCover) runs the same loop and records the same steps, with its
// own Done that checks the cover against K.
class GreedyMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER, NodeSet>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Minimum Vertex Cover Max-Degree Greedy";
    public string solverDefinition { get; } = "Repeats the following step until no edges remain: computes"
 + " the degree of every node with respect to the currently uncovered edges, selects the node with the"
 + " highest such degree (breaking ties by iteration order), adds it to the cover, and removes every"
 + " edge incident to it. Terminates once all edges are covered, returning the accumulated set of"
 + " selected nodes; does not use k, and unlike the maximal-matching approximation, offers no constant-"
 + " factor guarantee — its approximation ratio can be as bad as Theta(log n).";
    public string source { get; } = "David S. Johnson. 1974. Approximation algorithms for combinatorial problems. J. Comput. Syst. Sci. 9, 3 (December, 1974), 256–278. https://doi.org/10.1016/S0022-0000(74)80044-9";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("n * (n + m)", ", n = |nodes|, m = |edges|");
    // Set by a solver that calls this one (GreedyVertexCover), so it stops at the caller's time limit too.
    internal Func<bool>? stop;

    // --- Methods Including Constructors ---
    public GreedyMinimumVertexCover() {

    }
    // How the steps show the cover so far: its nodes, and the node just taken in focus.
    private static RunSteps<(List<string> Cover, string Taken), NodeSet> NewSteps(StepRecorder<NodeSet> rec) => new(rec,
        // What every picture shows: the cover so far, and the node just taken.
        partial: s => new NodeSet(s.Cover),
        focus: s => [s.Taken]);

    public string solve(MINIMUMVERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    public string Solve(MINIMUMVERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec);
        var cover = Cover(G.edges, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (cover.Count == 0)
            steps.Done(new NodeSet(cover), true, $"There are no edges to cover.");
        else
            steps.Done(new NodeSet(cover), true, $"Every edge is covered with {cover.Count} nodes. Greedy isn't guaranteed to find the smallest cover.");
        return "{" + string.Join(",", cover) + "}";
    }

    /// <summary>
    /// The greedy cover of <paramref name="allEdges"/>, in the order nodes were taken: take the node that touches the most
    /// uncovered edges, drop the edges it covers, and repeat until none are left. Records an Accept per node taken.
    /// </summary>
    internal List<string> Cover(IReadOnlyList<KeyValuePair<string, string>> allEdges, RunSteps<(List<string> Cover, string Taken), NodeSet> steps) {
        var edges = new List<KeyValuePair<string, string>>(allEdges);
        var cover = new List<string>();

        while (edges.Count > 0) {
            // At the time limit, stop; the caller sees its flag and returns the timed-out answer.
            if (timerHasExpired || (stop != null && stop())) break;
            // Each node's degree among the edges still uncovered.
            var degree = new Dictionary<string, int>();
            foreach (var e in edges) {
                if (!degree.ContainsKey(e.Key)) degree[e.Key] = 0;
                if (!degree.ContainsKey(e.Value)) degree[e.Value] = 0;
                degree[e.Key]++;
                degree[e.Value]++;
            }

            string best = degree.OrderByDescending(kv => kv.Value).First().Key;
            cover.Add(best);
            edges = edges.Where(e => e.Key != best && e.Value != best).ToList();
            int gain = degree[best];
            steps.Accept((cover, best), $"Take {best}: it covers {gain} more edge{(gain == 1 ? "" : "s")}, the most of any node left.");
        }

        return cover;
    }
}
