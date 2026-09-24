using API.Interfaces;
using System.Linq;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class VertexCoverBoundedSearchTree : ISolver<VERTEXCOVER> {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Bounded Search Tree";
    public string solverDefinition { get; } = "Recursively branches on a remaining budget k: if no edges"
 + " remain, returns the empty set as a solution; if k has been exhausted and edges remain, returns"
 + " failure for that branch; otherwise selects an uncovered edge (u,v), first recursing with u added"
 + " to the cover and the budget decremented, and if that branch fails, recursing with v added instead."
 + " Returns the first successful branch's cover, or failure if both branches fail, meaning no vertex "
 + " cover of size at most k exists.";
    public string source { get; } = "David S. Johnson. 1974. Approximation algorithms for combinatorial problems. J. Comput. Syst. Sci. 9, 3 (December, 1974), 256–278. https://doi.org/10.1016/S0022-0000(74)80044-9";
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Parameterized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(2^K * (n + m)), K = target cover size, n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public VertexCoverBoundedSearchTree() {

    }

    public string solve(VERTEXCOVER G) {
        var edges = new List<KeyValuePair<string, string>>(G.edges);
        var result = search(edges, G.K);
        return result == null ? "{}" : "{" + string.Join(",", result) + "}";
    }

    private List<string>? search(List<KeyValuePair<string, string>> edges, int k) {
        if (edges.Count == 0) {
            return new List<string>();
        }
        if (k <= 0) {
            return null;
        }

        var e = edges[0];

        var withoutU = edges.Where(x => x.Key != e.Key && x.Value != e.Key).ToList();
        var resultU = search(withoutU, k - 1);
        if (resultU != null) {
            resultU.Add(e.Key);
            return resultU;
        }

        var withoutV = edges.Where(x => x.Key != e.Value && x.Value != e.Value).ToList();
        var resultV = search(withoutV, k - 1);
        if (resultV != null) {
            resultV.Add(e.Value);
            return resultV;
        }

        return null;
    }
}