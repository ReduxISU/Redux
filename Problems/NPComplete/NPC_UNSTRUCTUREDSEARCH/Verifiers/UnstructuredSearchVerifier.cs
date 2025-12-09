using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;

class UnstructuredSearchVerifier : IVerifier<UNSTRUCTUREDSEARCH> {

    // --- Fields ---
    public string verifierName {get;} = "ProblemVerifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string[] contributors {get;} = { "Alex Svancara" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public UnstructuredSearchVerifier() {
        
    }

    public bool verify(UNSTRUCTUREDSEARCH problem, string certificate){
        // TODO: implement {VERIFIER} for {PROBLEM}
        return true;
    }
}
