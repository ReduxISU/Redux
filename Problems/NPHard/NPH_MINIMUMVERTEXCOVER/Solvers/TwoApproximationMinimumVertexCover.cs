using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

class TwoApproximationMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER, NodeSet> {

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
    public string complexity { get; } = "O(E), E = |edges|";

    // --- Methods Including Constructors ---
    // When set, the random choice of edges is reproducible (tests, replays). Unset keeps the original behaviour:
    // a different random choice on every run. Private, so it does not appear in /info.
    private readonly int? _seed;

    public TwoApproximationMinimumVertexCover() {

    }

    public TwoApproximationMinimumVertexCover(int seed) {
        _seed = seed;
    }

    public string solve(MINIMUMVERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // Steps: one per edge picked, taking both of its ends. Recorder state lives only in this call.
    public string Solve(MINIMUMVERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var cover = Cover(G.edges, rec);
        rec.Done(new NodeSet(cover), true, cover.Count == 0
            ? "There are no edges to cover."
            : $"Every edge is covered with {cover.Count} nodes. This method never uses more than twice the smallest cover.");
        return "{" + string.Join(",", cover) + "}";
    }

    /// <summary>The 2-approximation cover of <paramref name="allEdges"/>. Records a step per edge picked.</summary>
    internal List<string> Cover(IReadOnlyList<KeyValuePair<string, string>> allEdges, StepRecorder<NodeSet> rec) {
        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>(allEdges);
        List<KeyValuePair<string, string>> C = new List<KeyValuePair<string, string>>(); //This becomes our maximal matching
        Random rnd = _seed is int seed ? new Random(seed) : new Random();
        List<string> leftoverNodes = new List<string>();
        while (edges.Count > 0) {
            int index = rnd.Next(edges.Count); //gets a random edge index
            KeyValuePair<string, string> edge = edges[index]; //gets a random edge
            KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(edge.Key, edge.Value); //makes previous line more explicit
            C.Add(fullEdge); //Adds the random edge to C. 
            if (!leftoverNodes.Contains(edge.Key)) leftoverNodes.Add(edge.Key);
            if (!leftoverNodes.Contains(edge.Value)) leftoverNodes.Add(edge.Value);
            rec.Accept(() => new NodeSet(leftoverNodes),
                () => $"Edge {{{edge.Key},{edge.Value}}} isn't covered yet. Take both ends.",
                edge.Key, edge.Value, GraphSubsetFrames.EdgeId(edge.Key, edge.Value));
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
