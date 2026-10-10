using API.Interfaces;
using API.Interfaces.JSON_Objects;
using SPADE;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

class KnapsackBruteForce : ISolver<KNAPSACK> {

    // --- Fields ---
    public string solverName { get; } = "Knapsack Brute Force";
    public string solverDefinition { get; } = "This a brute force solver for the 0-1 Knapsack problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration (own complexity string above already says "O(2^n)").
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;


    public string complexity { get; } = "O(2^n)";

    // Every subset of len items, as 0/1 per item, in mask order: 0, 1, 2, ... with bit k picking item k. Counted with one
    // digit per item, not an int mask, so there is no limit on the number of items (#791).
    public IEnumerable<List<int>> possibleSolutions(int len) {
        int[] bits = new int[len];
        while (true) {
            yield return bits.ToList();
            int k = 0;
            while (k < len && bits[k] == 1) {
                bits[k] = 0;
                k++;
            }
            if (k == len) yield break;
            bits[k] = 1;
        }
    }
    // --- Methods Including Constructors ---
    //solver for 0-1 knapsack problem
    public string solve(KNAPSACK knapsack) {
        List<UtilCollection> items = knapsack.items.ToList();
        foreach (List<int> possibleSolution in possibleSolutions(items.Count())) {
            UtilCollection certificate = new UtilCollection("{}");
            for (int i = 0; i < knapsack.items.Count(); i++) {
                if (possibleSolution[i] == 1) certificate.Add(items[i]);
            }
            string strCertificate = certificate.ToString();
            if (knapsack.defaultVerifier.verify(knapsack, strCertificate)) return strCertificate;
        }

        return "";
    }

}
