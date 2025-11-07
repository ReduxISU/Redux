using API.Interfaces;

namespace API.Problems.NPComplete.NPC_{PROBLEM}.Verifiers;

class {VERIFIER_PASCEL_CASE} : IVerifier<{PROBLEM}> {

    // --- Fields ---
    public string verifierName {get;} = "{VERIFIER}";
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
    public {VERIFIER_PASCEL_CASE}Verifier() {
        
    }

    public bool verify({PROBLEM} problem, string certificate){
        // TODO: implement {VERIFIER} for {PROBLEM}
        return true;
    }
}
