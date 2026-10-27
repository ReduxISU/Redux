using API.Interfaces;
using API.Problems.P.P_DFA;

namespace API.Problems.P.P_DFA.Verifiers;

class DFAVerifier : IVerifier<DFA> {
    public const string CertificateGrammar = "q0,...,qn | q0 = S, each (qi,I[i],qi+1) in E, qn in F";
    public const string CertificateExample = "1,2";

    // --- Fields ---
    public string verifierName { get; } = "DFA Acceptance Solution Verifier";
    public string verifierDefinition { get; } = "This is a solver for DFAs";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    private string _certificate = "";

    public string certificate { get => _certificate; }

    // --- Methods Including Constructors ---
    public DFAVerifier() { }

    public bool verify(DFA problem, string certificate) {
        // The empty input is written "ε".
        string input = problem.inputString == "ε" ? "" : problem.inputString;
        // One state per comma; spaces around a state are ignored ("1, 2" reads like "1,2").
        string[] states = certificate.Trim().Split(',').Select(s => s.Trim()).ToArray();

        // The run on an input of n symbols visits n + 1 states, starting at the start state (#777).
        if (states.Length != input.Length + 1) return false;
        if (states[0] != problem.startState) return false;

        // Each step reads the next input symbol along a transition of the DFA.
        for (int i = 0; i < input.Length; i++) {
            if (!problem.edges.Any(e => e.From == states[i] && e.Symbol == input[i] && e.To == states[i + 1]))
                return false;
        }

        // The run ends in an accept state.
        return problem.acceptStates.Contains(states[^1]);
    }
}
