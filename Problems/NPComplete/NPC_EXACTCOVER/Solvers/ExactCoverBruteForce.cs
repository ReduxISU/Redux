using API.Interfaces;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
class ExactCoverBruteForce : ISolver<EXACTCOVER> {

    // --- Fields ---
    public string solverName {get;} = "Exact Cover Brute Force Solver";
    public string solverDefinition {get;} = "This is a generic brute force solver for Exact Cover";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley"};

    // --- Methods Including Constructors ---
    public ExactCoverBruteForce() {
        
    }
    private string BinaryToCertificate(List<int> binary,  List<List<string>> S ){
        string certificate = "";
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 1){
                certificate += "{";
                foreach(var element in S[i]){
                    certificate += element+",";
                }
                certificate = certificate.TrimEnd(',') + "},";
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
       
    public string solve(EXACTCOVER exactCover){
        List<int> binary = new List<int>(){};
        for(int i = 0; i < exactCover.S.Count; i++){
            binary.Add(0);
        }
        string certificate = BinaryToCertificate(binary, exactCover.S);
        for(int i=0; i< Math.Pow(2,binary.Count); i++){
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, exactCover.S);
            if(exactCover.defaultVerifier.verify(exactCover, certificate)){
                return certificate;
            }
        }
        return "{}";
    }
}
