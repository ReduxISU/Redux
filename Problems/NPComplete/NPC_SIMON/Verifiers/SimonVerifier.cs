using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SIMON.Verifiers;

class SimonVerifier : IVerifier<SIMON> {
    public const string CertificateGrammar = "binary string s (length at most n bits, or empty/all-zero for s = 0)";
    public const string CertificateExample = "010";

    // --- Fields ---
    public string verifierName { get; } = "SimonVerifier";
    public string verifierDefinition { get; } = "Classifcal Verifier for Simon's Problem";
    public string source { get; } = "Simon, Daniel R. (1997-10-01). \"On the Power of Quantum Computation\". SIAM Journal on Computing. 26 (5): 1474–1483. doi:10.1137/S0097539796298637. ISSN 0097-5397";
    public string[] contributors { get; } = { "Jason L. Wright", "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public SimonVerifier() {
    }

    public bool verify(SIMON problem, string certificate) {
        if (problem.funcValues is null)
            throw new NullReferenceException();
        int nbits = SIMON.PowerOfTwo(problem.funcValues.Length);
        if (certificate.Length > nbits)
            return false;

        var vals = new HashSet<int>();

        int s = Convert.ToInt32(certificate, 2);
        for (int i = 0; i < problem.funcValues.Length; i++) {
            if (problem.Func(i) != problem.Func(i ^ s))
                return false;
            vals.Add(problem.Func(i));
        }

        if (vals.Count == problem.funcValues.Length && s == 0)
            return true;
        if (vals.Count == problem.funcValues.Length / 2 && s != 0)
            return true;
        return false;
    }
}
