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