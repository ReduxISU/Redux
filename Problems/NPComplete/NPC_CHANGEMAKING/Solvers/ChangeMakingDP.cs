using API.Interfaces;
using System.Numerics;
using SPADE;

namespace API.Problems.NPComplete.NPC_CHANGEMAKING.Solvers;

class ChangeMakingDP : ISolver<CHANGEMAKING> {

    // --- Fields ---
    public string solverName { get; } = "Change-Making Dynamic Programming";
    public string solverDefinition { get; } = "A pseudo-polynomial time solver using bottom-up dynamic programming. best[v] is the fewest coins that make exactly v, computed for v = 1..X as 1 + min over coins c <= v of best[v - c]. If best[X] <= K, the coin chosen at each amount is followed back from X to build the certificate. The table has X + 1 entries, so X above 10,000,000 is refused rather than run out of memory.";
    public string source { get; } = "Martello, Silvano and Toth, Paolo. Knapsack Problems: Algorithms and Computer Implementations. Wiley, 1990. Chapter 5, Change-making problem.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://silvano333.github.io/kp/Chapter5.pdf";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Bucket stays Polynomial by convention (as for KnapsackDP) even though
    // O(m * X) is pseudo-polynomial: polynomial in the value of X, exponential in its bit length.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("m * X", ", m = |C|, X = target amount");
    public MathematicalFunction spaceComplexity { get; } = new("X", ", X = target amount");

    // Largest X whose tables are allocated: two int arrays of X + 1 entries, about 80 MB at this size.
    public const int MaxAmount = 10_000_000;

    // --- Methods Including Constructors ---
    public ChangeMakingDP() {

    }

    public string solve(CHANGEMAKING problem) {
        int x = problem.X;
        List<int> coins = problem.C.Distinct().ToList();

        // Checked before allocating: the timer cannot stop an allocation that is too large.
        if (x > MaxAmount)
            throw new ExactSearchLimitExceededException(problem.problemName, new BigInteger(x) + 1, MaxAmount + 1);

        // best[v] = fewest coins making exactly v (int.MaxValue if unreachable); last[v] = a coin
        // used in one such way.
        int[] best = new int[x + 1];
        int[] last = new int[x + 1];
        for (int v = 1; v <= x; v++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            best[v] = int.MaxValue;
            foreach (int c in coins) {
                if (c <= v && best[v - c] != int.MaxValue && best[v - c] + 1 < best[v]) {
                    best[v] = best[v - c] + 1;
                    last[v] = c;
                }
            }
        }

        if (best[x] > problem.K) return "{}";

        return Certificate(coins, x, v => last[v]);
    }

    // Follows lastCoin back from x to 0 and writes the coins used as {(c,n),...}, in the order of coins.
    // Shared with ChangeMakingMemo, which records the coin it chose at each amount the same way.
    internal static string Certificate(List<int> coins, int x, Func<int, int> lastCoin) {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        for (int v = x; v > 0; v -= lastCoin(v))
            counts[lastCoin(v)] = counts.GetValueOrDefault(lastCoin(v)) + 1;

        UtilCollection certificate = new UtilCollection("{}");
        foreach (int c in coins.Where(counts.ContainsKey))
            certificate.Add(new UtilCollection($"({c},{counts[c]})"));
        return certificate.ToString();
    }
}
