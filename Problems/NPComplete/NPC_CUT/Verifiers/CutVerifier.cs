using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_CUT.Verifiers;

class CutVerifier : IVerifier<CUT> {
    public const string CertificateGrammar = "{S} subset E | S has no duplicate edges (either orientation), |S| = K";
    public const string CertificateExample = "{{2,1},{1,3},{2,3},{3,5},{2,4}}";

    // --- Fields ---
    public string verifierName { get; } = "Cut Verifier";
    public string verifierDefinition { get; } = "This is a verifier for the Cut problem";
    public string source { get; } = "";
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

        // Sort each edge's endpoints so {b,c} and {c,b} compare equal --
        // an undirected edge has no canonical orientation.
        List<List<string>> canonicalEdges = edgeList.Select(e => e.OrderBy(x => x).ToList()).ToList();

        int counter = 0;
        foreach (var edge in canonicalEdges) {
            //makes sure there are no duplicate edges, regardless of orientation
            if (canonicalEdges.Count(e => e.SequenceEqual(edge)) > 1) {
                return false;
            }
            KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(edge[0], edge[1]);
            KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(edge[1], edge[0]);
            if ((problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)) && !edge[1].Equals(edge[0])) { //Checks if edge exists, then adds to cut
                counter++;
            }
        }
        if (counter != problem.K) {
            return false;
        }
        return true;
    }
}