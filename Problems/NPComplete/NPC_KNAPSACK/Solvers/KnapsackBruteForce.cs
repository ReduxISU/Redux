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


    public MathematicalFunction timeComplexity { get; } = new("2^|i| * |i|", ", |i| = number of items; each of the 2^|i| subsets is built and checked");

    private const int MaxItems = 62;

    // Every 0/1 choice of len items, item b taken when bit b of i is set. A long mask holds at most 62 items
    // (1L << 63 is negative); the int mask this replaced went wrong past 30 (#1028).
    public IEnumerable<List<int>> possibleSolutions(int len, string problemName) {
        if (len > MaxItems) throw new ExactSearchLimitExceededException(problemName, len, MaxItems);
        for (long i = 0; i < (1L << len); i++) {
            List<int> solution = new List<int>(len);
            for (int b = 0; b < len; b++)
                solution.Add((int)((i >> b) & 1));
            yield return solution;
        }
    }
    // --- Methods Including Constructors ---
    //solver for 0-1 knapsack problem
    public string solve(KNAPSACK knapsack) {
        List<UtilCollection> items = knapsack.items.ToList();
        foreach (List<int> possibleSolution in possibleSolutions(items.Count(), knapsack.problemName)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
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
