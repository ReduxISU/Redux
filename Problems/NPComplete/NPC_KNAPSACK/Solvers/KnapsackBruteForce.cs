using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Tools.UtilCollection;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
class KnapsackBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Knapsack brute force";
    private string _solverDefinition = "This a brute force solver for the 0-1 Knapsack problem";
    private string _source = "";
    private string[] _contributers = { "Russell Phillips"};


    private string _complexity = "O(2^n)";

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
     public string[] contributers{
        get{
            return _contributers;
        }
    }
    public string complexity {
        get {
            return _complexity;
        }
    }

    public IEnumerable<List<int>> possibleSolutions(int len)
    {
        for (int i = 0; i < Math.Pow(2, len); i++) 
        {
            List<int> solution = new List<int>(); 
            for (int solBin = i + (int)Math.Pow(2, len); solBin != 1; solBin >>= 1)
            {
                if ((solBin & 1) == 0)
                    solution.Add(0);
                else
                    solution.Add(1);
            }
            yield return solution;
        }
    }
    // --- Methods Including Constructors ---
    //solver for 0-1 knapsack problem
    public string solve(KNAPSACK knapsack) {
        List<UtilCollection> items = knapsack.items.ToList();
        foreach (List<int> possibleSolution in possibleSolutions(items.Count()))
        {
            UtilCollection certificate = new UtilCollection("{}");
            for (int i = 0; i < knapsack.items.Count(); i++)
            {
                if (possibleSolution[i] == 1) certificate.Add(items[i]);
            }
            string strCertificate = certificate.ToString();
            if (knapsack.defaultVerifier.verify(knapsack, strCertificate)) return strCertificate;
        }

        return "";
    }

}