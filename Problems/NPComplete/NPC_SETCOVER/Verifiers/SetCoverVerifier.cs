using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

class SetCoverVerifier : IVerifier<SETCOVER> {
    public const string CertificateGrammar = "{sub1,...,subK} subset S | union of chosen subsets covers the universal set U";
    public const string CertificateExample = "{{1,2,3},{4,5}}";

    // --- Fields ---
    public string verifierName { get; } = "Set Cover Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Set Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SetCoverVerifier() {

    }
    private List<string> parseCertificate(string certificate) {

        List<string> elementList = certificate.Replace("},{", ",").Replace("{", "").Replace("}", "").Replace(" ", "").Split(",").ToList();
        return elementList;

    }


    public bool verify(SETCOVER problem, string certificate) {

        List<string> elementList = parseCertificate(certificate);
        List<string> universalSet = new List<string>(problem.universal);

        foreach (var i in elementList) {
            if (universalSet.Contains(i)) {
                universalSet.Remove(i);
            }
        }

        if (!universalSet.Any()) {
            return true;
        }

        return false;
    }
}