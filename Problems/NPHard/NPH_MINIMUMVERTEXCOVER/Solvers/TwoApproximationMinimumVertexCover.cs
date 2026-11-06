using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

// The steps Minimum Vertex Cover Approximation records: an Accept per uncovered edge picked, taking both of its ends, then
// the Done. Vertex Cover Approximation (TwoApproximationVertexCover) runs the same loop and records the same steps, with
// its own Done that checks the cover against K.
class TwoApproximationMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER, NodeSet>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Minimum Vertex Cover Approximation";
    public string solverDefinition { get; } = "This approximation solver is a naive solver for Minimum Vertex Cover that does not have a clear origination, although there have been many improvements upon it"
    + " published. It repeatedly picks an arbitrary remaining edge, adds both endpoints to the cover, and"
    + " removes all edges incident to either endpoint, until no edges remain. It returns a cover of size at"
    + " most 2n when the optimal solution is n.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford (2001) [1990]. 'Section 35.1: The vertex-cover problem'. Introduction to Algorithms (2nd ed.). MIT Press and McGraw-Hill. pp. 1024–1027. ISBN 0-262-03293-7.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } =
        "https://www.cs.mcgill.ca/~akroit/math/compsci/Cormen%20Introduction%20to%20Algorithms.pdf";
    public string[] contributors { get; } = { "Janita Aamir", "Alex Diviney" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("E", ", E = |edges|");

    // --- Methods Including Constructors ---
    // When set, the random choice of edges is reproducible (tests, replays). Unset keeps the original behaviour:
    // a different random choice on every run. Private, so it does not appear in /info.
    private readonly int? _seed;

    public TwoApproximationMinimumVertexCover() {

    }

    public TwoApproximationMinimumVertexCover(int seed) {
        _seed = seed;
    }

    // An outer solver's live time-limit flag (TwoApproximationVertexCover runs this solver).
    private readonly Func<bool>? outerTimerHasExpired;
    internal TwoApproximationMinimumVertexCover(int? seed, Func<bool> outerTimerHasExpired) {
        _seed = seed;
        this.outerTimerHasExpired = outerTimerHasExpired;
    }

    // How the steps show the cover so far: its nodes, with the edge just picked and its two ends in focus.
    private static RunSteps<(List<string> Cover, KeyValuePair<string, string> Picked), NodeSet> NewSteps(StepRecorder<NodeSet> rec) => new(rec,
        // What every picture shows: the cover so far, and the edge just picked with its ends.
        partial: s => new NodeSet(s.Cover),
        focus: s => [s.Picked.Key, s.Picked.Value, GraphSubsetFrames.EdgeId(s.Picked.Key, s.Picked.Value)]);

    public string solve(MINIMUMVERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    public string Solve(MINIMUMVERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec);
        var cover = Cover(G.edges, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (cover.Count == 0)
            steps.Done(new NodeSet(cover), true, $"There are no edges to cover.");
        else
            steps.Done(new NodeSet(cover), true, $"Every edge is covered with {cover.Count} nodes. This method never uses more than twice the smallest cover.");
        return "{" + string.Join(",", cover) + "}";
    }

    /// <summary>
    /// The 2-approximation cover of <paramref name="allEdges"/>: pick a random uncovered edge, take both of its ends, drop
    /// every edge they touch, and repeat until none are left. The edges picked form a maximal matching, and any cover needs
    /// one end of each, so the cover is at most twice the smallest. Records an Accept per edge picked.
    /// </summary>
    internal List<string> Cover(IReadOnlyList<KeyValuePair<string, string>> allEdges,
        RunSteps<(List<string> Cover, KeyValuePair<string, string> Picked), NodeSet> steps) {
        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>(allEdges);
        List<KeyValuePair<string, string>> C = new List<KeyValuePair<string, string>>(); //This becomes our maximal matching
        Random rnd = _seed is int seed ? new Random(seed) : new Random();
        List<string> leftoverNodes = new List<string>();
        while (edges.Count > 0) {
            // At the time limit, stop; the caller sees its flag and returns the timed-out answer.
            if (timerHasExpired || outerTimerHasExpired?.Invoke() == true) break;
            int index = rnd.Next(edges.Count); //gets a random edge index
            KeyValuePair<string, string> edge = edges[index]; //gets a random edge
            KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(edge.Key, edge.Value); //makes previous line more explicit
            C.Add(fullEdge); //Adds the random edge to C. 
            if (!leftoverNodes.Contains(edge.Key)) leftoverNodes.Add(edge.Key);
            if (!leftoverNodes.Contains(edge.Value)) leftoverNodes.Add(edge.Value);
            steps.Accept((leftoverNodes, edge), $"Edge {{{edge.Key},{edge.Value}}} isn't covered yet. Take both ends.");
            foreach (KeyValuePair<string, string> e in new List<KeyValuePair<string, string>>(edges)) { //For the random edge {u,v}, remove every edge in vertexcover that has a node u or v.

                if (e.Key.Equals(edge.Key)) {
                    edges.Remove(e);
                }
                if (e.Key.Equals(edge.Value)) {
                    edges.Remove(e);

                }
                if (e.Value.Equals(edge.Key)) {
                    edges.Remove(e);
                }
                if (e.Value.Equals(edge.Value)) {
                    KeyValuePair<string, string> rmEdge = new KeyValuePair<string, string>(edge.Key, edge.Value);
                    edges.Remove(e);
                }
            }
        }

        return leftoverNodes;
    }

}
