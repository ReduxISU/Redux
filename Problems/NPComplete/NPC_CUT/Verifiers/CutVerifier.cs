using API.Interfaces;
using API.Interfaces.Graphs;
using SPADE;

namespace API.Problems.NPComplete.NPC_CUT.Verifiers;

class CutVerifier : IVerifier<CUT> {
    public const string CertificateGrammar = "{S} subset E | S has no duplicate edges (either orientation), S is exactly the set of edges crossing some partition of N into two sets, |S| = K";
    public const string CertificateExample = "{{2,1},{1,3},{2,3},{3,5},{2,4}}";

    // --- Fields ---
    public string verifierName { get; } = "Default Cut Verifier";
    public string verifierDefinition { get; } = "Checks that the certificate edges all exist in G, are distinct, are exactly the edges crossing some partition of the vertices into two sets, and number exactly K.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public CutVerifier() {

    }
    private List<List<string>> ParseCertificate(string certificate) {
        UtilCollection edgeSet = new UtilCollection(certificate);
        edgeSet.assertUnordered();

        return edgeSet.ToList().Select(edge => {
            edge.assertUnordered();
            edge.assertCount(2);
            return edge.ToList().Select(n => n.ToString()).ToList();
        }).ToList();
    }

    public bool verify(CUT problem, string certificate) {
        List<List<string>> edgeList;
        try {
            edgeList = ParseCertificate(certificate);
        } catch {
            return false;
        }

        List<(string, string)> certEdges = edgeList.Select(e => (e[0], e[1])).ToList();
        List<(string, string)> graphEdges = problem.edges.Select(e => (e.Key, e.Value)).ToList();

        // Every edge must exist in G and appear once (either orientation).
        if (!GraphCertificateChecks.EdgesExistAndDistinct(graphEdges, certEdges)) {
            return false;
        }
        // The certificate must be exactly the set of edges crossing some partition (S, V\S).
        if (!GraphCertificateChecks.IsExactCut(problem.nodes, graphEdges, certEdges)) {
            return false;
        }
        return certEdges.Count == problem.K;
    }
}
