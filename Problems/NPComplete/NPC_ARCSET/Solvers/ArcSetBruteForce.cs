using System.Runtime.ConstrainedExecution;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_ARCSET.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_ARCSET.Solvers;

// The steps Arc Set Brute Force records: one per set of edges it tries removing, removing nothing first, then every set of
// 1 edge, 2 edges, ... up to K. A set is a Reject naming a cycle that survives its removal, or the Accept that ends the
// search; then the Done. The recorder's cap hides the rest of a long run.
class ArcSetBruteForce : ISolver<ARCSET, EdgeSet> {

    // --- Fields ---
    public string solverName { get; } = "Arc Set Brute Force";
    public string solverDefinition { get; } = @" This Solver is a brute force solver, which checks all combinations of k edges until a solution is found or its determined there is no solution";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. ChooseUpTo(K) enumerates edge subsets of size 1..K (K <= m,
    // but K can still be Theta(m), so the sum of C(m,i) terms is Theta(2^m) in the worst
    // case -- bounded by K the same way CutBruteForce is bounded by K, but here the base is
    // the edge count m rather than the node count n). Each candidate costs O(n^2 * m) to
    // verify (ArcSetVerifier's isACyclical reachability fixpoint).
    public MathematicalFunction timeComplexity { get; } = new("2^m * n^2 * m", ", n = |nodes|, m = |edges|");

    public string[] contributors { get; } = { "Alex Diviney", "Caleb Eardley", "Russell Phillips" };

    // --- Methods Including Constructors ---
    public ArcSetBruteForce() {

    }

    /**
    * Returns the set of edges that if removed from arcset would turn it acyclic
    */

    // How the steps show a candidate: the edges it removes (as arc ids), every one of them in focus.
    private static RunSteps<UtilCollection?, EdgeSet> NewSteps(StepRecorder<EdgeSet> rec) => new(rec,
        // What every picture shows: the edges removed (none for removing nothing).
        partial: removed => new EdgeSet(ArcIds(removed)),
        focus: removed => ArcIds(removed));

    // The arc ids of a set of edges, each edge written (from, to); none for removing nothing (null).
    private static string[] ArcIds(UtilCollection? removed) =>
        removed == null ? [] : removed.ToList().Select(e => LayeredFrames.ArcId(e[0].ToString(), e[1].ToString())).ToArray();

    public string solve(ARCSET arc) => Solve(arc, StepRecorder<EdgeSet>.Off);

    // Tries removing nothing, then every set of 1 edge, 2 edges, ... up to K, until what is left has no cycle.
    public string Solve(ARCSET arc, StepRecorder<EdgeSet> rec) {
        var steps = NewSteps(rec);
        UtilCollectionGraph graph = arc.graph;
        // The verifier only says no, so a cycle that survives is found for the steps alone (a plain solve builds no finder).
        var cycle = rec.Enabled ? LayeredFrames.SurvivingCycle(ArcSetDefaultVisualization.Spec(arc)) : null;
        int tried = 0;

        // Removing nothing comes first: an acyclic graph needs no edge removed.
        tried++;
        if (arc.defaultVerifier.verify(arc, "{}")) {
            steps.Accept(null, $"Try removing nothing. No cycle is left.");
            steps.Done(new EdgeSet([]), true, $"The graph has no cycle, so removing nothing is enough. Found after 1 try.");
            return "{}";
        }
        steps.Reject(null, $"Try removing nothing: the cycle {cycle!([], [])} is there.");

        foreach (UtilCollection potentialSolution in graph.Edges.ChooseUpTo(arc.K)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            string certificate = potentialSolution.ToString();
            tried++;
            if (arc.defaultVerifier.verify(arc, certificate)) {
                steps.Accept(potentialSolution, $"Try removing {certificate}. No cycle is left.");
                steps.Done(new EdgeSet(ArcIds(potentialSolution)), true,
                    $"Removing {certificate} leaves no cycle, within K = {arc.K}. Found after {Words.Count(tried, "try", "tries")}.");
                return certificate;
            }
            steps.Reject(potentialSolution, $"Try removing {certificate}: the cycle {cycle!(ArcIds(potentialSolution), [])} is still there.");
        }
        // Removing nothing failed above, and so did every set of 1 to K edges.
        steps.Done(new EdgeSet([]), false,
            $"Checked every way to remove up to K = {Words.Count(arc.K, "edge")} ({Words.Count(tried, "try", "tries")}). A cycle always survives.");
        return "{}";
    }
}
