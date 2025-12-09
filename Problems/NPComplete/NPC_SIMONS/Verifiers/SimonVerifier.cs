using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SIMON.Verifiers;

class SimonVerifier : IVerifier<SIMON>
{

    // --- Fields ---
    public string verifierName {get;} = "ProblemVerifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public SimonVerifier()
    {

    }

    public bool verify(SIMON problem, string certificate){
        // TODO: implement {VERIFIER} for {PROBLEM}
        return true;
    }
}
