using API.Interfaces;

namespace API.Problems.NPComplete.NPC_ExactCover.Solvers;
class ExactCoverBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Exact Cover Brute Force Solver";
    private string _solverDefinition = "This is a generic brute force solver for Exact Cover";
    private string _source = "";
    private string[] _contributors = { "Caleb Eardley"};


    // --- Properties ---
    public string solverName {
        get {
            return _solverName;
        }
    }
    public string solverDefinition {
        get {
            return _solverDefinition;
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
       
    public string solve(ExactCover exactCover){
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
