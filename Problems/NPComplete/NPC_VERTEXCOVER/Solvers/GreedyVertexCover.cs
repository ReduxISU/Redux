using API.Interfaces;
using System.Linq;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class GreedyVertexCover : ISolver<VERTEXCOVER>
{

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Max-Degree Greedy";
    public string solverDefinition { get; } = "Note: despite the name, this solver calls a different"
+ " algorithm internally. Repeats the following step until no edges remain: computes"
+ " the degree of every node with respect to the currently uncovered edges, selects the node with the"
+ " highest such degree (breaking ties by iteration order), adds it to the cover, and removes every"
+ " edge incident to it. Terminates once all edges are covered, returning the accumulated set of"
+ " selected nodes; does not use k, and unlike the maximal-matching approximation, offers no constant-"
+ " factor guarantee — its approximation ratio can be as bad as Theta(log n).";
    public string source { get; } = "David S. Johnson. 1974. Approximation algorithms for combinatorial problems. J. Comput. Syst. Sci. 9, 3 (December, 1974), 256–278. https://doi.org/10.1016/S0022-0000(74)80044-9";
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public string complexity { get; } = "O(n * (n + m)), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public GreedyVertexCover()
    {

    }
    public string solve(VERTEXCOVER G)
    {
        var mvc = new MINIMUMVERTEXCOVER();
        mvc.nodes = G.nodes;
        mvc.edges = G.edges;

        string certificate = new GreedyMinimumVertexCover().solve(mvc);

        int size = certificate == "{}" ? 0 : certificate.Trim('{', '}').Split(',').Length;

        if (size > G.K)
        {
            return "No solution found. Does not guarantee a solution does not exist.";
        }

        return certificate;
    }
}