using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;

class ProblemVerifier : IVerifier<DEUTSCH> {

    // --- Fields ---
    public string verifierName {get;} = "ProblemVerifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    private string[] _contributers = { "TODO" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public ProblemVerifier() {
        
    }

    public bool verify(DEUTSCH problem, string certificate){
        // TODO: implement {VERIFIER} for {PROBLEM}
        return true;
    }
}
