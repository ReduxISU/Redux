using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
class JobSeqBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Job Sequencing Set Brute Force Solver";
    private string _solverDefinition = "This is a brute force solver for the NP-Complete Job Sequencing problem";
    private string _source = "";
    private string[] _contributors = {"Russell Phillips"};


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
    public JobSeqBruteForce() {
        
    }
    public static List<List<int>> GenerateCombinations(int x)
    {
        List<int> currentCombination = new List<int>();
        for (int i = 0; i < x; i++)
        {
            currentCombination.Add(i);
        }

        List<List<int>> combinations = new List<List<int>>();
        combinations.Add(new List<int>(currentCombination));

        while (true)
        {
            if (GetNextCombination(currentCombination))
            {
                combinations.Add(new List<int>(currentCombination));
            }
            else
            {
                break; // All combinations have been generated
            }
        }

        return combinations;
    }

    private static bool GetNextCombination(List<int> combination)
    {
        int x = combination.Count;
        int i = x - 2;
        while (i >= 0 && combination[i] >= combination[i + 1])
        {
            i--;
        }

        if (i < 0)
        {
            return false; // No more combinations
        }

        int j = x - 1;
        while (combination[j] <= combination[i])
        {
            j--;
        }

        // Swap elements at indices i and j
        int temp = combination[i];
        combination[i] = combination[j];
        combination[j] = temp;

        // Reverse the sequence from i+1 to the end
        combination.Reverse(i + 1, x - i - 1);

        return true;
    }
    public string permToCertificate(List<int> permutation) {
        string certificate = "(";
        foreach (int i in permutation) {
            certificate += $"{i},";
        }
        return certificate.TrimEnd(',') + ")";
    }
    public string solve(JOBSEQ jobseq){
        foreach (List<int> permutation in GenerateCombinations(jobseq.T.Count())) {
            if (jobseq.defaultVerifier.verify(jobseq, permutation)) {
                return permToCertificate(permutation);
            }
        }
        return "()";
    }

    /// <summary>
    /// Given Independent Set instance in string format and solution string, outputs a solution dictionary with 
    /// true values mapped to nodes that are in the solution set else false. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    public Dictionary<string,bool> getSolutionDict(string problemInstance, string solutionString){
        throw new NotImplementedException();
    }
}