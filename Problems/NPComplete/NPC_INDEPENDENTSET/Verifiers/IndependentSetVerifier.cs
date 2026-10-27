using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.Verifiers;

class IndependentSetVerifier : IVerifier<INDEPENDENTSET> {
    public const string CertificateGrammar = "independent set of exactly K node names | no two joined by an edge in E";
    public const string CertificateExample = "{c,d,g}";

    // --- Fields ---
    public string verifierName { get; } = "Independent Set Solution Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Independent Set";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public IndependentSetVerifier() {

    }
    private List<string> parseCertificate(string certificate) {
        // "{}" is the empty set, not a set holding one blank node name.
        if (certificate.Replace(" ", "") == "{}") {
            return new List<string>();
        }
        return GraphParser.parseNodeListWithStringFunctions(certificate).Select(node => node.Trim()).ToList();
    }

    public bool verify(INDEPENDENTSET problem, string certificate) {
        List<string> nodeList = parseCertificate(certificate);
        //Check k value. Repeated names count once: {1,1} is a set of one node, not two (#766).
        if (nodeList.Distinct().Count() != problem.K) {
            return false;
        }
        // Every certificate node must belong to the graph: a name that is not in V has no edges, so the
        // pairwise check below would otherwise accept it.
        if (nodeList.Any(node => !problem.nodes.Contains(node))) {
            return false;
        }
        foreach (var i in nodeList) {
            foreach (var j in nodeList) {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                if ((problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)) && !i.Equals(j)) {
                    return false;
                }
            }
        }
        return true;
    }
}
