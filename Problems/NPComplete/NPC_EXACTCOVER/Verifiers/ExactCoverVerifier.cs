using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Verifiers;

class ExactCoverVerifier : IVerifier<EXACTCOVER> {

    public const string CertificateGrammar = "{T | T subset S, union of T = X, T pairwise disjoint}";
    public const string CertificateExample = "{{2,3},{4,1}}";

    // --- Fields ---
    public string verifierName { get; } = "Exact Cover Solution Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Exact Cover";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public ExactCoverVerifier() {

    }
    // The chosen sets, read with SPADE as the instance is, so an element name the instance accepts (with ':' or '~' in it) is
    // read back whole (#833). {} is the empty collection, which exactly covers an empty universe (#775).
    private List<List<string>> parseCertificate(string certificate) {
        // Blank text chooses no sets; SPADE cannot read it.
        if (string.IsNullOrWhiteSpace(certificate)) return new List<List<string>>();
        UtilCollection chosen = new UtilCollection(certificate.Trim());
        if (chosen.IsValue() || chosen.IsOrdered())
            throw new FormatException($"'{certificate}' is not a set of sets like {CertificateExample}");
        List<List<string>> parsedCertificate = new List<List<string>>();
        foreach (UtilCollection subset in chosen.ToList()) {
            if (subset.IsValue() || subset.IsOrdered())
                throw new FormatException($"'{certificate}' is not a set of sets like {CertificateExample}");
            parsedCertificate.Add(subset.ToList().Select(item => item.ToString()).ToList());
        }

        return parsedCertificate;
    }
    //Example certificate "{{1,2,3},{2,3,4},{1,2}}
    public bool verify(EXACTCOVER problem, string certificate) {
        List<List<string>> parsedCertificate = parseCertificate(certificate);

        foreach (var subset_i in parsedCertificate) {
            bool inS = false;
            foreach (var subset_j in problem.S) {
                if (subset_i.OrderBy(e => e).SequenceEqual(subset_j.OrderBy(e => e))) {
                    inS = true;
                }
            }
            if (!inS) return false;
        }

        //Check that it is an exact cover
        List<string> cover = new List<string>();
        foreach (var subset in parsedCertificate) {
            foreach (string element in subset) {
                cover.Add(element);
            }
        }
        if (cover.OrderBy(e => e).SequenceEqual(problem.X.OrderBy(e => e))) {

            return true;
        }

        return false;
    }
}
