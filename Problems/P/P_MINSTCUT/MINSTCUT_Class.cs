using API.Interfaces;
using API.Problems.P.P_MINSTCUT.Solvers;
using API.Problems.P.P_MINSTCUT.Verifiers;
using API.Problems.P.P_MINSTCUT.Visualizations;
using SPADE;

namespace API.Problems.P.P_MINSTCUT;

class MINSTCUT : IGraphProblem<MinSTCutSolver, MinSTCutVerifier, MinSTCutVisualization, UtilCollectionGraph> {
    public string problemName { get; } = "Minimum S-T Cut";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Minimum_cut";
    public string formalDefinition { get; } = "MinSTCut = {<G,s,t> | G is a weighted directed graph with source s and sink t} — find the partition of V into S (containing s) and T (containing t) minimizing the total capacity of edges directed from S to T.";
    public string problemDefinition { get; } = "Given a weighted directed graph with non-negative edge capacities, a source node s, and a sink node t, find a partition of the vertices into S (containing s) and T (containing t) such that the total capacity of edges directed from S to T is minimized. By the Max-Flow Min-Cut theorem, this minimum cut capacity equals the maximum flow from s to t.";
    public string[] contributors { get; } = { "Michael Trosper" };
    // Declared, not derived. Minimum s-t cut is solvable in polynomial time
    // (Ford-Fulkerson / max-flow min-cut).
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;
    public ProblemType problemType { get; } = ProblemType.NetworkDesign;
    public string source { get; } = "Ford, L. R.; Fulkerson, D. R. (1956). Maximal flow through a network. Canadian Journal of Mathematics, 8, 399–404.";
    public string sourceLink { get; } = "https://doi.org/10.4153/CJM-1956-045-5";
    public string wikiName { get; } = "";
    public static string _defaultInstance { get; } = "({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = "Weighted directed graph with source and sink: ({nodes},{((u,v),w),...},source,target) — e.g. ({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)";
    public string certificateFormat { get; } = "Set of vertices on the S side of the minimum cut (must contain source, must not contain target): {node,...} — e.g. {1,2}";

    private List<string> _nodes = new();
    private List<(string from, string to, int weight)> _edges = new();

    public string sourceNode { get; private set; } = string.Empty;
    public string targetNode { get; private set; } = string.Empty;

    public MinSTCutSolver defaultSolver { get; } = new MinSTCutSolver();
    public MinSTCutVerifier defaultVerifier { get; } = new MinSTCutVerifier();
    public MinSTCutVisualization defaultVisualization { get; } = new MinSTCutVisualization();
    public UtilCollectionGraph graph { get; set; }

    public List<string> nodes { get => _nodes; set => _nodes = value; }
    public List<(string from, string to, int weight)> edges { get => _edges; set => _edges = value; }

    public MINSTCUT() : this(_defaultInstance) { }

    public MINSTCUT(string instanceString) {
        instance = instanceString;

        List<string> outerParts = SplitOuterTuple(instanceString);

        string graphPart;
        if (outerParts.Count == 4) {
            graphPart = $"({outerParts[0]},{outerParts[1]})";
            sourceNode = outerParts[2];
            targetNode = outerParts[3];
        } else if (outerParts.Count == 3 && LooksLikeTuple(outerParts[0])) {
            graphPart = outerParts[0];
            sourceNode = outerParts[1];
            targetNode = outerParts[2];
        } else {
            throw new InvalidOperationException("Invalid Minimum S-T Cut instance format. Expected ({nodes},{((u,v),w),...},source,target).");
        }

        StringParser sp = new("{(N,E) | N is set, E subset {(e,w) | e is N cross N, w is int}}");
        sp.parse(graphPart);

        nodes = sp["N"].ToList().Select(n => n.ToString()).ToList();
        edges = sp["E"].ToList().Select(edge => {
            string from = edge[0][0].ToString();
            string to = edge[0][1].ToString();
            int weight = int.Parse(edge[1].ToString());
            return (from, to, weight);
        }).ToList();

        graph = new UtilCollectionGraph(sp["N"], sp["E"]);

        if (!nodes.Contains(sourceNode))
            throw new InvalidOperationException($"Source node '{sourceNode}' is not in the node set.");
        if (!nodes.Contains(targetNode))
            throw new InvalidOperationException($"Target node '{targetNode}' is not in the node set.");
    }

    private static List<string> SplitOuterTuple(string input) {
        string trimmed = input.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
            return new List<string>();

        string inner = trimmed[1..^1];
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        int parenDepth = 0;
        int braceDepth = 0;

        foreach (char ch in inner) {
            if (ch == ',' && parenDepth == 0 && braceDepth == 0) {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
            switch (ch) {
                case '(': parenDepth++; break;
                case ')': parenDepth--; break;
                case '{': braceDepth++; break;
                case '}': braceDepth--; break;
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString().Trim());

        return parts;
    }

    private static bool LooksLikeTuple(string value) => value.TrimStart().StartsWith("(");
}
