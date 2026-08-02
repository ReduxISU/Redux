using System.Runtime.ConstrainedExecution;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using SPADE;

namespace API.Problems.NPComplete.NPC_ARCSET.Solvers;
class ArcSetBruteForce : ISolver<ARCSET> {

    // --- Fields ---
    public string solverName {get;} = "Arc Set Brute Force Solver";
    public string solverDefinition {get;} = @" This Solver is a brute force solver, which checks all combinations of k edges until a solution is found or its determined there is no solution";
    public string source {get;} = "";
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. ChooseUpTo(K) enumerates edge subsets of size 1..K (K <= m,
    // but K can still be Theta(m), so the sum of C(m,i) terms is Theta(2^m) in the worst
    // case -- bounded by K the same way CutBruteForce is bounded by K, but here the base is
    // the edge count m rather than the node count n). Each candidate costs O(n^2 * m) to
    // verify (ArcSetVerifier's isACyclical reachability fixpoint).
    public string complexity { get; } = "O(2^m * n^2 * m), n = |nodes|, m = |edges|";

    public string[] contributors {get;} = { "Alex Diviney","Caleb Eardley","Russell Phillips"};

    // --- Methods Including Constructors ---
    public ArcSetBruteForce() {

    }

    /**
    * Returns the set of edges that if removed from arcset would turn it acyclic
    */

    public string solve(ARCSET arc){
        UtilCollectionGraph graph = arc.graph;

        foreach (UtilCollection potentialSolution in graph.Edges.ChooseUpTo(arc.K))
        {
            string certificate = potentialSolution.ToString();
            if (arc.defaultVerifier.verify(arc, certificate)) return certificate;
        }
        return "{}";
    }
}
