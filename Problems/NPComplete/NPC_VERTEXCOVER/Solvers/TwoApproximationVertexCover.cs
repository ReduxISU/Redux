using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class TwoApproximationVertexCover : ISolver<VERTEXCOVER, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Approximation";
    public string solverDefinition { get; } = "Runs the Minimum Vertex Cover 2-approximation solver and returns its cover if it has at most K nodes,"
    + " otherwise returns {}. The 2-approximation solver is a naive solver for Vertex Cover that does not have a clear origination, although there have been many improvements upon it"
    + " published. It repeatedly picks an arbitrary remaining edge, adds both endpoints to the cover, and"
    + " removes all edges incident to either endpoint, until no edges remain. It returns a cover of size at"
    + " most 2n when the optimal solution is n.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford (2001) [1990]. 'Section 35.1: The vertex-cover problem'. Introduction to Algorithms (2nd ed.). MIT Press and McGraw-Hill. pp. 1024–1027. ISBN 0-262-03293-7.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } =
        "https://www.cs.mcgill.ca/~akroit/math/compsci/Cormen%20Introduction%20to%20Algorithms.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public string complexity { get; } = "O(E), E = |edges|";

    // --- Methods Including Constructors ---
    // See TwoApproximationMinimumVertexCover: a seed makes the random edge choices reproducible.
    private readonly int? _seed;

    public TwoApproximationVertexCover() {

    }

    public TwoApproximationVertexCover(int seed) {
        _seed = seed;
    }

    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // Steps: the edges picked (as in the Minimum Vertex Cover solver this wraps), then whether the cover fits in K.
    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var approx = _seed is int seed ? new TwoApproximationMinimumVertexCover(seed) : new TwoApproximationMinimumVertexCover();
        var cover = approx.Cover(G.edges, rec);

        if (cover.Count > G.K) {
            rec.Done(new NodeSet([]), false, $"This cover uses {cover.Count} nodes, more than K = {G.K}.");
            return "{}";
        }

        rec.Done(new NodeSet(cover), true, $"Every edge is covered with {cover.Count} nodes, within K = {G.K}.");
        return "{" + string.Join(",", cover) + "}";
    }

}
