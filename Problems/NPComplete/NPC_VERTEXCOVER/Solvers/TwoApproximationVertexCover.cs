using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

// The steps Vertex Cover Approximation records: the Minimum Vertex Cover 2-approximation's Accept per edge picked (its
// steps, recorded by its loop), then a Done that says whether the cover fits in K.
class TwoApproximationVertexCover : ISolver<VERTEXCOVER, NodeSet>, IIncrementalSolver {

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
    public MathematicalFunction timeComplexity { get; } = new("E", ", E = |edges|");

    // --- Methods Including Constructors ---
    // See TwoApproximationMinimumVertexCover: a seed makes the random edge choices reproducible.
    private readonly int? _seed;

    public TwoApproximationVertexCover() {

    }

    public TwoApproximationVertexCover(int seed) {
        _seed = seed;
    }

    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // How the steps show the cover so far: its nodes, with the edge just picked and its two ends in focus (as the Minimum
    // Vertex Cover 2-approximation shows them; its loop records the steps).
    private static RunSteps<(List<string> Cover, KeyValuePair<string, string> Picked), NodeSet> NewSteps(StepRecorder<NodeSet> rec) => new(rec,
        // What every picture shows: the cover so far, and the edge just picked with its ends.
        partial: s => new NodeSet(s.Cover),
        focus: s => [s.Picked.Key, s.Picked.Value, GraphSubsetFrames.EdgeId(s.Picked.Key, s.Picked.Value)]);

    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec);
        // The inner solver watches this solver's flag, which only the time limit sets.
        var approx = new TwoApproximationMinimumVertexCover(_seed, () => timerHasExpired);
        var cover = approx.Cover(G.edges, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        if (cover.Count > G.K) {
            steps.Done(new NodeSet([]), false, $"This cover uses {cover.Count} nodes, more than K = {G.K}.");
            return "{}";
        }

        steps.Done(new NodeSet(cover), true, $"Every edge is covered with {cover.Count} nodes, within K = {G.K}.");
        return "{" + string.Join(",", cover) + "}";
    }

}
