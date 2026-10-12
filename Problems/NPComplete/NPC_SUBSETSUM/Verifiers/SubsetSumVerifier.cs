using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;

class SubsetSumVerifier : IVerifier<SUBSETSUM> {

    public const string CertificateGrammar = "{K | K is set}";
    public const string CertificateExample = "{1,12,15}";

    // --- Fields ---
    public string verifierName { get; } = "Subset Sum Solution Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Subset Sum";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Garret Stouffer" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SubsetSumVerifier() {

    }

    public bool verify(SUBSETSUM problem, string certificate) {
        StringParser parser = new(CertificateGrammar);
        List<UtilCollection> elements;
        try {
            parser.parse(certificate);
            // Materialize inside the try: SPADE may parse a scalar (e.g. "101")
            // without error and only fail when the result is enumerated.
            // SPADE checks the certificate; its numbers are then read from the text with every repeat kept, so {7,7} is
            // two 7s (#628). TODO(#628): once SPADE releases "is multiset", use "{K | K is multiset}" instead.
            elements = RepeatedElements.KeepRepeats(certificate, parser["K"]);
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }

        // Multiset of S (value -> remaining count): each element may be used at
        // most as many times as it occurs in S, so a certificate that reuses an
        // element is rejected. Using a count map keeps membership checks O(1).
        // Numbers are compared by value, so " 7" in S and "7" in the certificate are the same number.
        Dictionary<int, int> available = new Dictionary<int, int>();
        foreach (string s in problem.S) {
            int number = int.Parse(s);
            available[number] = available.GetValueOrDefault(number) + 1;
        }

        long sum = 0;

        foreach (UtilCollection element in elements) {
            string a = element.ToString();
            if (!int.TryParse(a, out int value))
                throw new CertificateParseException(problem, certificate,
                    $"'{a}' is not a valid integer");

            // Not a member of S, or every copy of it has already been used.
            if (available.GetValueOrDefault(value) == 0)
                return false;

            available[value]--;
            sum += value;
        }

        return sum == problem.T;
    }
}
