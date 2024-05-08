using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;
class HurkensShrijver : ISolver {

    // --- Fields ---
    private string _solverName = "Hurkens Shriver";
    private string _solverDefinition = "This is a generic local search solver for 3-Dimensional Matching, which, while possible, removes one constraint from the current solution, and swaps in two more constraints.";
    private string _source = "Hurkens, C. A. J., and A. Schrijver. “On the Size of Systems of Sets Every t of Which Have an SDR, with an Application to the Worst-Case Ratio of Heuristics for Packing Problems.” SIAM Journal on Discrete Mathematics 2, no. 1 (February 1989): 68–72. https://doi.org/10.1137/0402008.";
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
    public HurkensShrijver() {

    }

    public List<List<string>> solve(DM3 problem){
        List<List<string>> S = new List<List<string>>();
        List<List<string>> M = problem.M;
        HashSet<string> SHash = new HashSet<string>();
        

        S.Add(M[0]);
        SHash.Add(S[0][0]);SHash.Add(S[0][1]);SHash.Add(S[0][2]);
        M.RemoveAt(0);
        int currentCount = 0;
        while(currentCount<S.Count){
            currentCount = S.Count;
            foreach(var setS in S){
                SHash.Remove(setS[0]);SHash.Remove(setS[1]);SHash.Remove(setS[2]);
                foreach(var setM1 in M){
                    foreach(var setM2 in M){
                        if(setM1 == setM2){continue;}
                        if(setM1[0] == setM2[0] || setM1[1] == setM2[1] || setM1[2] == setM2[2]){continue;}
                        bool works = true;
                        foreach(var element in setM1)
                            if(SHash.Contains(element)){
                                works = false;
                            }
                        foreach(var element in setM2)
                            if(SHash.Contains(element)){
                                works = false;
                            }
                        if(works == true){
                            SHash.Add(setM1[0]);SHash.Add(setM1[1]);SHash.Add(setM1[2]);                            
                            SHash.Add(setM2[0]);SHash.Add(setM2[1]);SHash.Add(setM2[2]);
                            S.Add(setM1);S.Add(setM2);
                            S.Remove(setS);
                            break;
                        }
                        
                    }
                    if(S.Count > currentCount){
                        break;
                    }
                }
                if(S.Count == currentCount){
                    SHash.Add(setS[1]);SHash.Add(setS[2]);
                }
                break;
                
            }

        }

        return S;
    }
}