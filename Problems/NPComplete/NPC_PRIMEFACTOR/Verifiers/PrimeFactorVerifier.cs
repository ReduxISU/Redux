using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;

class PrimeFactorVerifier : IVerifier<PRIMEFACTOR> {

    // --- Fields ---
    public string verifierName {get;} = "ProblemVerifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string[] contributors {get;} = { "Paul Gilbreath", "Alex Svancara" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public PrimeFactorVerifier() {
        
    }

    public bool verify(PRIMEFACTOR problem, string certificate){
        // TODO: implement {VERIFIER} for {PROBLEM}
        return true;
    }
}
