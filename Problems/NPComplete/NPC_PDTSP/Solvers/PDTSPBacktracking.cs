using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PDTSP.Solvers;

class PDTSPBacktracking : ISolver<PDTSP> {
    public string solverName { get; } = "Pickup and Delivery Traveling Salesperson Backtracking";
    public string solverDefinition { get; } = "Builds a tour by depth-first search, pruning deliveries whose pickups are unvisited, missing edges, and prefixes over K. Returns the first feasible closed tour, or {} after exhaustive failure.";
    public string source { get; } = "Constraint backtracking applied to PDTSP.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    public SolverType solverType { get; } = SolverType.Backtracking | SolverType.DepthFirstSearch;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    public MathematicalFunction timeComplexity { get; } = new("n * n!", ", n = |nondepot vertices|");
    public bool timerHasExpired { get; set; }

    public string solve(PDTSP problem) {
        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;
        int n = problem.nodes.Count;
        int[] tour = new int[n + 1];
        bool[] visited = new bool[n];
        tour[0] = tour[n] = problem.DepotIndex;
        visited[problem.DepotIndex] = true;

        bool Search(int depth, int last, long cost) {
            if (timerHasExpired)
                return false;
            if (depth == n) {
                long closing = problem.Distance(last, problem.DepotIndex);
                return closing != PDTSP.NoEdge && cost + closing <= problem.K;
            }
            for (int next = 0; next < n; next++) {
                if (timerHasExpired)
                    return false;
                int pickup = problem.PickupForDelivery[next];
                if (visited[next] || (pickup >= 0 && !visited[pickup]))
                    continue;
                long edge = problem.Distance(last, next);
                if (edge == PDTSP.NoEdge || cost + edge > problem.K)
                    continue;
                visited[next] = true;
                tour[depth] = next;
                if (Search(depth + 1, next, cost + edge))
                    return true;
                visited[next] = false;
            }
            return false;
        }

        bool found = Search(1, problem.DepotIndex, 0);
        return timerHasExpired ? SolveRun.TimedOutAnswer : found ? problem.TourCertificate(tour) : "{}";
    }
}
