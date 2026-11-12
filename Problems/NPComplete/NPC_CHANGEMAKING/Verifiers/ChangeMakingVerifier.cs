using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_CHANGEMAKING.Verifiers;

class ChangeMakingVerifier : IVerifier<CHANGEMAKING> {

    public const string CertificateGrammar = "{(c,n),...} | c in C used n times, sum(n * c) = X, sum(n) <= K";
    public const string CertificateExample = "{(25,1),(7,2)}";

    // --- Fields ---
    public string verifierName { get; } = "Change-Making Solution Verifier";
    public string verifierDefinition { get; } = "Checks that every coin in the certificate is in C with a nonnegative count, that the coins total exactly X, and that at most K coins are used.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public ChangeMakingVerifier() {

    }

    public bool verify(CHANGEMAKING problem, string certificate) {
        // Counts rather than a list of coins: K is written in binary, so a list of up to K
        // coins could be exponentially longer than the instance.
        StringParser parser = new("{P | P subset int cross int}");
        List<UtilCollection> pairs;
        try {
            parser.parse(certificate);
            pairs = parser["P"].ToList();
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }

        HashSet<int> coins = new HashSet<int>(problem.C);
        // long: n * c and the running totals can overflow int on large inputs.
        long total = 0;
        long used = 0;

        foreach (UtilCollection pair in pairs) {
            string cStr = pair[0].ToString();
            string nStr = pair[1].ToString();
            if (!int.TryParse(cStr, out int coin) || !int.TryParse(nStr, out int count))
                throw new CertificateParseException(problem, certificate,
                    $"'{pair}' is not a pair of integers");

            if (!coins.Contains(coin) || count < 0)
                return false;

            total += (long)coin * count;
            used += count;
        }

        return total == problem.X && used <= problem.K;
    }
}
