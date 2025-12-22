using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;

class DeutschClassicalVerifier : IVerifier<DEUTSCH> {

    // --- Fields ---
    public string verifierName {get;} = "Deutsch Classical Verifier";
    public string verifierDefinition { get; } = "Verify that a proposed solution correctly identifies whether the given function is constant or balanced by trying both possible inputs to the hidden function.";
    public string source {get;} = " ";
    public string[] contributors {get;} = { "Jason L. Wright" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public DeutschClassicalVerifier() {
    }

    public bool verify(DEUTSCH problem, string certificate){
        var solution = problem.Func(false) == problem.Func(true) ? "constant" : "balanced"; 
        return solution == certificate;
    }
}
