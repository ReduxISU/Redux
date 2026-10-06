using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETPRODUCT.Verifiers;

class SubsetProductVerifier : IVerifier<SUBSETPRODUCT> {

    public const string CertificateGrammar = "{K | K is set}";
    public const string CertificateExample = "{2,3,5}";

    // --- Fields ---
    public string verifierName { get; } = "Default Subset Product Verifier";
    public string verifierDefinition { get; } = "Checks that every number in the certificate comes from S (each used at most once) and that they multiply to T.";
    public string source { get; } = "";
    public string sourceLink { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public SubsetProductVerifier() { }

    public bool verify(SUBSETPRODUCT problem, string certificate) {
        StringParser parser = new(CertificateGrammar);
        List<UtilCollection> elements;
        try {
            parser.parse(certificate);
            elements = parser["K"].ToList();
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }

        // How many copies of each number S still has available.
        Dictionary<string, int> available = new Dictionary<string, int>();
        foreach (string s in problem.S)
            available[s] = available.GetValueOrDefault(s) + 1;

        long product = 0;

        foreach (UtilCollection element in elements) {
            string a = element.ToString();
            if (!long.TryParse(a, out long value))
                throw new CertificateParseException(problem, certificate,
                    $"'{a}' is not a valid integer");

            if (available.GetValueOrDefault(a) == 0)
                return false;

            available[a]--;
            product *= value;
        }

        return elements.Count > 0 && product == problem.T;
    }
}
