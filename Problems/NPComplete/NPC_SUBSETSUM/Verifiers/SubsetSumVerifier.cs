using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;

class SubsetSumVerifier : IVerifier<SUBSETSUM> {

    // --- Fields ---
    public string verifierName {get;} = "Subset Sum Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Subset Summ";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Garret Stouffer"};

    private string _certificate = "";

     public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SubsetSumVerifier() {
        
    }

    public bool verify(SUBSETSUM problem, string certificate){
        List<string> c = certificate.Replace("{","").Replace("}","").Replace(" ","").Split(",").ToList();
        int sum = 0;
        foreach(string a in c){
            if(problem.S.Contains(a)){    
                sum += int.Parse(a);
            }
            else{
                return false;
            }
        }
        if(sum == problem.T){
            return true;
        }


        return false;
    }
}