using API.Interfaces;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
class IntegerProgrammingBruteForce : ISolver<INTPROGRAMMING01> {

    // --- Fields ---
    public string solverName {get;} = "Integer Programming Brute Force Solver";
    public string solverDefinition {get;} = "This is a generic brute force solver for 0-1 Integer Programming";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley"};
    public bool timerHasExpired { get; set; }

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