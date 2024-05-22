using API.Interfaces;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
class IntegerProgrammingBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Integer Programming Brute Force Solver";
    private string _solverDefinition = "This is a generic brute force solver for 0-1 Integer Programming";
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
    public IntegerProgrammingBruteForce() {

    }
    private string BinaryToCertificate(List<int> binary){
        string certificate = "";
        for(int i = 0; i< binary.Count; i++){
            certificate += binary[i]+" ";
            
        }
        return "(" + certificate.TrimEnd() + ")";

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

    public string solve(INTPROGRAMMING01 intPrograming){
        List<int> binary = new List<int>();
        for(int i=0; i<intPrograming.C[0].Count; i++){
            binary.Add(0);
        }
        for(int i = 0; i<Math.Pow(2, binary.Count); i++){
            string certificate = BinaryToCertificate(binary);
            if(intPrograming.defaultVerifier.verify(intPrograming,certificate)){
                return certificate;
            }
            nextBinary(binary);

        }
        return "()";
    }
}