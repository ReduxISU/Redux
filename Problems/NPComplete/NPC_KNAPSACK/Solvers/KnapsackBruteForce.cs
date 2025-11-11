using API.Interfaces;
using API.Interfaces.JSON_Objects;
using SPADE;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
class KnapsackBruteForce : ISolver<KNAPSACK> {

    // --- Fields ---
    public string solverName {get;} = "Knapsack Brute Force Solver";
    public string solverDefinition {get;} = "This a brute force solver for the 0-1 Knapsack problem";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Russell Phillips"};


    public string complexity {get;} = "O(2^n)";

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