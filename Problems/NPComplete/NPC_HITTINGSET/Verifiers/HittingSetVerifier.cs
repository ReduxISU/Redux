using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;

class HittingSetVerifier : IVerifier<HITTINGSET> {

    // --- Fields ---
    public string verifierName {get;} = "Hitting Set Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Hitting Set";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Russell Phillips"};
    private string _certificate =  "";

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public HittingSetVerifier() {
        
    }

    public bool verify(HITTINGSET problem, string certificate)
    {
        UtilCollection choosenSet = new UtilCollection(certificate);
        foreach (UtilCollection set in problem.subSets)
        {
            if (set.Intersect(choosenSet).Count() != 1)
                return false;
        }
        return true;
    }
}