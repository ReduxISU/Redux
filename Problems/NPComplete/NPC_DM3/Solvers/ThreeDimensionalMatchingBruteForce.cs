using API.Interfaces;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;
class ThreeDimensionalMatchingBruteForce : ISolver<DM3> {

    // --- Fields ---
    public string solverName {get;} = "3-Dimensional Matching Brute Force Solver";
    public string solverDefinition {get;} = "This is a generic local search solver for 3-Dimensional Matching, which, while possible, removes one constraint from the current solution, and swaps in two more constraints.";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley"};

    // --- Methods Including Constructors ---
    public ThreeDimensionalMatchingBruteForce() {

    }


    private BigInteger factorial(long x){
    BigInteger y = 1;
    for(BigInteger i=1; i<=x; i++){
        y *= i;
    }
    return y;
    }
    private string indexListToCertificate(List<int> indecies, List<List<string>> M ){
        string certificate = "";
        foreach(int i in indecies){
            string set = "";
            foreach(string e in M[i]){
                set += ","+e;
            }
            set = "{"+set.Substring(1)+"}";
            certificate += "," + set;
        }
        return "{" + certificate.Substring(1) + "}";
    }
    private List<int> nextComb(List<int> combination, int size){
        for(int i=combination.Count-1; i>=0; i--){
            if(combination[i]+1 <= (i + size - combination.Count)){
                combination[i] += 1;
                for(int j = i+1; j < combination.Count; j++){
                    combination[j] = combination[j-1]+1;
                }
                return combination;
            }
        }
        return combination;
    }
    public string solve(DM3 problem){
        List<int> combination = new List<int>();
        for(int i=0; i<problem.X.Count(); i++){
            combination.Add(i);
        }
        BigInteger reps = factorial(problem.M.Count()) / (factorial(problem.X.Count()) * factorial(problem.M.Count() - problem.X.Count()));
        for(int i=0; i<reps; i++){
            string certificate = indexListToCertificate(combination,problem.M);
            if(problem.defaultVerifier.verify(problem, certificate)){
                return certificate;
            }
            combination = nextComb(combination, problem.M.Count);

        }
        return "{}";
    }
}