using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
class SubsetSumBruteForce : ISolver<SUBSETSUM> {

    // --- Fields ---
    public string solverName {get;} = "Subset Sum Brute Force Solver";
    public string solverDefinition {get;} = "This is a brute force solver for Subset Sum";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley","Garret Stouffer"};
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public SubsetSumBruteForce() {
        
    }
    private string BinaryToCertificate(List<int> binary, List<string> S ){
        string certificate = "";
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 1){
                certificate += S[i]+",";
            }
        }
        return "{" + certificate.TrimEnd(',') + "}";

    }
    private void nextBinary(List<int> binary){
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 0){
                binary[i] += 1;
                return;
            }
            else if(binary[i] == 1){
                binary[i] = 0;
            }
        }
    }
       
    public string solve(SUBSETSUM subsetSum){
        List<int> binary = new List<int>(){1};
        for(int i = 0; i < subsetSum.S.Count-1; i++){
            binary.Add(0);
        }
        string certificate = BinaryToCertificate(binary, subsetSum.S);
        while(certificate != "{}"){
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, subsetSum.S);
            if(subsetSum.defaultVerifier.verify(subsetSum, certificate)){
                return certificate;
            }
        }
        return "{}";
    }
}