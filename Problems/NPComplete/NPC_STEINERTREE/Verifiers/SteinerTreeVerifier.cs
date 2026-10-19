using API.Interfaces;
using API.Interfaces.Graphs;
using SPADE;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;


class SteinerTreeVerifier : IVerifier<STEINERTREE> {
    public const string CertificateGrammar = "{node,node},... | edges are distinct edges of G, at most K of them, forming a tree that includes every terminal node in R";
    public const string CertificateExample = "{{8,6},{6,1},{1,2},{2,3},{3,5}}";

    // --- Fields ---
    public string verifierName { get; } = "Default Steiner Tree Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Steiner Tree";
    public string source { get; } = "Andrija Sevaljevic";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SteinerTreeVerifier() {

    }

    private Dictionary<string, List<string>> BuildAdjacencyList(List<KeyValuePair<string, string>> edges) {
        Dictionary<string, List<string>> adjacencyList = new Dictionary<string, List<string>>();

        foreach (var edge in edges) {
            if (!adjacencyList.ContainsKey(edge.Key))
                adjacencyList[edge.Key] = new List<string>();

            if (!adjacencyList.ContainsKey(edge.Value))
                adjacencyList[edge.Value] = new List<string>();

            adjacencyList[edge.Key].Add(edge.Value);
            adjacencyList[edge.Value].Add(edge.Key);
        }

        return adjacencyList;
    }

    private void DFS(string v, HashSet<string> visited, Dictionary<string, List<string>> adjacencyList) {
        visited.Add(v);

        foreach (string neighbor in adjacencyList[v]) {
            if (!visited.Contains(neighbor))
                DFS(neighbor, visited, adjacencyList);
        }
    }

    public bool IsConnected(List<KeyValuePair<string, string>> edges) {
        Dictionary<string, List<string>> adjacencyList = BuildAdjacencyList(edges);
        HashSet<string> visited = new HashSet<string>();

        if (adjacencyList.Count == 0)
            return true; // Empty graph is considered connected

        string startVertex = adjacencyList.Keys.First();
        DFS(startVertex, visited, adjacencyList);

        // Check if all vertices were visited
        foreach (string vertex in adjacencyList.Keys) {
            if (!visited.Contains(vertex))
                return false; // Graph is not connected
        }

        return true;
    }


    private List<KeyValuePair<string, string>> ParseCertificate(string certificate) {
        UtilCollection edgeSet = new UtilCollection(certificate);
        edgeSet.assertUnordered();

        return edgeSet.ToList().Select(edge => {
            edge.assertUnordered();
            edge.assertCount(2);
            List<UtilCollection> pair = edge.ToList();
            return new KeyValuePair<string, string>(pair[0].ToString(), pair[1].ToString());
        }).ToList();
    }

    public bool verify(STEINERTREE problem, string certificate) {
        List<KeyValuePair<string, string>> edges;
        try {
            edges = ParseCertificate(certificate);
        } catch {
            return false;
        }

        List<(string, string)> certEdges = edges.Select(e => (e.Key, e.Value)).ToList();
        List<(string, string)> graphEdges = problem.edges.Select(e => (e.Key, e.Value)).ToList();

        // Every edge must exist in G and appear once; K bounds the number of edges.
        if (!GraphCertificateChecks.EdgesExistAndDistinct(graphEdges, certEdges) || edges.Count > problem.K) {
            return false;
        }

        // A tree: connected and acyclic, i.e. |E| = |V| - 1 for the vertices the edges touch.
        HashSet<string> vertices = new HashSet<string>(edges.SelectMany(e => new[] { e.Key, e.Value }));
        if (edges.Count == 0) {
            // The empty edge set is a (single-vertex) tree only when there is nothing to connect.
            return problem.terminals.Count <= 1;
        }
        if (edges.Count != vertices.Count - 1 || !IsConnected(edges)) {
            return false;
        }

        // Every terminal must be in the tree.
        return problem.terminals.All(vertices.Contains);
    }

}
