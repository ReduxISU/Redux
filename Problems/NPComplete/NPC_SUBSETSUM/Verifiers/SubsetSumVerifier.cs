using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;

class SubsetSumVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Subset Sum Verifier";
    private string _verifierDefinition = "This is a verifier for Subset Summ";
    private string _source = " ";
    private string[] _contributors = { "Garret Stouffer"};

    private string _certificate = "";

    // --- Properties ---
    public string verifierName {
        get {
            return _verifierName;
        }
    }
    public string verifierDefinition {
        get {
            return _verifierDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
 public string[] contributors{
        get{
            return _contributors;
        }
    }
     public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SubsetSumVerifier() {
        
    }

    public bool verify(SUBSETSUM subsetSum, string certificate){
        List<string> c = certificate.Replace("{","").Replace("}","").Replace(" ","").Split(",").ToList();
        int sum = 0;
        foreach(string a in c){
            if(subsetSum.S.Contains(a)){    
                sum += int.Parse(a);
            }
            else{
                return false;
            }
        }
        if(sum == subsetSum.T){
            return true;
        }


        return false;
    }
}