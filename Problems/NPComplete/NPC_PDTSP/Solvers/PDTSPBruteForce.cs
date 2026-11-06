using API.Interfaces;
using API.Problems.NPComplete.NPC_PDTSP.Verifiers;

namespace API.Problems.NPComplete.NPC_PDTSP.Solvers;

class PDTSPBruteForce : ISolver<PDTSP> {
    public string solverName { get; } = "Pickup and Delivery Traveling Salesperson Brute Force";
    public string solverDefinition { get; } = "Enumerates all permutations of the nondepot vertices without pruning, checking each complete tour with the verifier. Returns the first feasible tour, or {} after exhaustive failure.";
    public string source { get; } = "Exhaustive permutation enumeration applied to PDTSP.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    public MathematicalFunction timeComplexity { get; } = new("n * n!", ", n = |nondepot vertices| (unit-cost label operations)");
    public bool timerHasExpired { get; set; }

    public string solve(PDTSP problem) {
        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;
        int[] permutation = Enumerable.Range(0, problem.nodes.Count).Where(i => i != problem.DepotIndex).ToArray();
        int[] tour = new int[problem.nodes.Count + 1];
        tour[0] = tour[^1] = problem.DepotIndex;
        var verifier = new PDTSPVerifier();
        string? answer = null;

        void Enumerate(int depth) {
            if (timerHasExpired || answer != null)
                return;
            if (depth == permutation.Length) {
                Array.Copy(permutation, 0, tour, 1, permutation.Length);
                string candidate = problem.TourCertificate(tour);
                if (verifier.verify(problem, candidate))
                    answer = candidate;
                return;
            }
            for (int i = depth; i < permutation.Length; i++) {
                if (timerHasExpired || answer != null)
                    return;
                (permutation[depth], permutation[i]) = (permutation[i], permutation[depth]);
                Enumerate(depth + 1);
                (permutation[depth], permutation[i]) = (permutation[i], permutation[depth]);
            }
        }

        Enumerate(0);
        return timerHasExpired ? SolveRun.TimedOutAnswer : answer ?? "{}";
    }
}
