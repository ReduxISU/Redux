using API.Interfaces;
using System.Linq;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class TwoApproximationVertexCover : ISolver<VERTEXCOVER> {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Approximation";
    public string solverDefinition { get; } = "Note: despite the name, this solver calls a different"
    + " algorithm internally. This approximation solver is a naive solver for Vertex Cover that does not have a clear origination, although there have been many improvements upon it"
    + " published. It repeatedly picks an arbitrary remaining edge, adds both endpoints to the cover, and"
    + " removes all edges incident to either endpoint, until no edges remain. It returns a cover of size at"
    + " most 2n when the optimal solution is n.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford (2001) [1990]. 'Section 35.1: The vertex-cover problem'. Introduction to Algorithms (2nd ed.). MIT Press and McGraw-Hill. pp. 1024–1027. ISBN 0-262-03293-7.";
    public string sourceLink { get; } =
        "https://www.cs.mcgill.ca/~akroit/math/compsci/Cormen%20Introduction%20to%20Algorithms.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public string complexity { get; } = "O(E), E = |edges|";

    // --- Methods Including Constructors ---
    public TwoApproximationVertexCover() {

    }

    public string solve(VERTEXCOVER G) {
        var mvc = new MINIMUMVERTEXCOVER();
        mvc.nodes = G.nodes;
        mvc.edges = G.edges;

        string certificate = new TwoApproximationMinimumVertexCover().solve(mvc);

        int size = certificate == "{}" ? 0 : certificate.Trim('{', '}').Split(',').Length;

        if (size > G.K) {
            return "No solution found. Does not guarantee a solution does not exist.";
        }

        return certificate;
    }

}