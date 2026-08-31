using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;

class BernsteinVaziraniClassicalVerifier : IVerifier<BERNSTEINVAZIRANI> {

    public const string CertificateGrammar = "{f | f is list}";
    public const string CertificateExample = "(1,0,1)";

    // --- Fields ---
    public string verifierName { get; } = "Default Verifier";
    public string verifierDefinition { get; } = "Verify that a proposed solution bit string fulfills the promise of the Bernstein-Vazirani problem: f(x) = s · x for all x.";
    public string source { get; } = " ";
    public string[] contributors { get; } = { "Jason L. Wright" };
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public BernsteinVaziraniClassicalVerifier() {

    }

    /// <summary>
    /// Count the number of bits set to 1 in an integer
    /// </summary>
    private static int CountBits(int value) {
        int count = 0;
        while (value != 0) {
            count++;
            value &= value - 1;
        }
        return count;
    }

    public bool verify(BERNSTEINVAZIRANI problem, string certificate) {
        StringParser parser = new(CertificateGrammar);
        try {
            parser.parse(certificate);
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }

        UtilCollection bits = parser["f"];
        int certInt = 0;
        foreach (UtilCollection bit in bits) {
            string bitStr = bit.ToString();
            if (bitStr != "0" && bitStr != "1")
                throw new CertificateParseException(problem, certificate,
                    $"'{bitStr}' is not a valid bit; expected 0 or 1");
            certInt = (certInt << 1) | (bitStr == "1" ? 1 : 0);
        }

        if (bits.ToList().Count != problem.NBits)
            throw new CertificateParseException(problem, certificate,
                $"certificate has {bits.ToList().Count} bits but problem requires {problem.NBits}");

        for (int x = 0; x < (1 << problem.NBits); x++) {
            int dotProduct = CountBits(certInt & x) % 2;
            if (problem.Func(x) != (dotProduct == 1))
                return false;
        }
        return true;
    }
}
