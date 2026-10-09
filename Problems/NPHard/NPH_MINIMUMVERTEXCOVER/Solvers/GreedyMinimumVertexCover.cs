using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

class GreedyMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER, NodeSet> {

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
    public string complexity { get; } = "O(n * (n + m)), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public GreedyMinimumVertexCover() {

    }
    public string solve(MINIMUMVERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // Steps: one per node taken, with how many uncovered edges it covers. Recorder state lives only in this call.
    public string Solve(MINIMUMVERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var cover = Cover(G.edges, rec);
        rec.Done(new NodeSet(cover), true, cover.Count == 0
            ? "There are no edges to cover."
            : $"Every edge is covered with {cover.Count} nodes. Greedy isn't guaranteed to find the smallest cover.");
        return "{" + string.Join(",", cover) + "}";
    }

    /// <summary>The greedy cover of <paramref name="allEdges"/>, in the order nodes were taken. Records a step per node taken.</summary>
    internal List<string> Cover(IReadOnlyList<KeyValuePair<string, string>> allEdges, StepRecorder<NodeSet> rec) {
        var edges = new List<KeyValuePair<string, string>>(allEdges);
        var cover = new List<string>();

        while (edges.Count > 0) {
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
            rec.Accept(() => new NodeSet(cover),
                () => $"Take {best}: it covers {gain} more edge{(gain == 1 ? "" : "s")}, the most of any node left.", best);
        }

        return cover;
    }
}
