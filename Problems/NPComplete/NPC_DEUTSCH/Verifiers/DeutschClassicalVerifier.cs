using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;

class DeutschClassicalVerifier : IVerifier<DEUTSCH> {
    public const string CertificateGrammar = "'constant' | f(0) = f(1), or 'balanced' | f(0) != f(1)";
    public const string CertificateExample = "balanced";

    // --- Fields ---
    public string verifierName { get; } = "Default Verifier";
    public string verifierDefinition { get; } = "Verify that a proposed solution correctly identifies whether the given function is constant or balanced by trying both possible inputs to the hidden function.";
    public string source { get; } = "Deutsch, David. 1985. Quantum theory, the Church-Turing principle and the universal quantum computer. Proc. R. Soc. Lond. A40097-117";
    public string[] contributors { get; } = { "Jason L. Wright" };
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public DeutschClassicalVerifier() {
    }

    public bool verify(DEUTSCH problem, string certificate) {
        var solution = problem.Func(false) == problem.Func(true) ? "constant" : "balanced";
        return solution == certificate;
    }
}
