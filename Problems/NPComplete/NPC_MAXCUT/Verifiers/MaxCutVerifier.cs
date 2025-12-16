using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_MAXCUT.Verifiers;

class MaxCutVerifier : IVerifier<MAXCUT> {

    // --- Fields ---
    public string verifierName {get;} = "Max Cut Verifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = {"Max Gruenwoldt"};


    private string _certificate =  "";

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public CutVerifier() {
        
    }

    public bool verify(MAXCUT problem, string certificate){
        return "TODO";
    }
}