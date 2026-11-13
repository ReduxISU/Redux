using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CHANGEMAKING.Solvers;

class ChangeMakingMemo : ISolver<CHANGEMAKING> {

    // --- Fields ---
    public string solverName { get; } = "Change-Making Memoization";
    public string solverDefinition { get; } = "A top-down, memoized solver. fewest(v) = 1 + min over coins c <= v of fewest(v - c), with fewest(0) = 0, is evaluated starting from X, and each amount's answer is stored in a dictionary the first time it is computed. Only the amounts reachable from X by taking away coins are visited or stored, each once, so a large X with few reachable amounts (all coins large) takes little time or memory; the work is refused once more than 2,000,000 amounts would be held. The recursion is run on an explicit stack so a long chain of small coins cannot overflow the call stack. If fewest(X) <= K, the coin chosen at each amount is followed back from X to build the certificate.";
    public string source { get; } = "Martello, Silvano and Toth, Paolo. Knapsack Problems: Algorithms and Computer Implementations. Wiley, 1990. Chapter 5, Change-making problem.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://silvano333.github.io/kp/Chapter5.pdf";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. The same recurrence as ChangeMakingDP, evaluated top-down with a memo
    // instead of bottom-up (#1011 adds a Memoization tag); pseudo-polynomial, Polynomial bucket by the
    // same convention.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("m * X", ", m = |C|, X = target amount");
    public MathematicalFunction spaceComplexity { get; } = new("X", ", X = target amount");

    // Most amounts held at once, in the memo and on the stack together: a dictionary entry costs about
    // 30 bytes, so this is about 60 MB.
    public const int MaxAmounts = 2_000_000;

    // --- Methods Including Constructors ---
    public ChangeMakingMemo() {

    }

    public string solve(CHANGEMAKING problem) {
        int x = problem.X;
        List<int> coins = problem.C.Distinct().ToList();

        // memo[v] = (fewest coins making exactly v, or int.MaxValue if unreachable; a coin used in one
        // such way). An amount is in memo once it is done.
        Dictionary<int, (int fewest, int last)> memo = new Dictionary<int, (int, int)> { [0] = (0, 0) };

        // Each frame is an amount and the index of the next coin whose subproblem it still has to visit.
        // Subproblems are strictly smaller amounts, so the frames on the stack are always distinct and
        // none of them is in memo yet.
        Stack<(int amount, int next)> stack = new Stack<(int, int)>();
        stack.Push((x, 0));
        while (stack.Count > 0) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
            if (memo.Count + stack.Count > MaxAmounts)
                throw new ExactSearchLimitExceededException(problem.problemName, new BigInteger(memo.Count + stack.Count), MaxAmounts);

            (int v, int i) = stack.Pop();
            if (i < coins.Count) {
                stack.Push((v, i + 1));
                int c = coins[i];
                if (c <= v && !memo.ContainsKey(v - c))
                    stack.Push((v - c, 0));
                continue;
            }

            // Every subproblem of v is done.
            (int fewest, int last) best = (int.MaxValue, 0);
            foreach (int c in coins) {
                if (c > v) continue;
                int sub = memo[v - c].fewest;
                if (sub != int.MaxValue && sub + 1 < best.fewest)
                    best = (sub + 1, c);
            }
            memo[v] = best;
        }

        if (memo[x].fewest > problem.K) return "{}";

        return ChangeMakingDP.Certificate(coins, x, v => memo[v].last);
    }
}
