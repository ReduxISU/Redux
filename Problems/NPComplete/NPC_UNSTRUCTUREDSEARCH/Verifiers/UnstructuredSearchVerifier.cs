using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;

class UnstructuredSearchVerifier : IVerifier<UNSTRUCTUREDSEARCH> {
    public const string CertificateGrammar = "i | integer index such that f(i) != 0";
    public const string CertificateExample = "1";

    // --- Fields ---
    public string verifierName { get; } = "Default Verifier";
    public string verifierDefinition { get; } = "This verifier simply checks that the certificate provided results in f(x) != 0 for the unstructured search problem.";
    public string source { get; } = "Grover, L. K. (1996). A fast quantum mechanical algorithm for database search. In Proceedings of the twenty-eighth annual ACM symposium on Theory of computing (pp. 212-219).";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public UnstructuredSearchVerifier() {
    }

    public bool verify(UNSTRUCTUREDSEARCH problem, string certificate) {
        int i = Convert.ToInt32(certificate);
        // All we need to do is see if funcValues[certificate] is non-zero
        return problem.funcValues[i] != 0;
    }
}
