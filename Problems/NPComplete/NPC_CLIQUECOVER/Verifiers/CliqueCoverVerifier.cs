using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER.Verifiers;

class CliqueCoverVerifier : IVerifier<CLIQUECOVER> {
    public const string CertificateGrammar = "{clique1,...,cliqueM} | cliques partition N, M <= K, each clique fully connected in E";
    public const string CertificateExample = "{1,2,3},{4,5},{6,7,8}";

    // --- Fields ---
    public string verifierName { get; } = "Clique Cover Verifier";
    public string verifierDefinition { get; } = "This is a verifier for the NP-Complete Clique Cover problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public CliqueCoverVerifier() {

    }

    // Certificates come in two shapes: the documented bare form
    // ("{1,2,3},{4,5},{6,7,8}", no enclosing collection) and the form
    // CliqueCoverBruteForce actually emits, which already wraps that in an
    // extra outer "{...}". Try parsing as-is first (covers the solver's
    // output); if that isn't a single valid top-level collection, wrap it
    // in braces ourselves so UtilCollection can walk the bare form too.
    private List<List<string>> ParseCertificate(string certificate) {
        UtilCollection cliques;
        try {
            cliques = new UtilCollection(certificate);
            cliques.assertUnordered();
        } catch {
            cliques = new UtilCollection("{" + certificate + "}");
            cliques.assertUnordered();
        }

        List<List<string>> result = new List<List<string>>();
        foreach (UtilCollection clique in cliques) {
            clique.assertUnordered();
            result.Add(clique.ToList().Select(node => node.ToString()).ToList());
        }
        return result;
    }

    public bool verify(CLIQUECOVER problem, string certificate) {
        List<List<string>> cliques;
        try {
            cliques = ParseCertificate(certificate);
        } catch {
            return false;
        }

        if (cliques.Count > problem.K) {
            return false;
        }

        List<string> bandAid = new List<string>(problem.nodes);
        foreach (var nodeList in cliques) {
            foreach (var i in nodeList) {
                if (!bandAid.Contains(i)) {
                    return false;
                }

                bandAid.Remove(i);

                foreach (var j in nodeList) {
                    KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                    KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                    if (!(problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2) || i.Equals(j))) {
                        return false;
                    }
                }
            }
        }

        if (bandAid.Any()) {
            return false;
        }
        return true;
    }
}