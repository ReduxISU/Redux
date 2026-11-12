using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CHANGEMAKING.Solvers;

class ChangeMakingMemo : ISolver<CHANGEMAKING> {

    // --- Fields ---
    public string solverName { get; } = "Change-Making Memoization";
    public string solverDefinition { get; } = "A top-down, memoized solver. fewest(v) = 1 + min over coins c <= v of fewest(v - c), with fewest(0) = 0, is evaluated starting from X and each amount's answer is stored the first time it is computed, so only amounts reachable from X are visited, each once. The recursion is run on an explicit stack so a long chain of small coins cannot overflow the call stack. If fewest(X) <= K, the coin chosen at each amount is followed back from X to build the certificate.";
    public string source { get; } = "Martello, Silvano and Toth, Paolo. Knapsack Problems: Algorithms and Computer Implementations. Wiley, 1990. Chapter 5, Change-making problem.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://silvano333.github.io/kp/Chapter5.pdf";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. The same recurrence as ChangeMakingDP, evaluated by depth-first search
    // with a memo instead of bottom-up; pseudo-polynomial, Polynomial bucket by the same convention.
    public SolverType solverType { get; } = SolverType.DynamicProgramming | SolverType.DepthFirstSearch;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("m * X", ", m = |C|, X = target amount");
    public MathematicalFunction spaceComplexity { get; } = new("X", ", X = target amount");

    // --- Methods Including Constructors ---
    public ChangeMakingMemo() {

    }

    public string solve(CHANGEMAKING problem) {
        int x = problem.X;
        List<int> coins = problem.C.Distinct().ToList();

        // fewest[v] = fewest coins making exactly v (int.MaxValue if unreachable), valid once done[v];
        // last[v] = a coin used in one such way.
        int[] fewest = new int[x + 1];
        int[] last = new int[x + 1];
        bool[] done = new bool[x + 1];
        done[0] = true;

        // Each frame is an amount and the index of the next coin whose subproblem it still has to visit.
        // Subproblems are strictly smaller amounts, so the frames on the stack are always distinct and
        // the stack never holds more than X + 1 of them.
        Stack<(int amount, int next)> stack = new Stack<(int, int)>();
        stack.Push((x, 0));
        while (stack.Count > 0) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit

            (int v, int i) = stack.Pop();
            if (i < coins.Count) {
                stack.Push((v, i + 1));
                int c = coins[i];
                if (c <= v && !done[v - c])
                    stack.Push((v - c, 0));
                continue;
            }

            // Every subproblem of v is done.
            fewest[v] = int.MaxValue;
            foreach (int c in coins) {
                if (c <= v && fewest[v - c] != int.MaxValue && fewest[v - c] + 1 < fewest[v]) {
                    fewest[v] = fewest[v - c] + 1;
                    last[v] = c;
                }
            }
            done[v] = true;
        }

        if (fewest[x] > problem.K) return "{}";

        return ChangeMakingDP.Certificate(coins, x, last);
    }
}
