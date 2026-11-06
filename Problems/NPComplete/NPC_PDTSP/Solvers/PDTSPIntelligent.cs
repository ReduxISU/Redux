using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PDTSP.Solvers;

[DefaultFor(typeof(PDTSP))]
class PDTSPIntelligent : ISolver<PDTSP> {
    public string solverName { get; } = "Pickup and Delivery Traveling Salesperson Branch and Bound";
    public string solverDefinition { get; } = "Exact precedence-aware branch-and-bound. Tries cheaper edges first, prunes using a relaxed minimum-outgoing-edge lower bound, and discards dominated (visited set,last vertex) states. Returns a feasible tour within K or proves none exists; it does not promise the shortest tour.";
    public string source { get; } = "Branch-and-bound with outgoing-edge relaxation and state dominance applied to PDTSP.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Conservative worst case: improving arrivals can revisit a memoized state.
    public MathematicalFunction timeComplexity { get; } = new("n^2 * n!", ", n = |vertices|, including arbitrary-width visited-set operations");
    public bool timerHasExpired { get; set; }

    public string solve(PDTSP problem) {
        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;
        int n = problem.nodes.Count;
        if (n == 1)
            return problem.TourCertificate(new[] { problem.DepotIndex, problem.DepotIndex });

        long[] minimumOutgoing = new long[n];
        int[][] neighbors = new int[n][];
        for (int i = 0; i < n; i++) {
            if (timerHasExpired)
                return SolveRun.TimedOutAnswer;
            int from = i;
            neighbors[i] = Enumerable.Range(0, n)
                .Where(j => j != from && problem.Distance(from, j) != PDTSP.NoEdge)
                .OrderBy(j => problem.Distance(from, j)).ThenBy(j => j).ToArray();
            if (neighbors[i].Length == 0)
                return "{}";
            minimumOutgoing[i] = problem.Distance(i, neighbors[i][0]);
        }

        int[] tour = new int[n + 1];
        bool[] visited = new bool[n];
        tour[0] = tour[n] = problem.DepotIndex;
        visited[problem.DepotIndex] = true;
        // BigInteger keeps distinct states even with more than 64 vertices.
        BigInteger[] bits = Enumerable.Range(0, n).Select(i => BigInteger.One << i).ToArray();
        var cheapestArrival = new Dictionary<(BigInteger visited, int last), long>();
        long initialRemaining = minimumOutgoing.Sum() - minimumOutgoing[problem.DepotIndex];

        bool Search(int depth, int last, long cost, long remaining, BigInteger mask) {
            if (timerHasExpired)
                return false;
            if (depth == n) {
                long closing = problem.Distance(last, problem.DepotIndex);
                return closing != PDTSP.NoEdge && cost + closing <= problem.K;
            }

            var state = (mask, last);
            if (cheapestArrival.TryGetValue(state, out long earlier) && earlier <= cost)
                return false;
            cheapestArrival[state] = cost;

            // Any completion needs one outgoing edge from last and each unvisited vertex.
            // Allowing those vertices to point even to visited vertices only relaxes the problem.
            long firstEdge = PDTSP.NoEdge;
            foreach (int next in neighbors[last]) {
                if (timerHasExpired)
                    return false;
                int pickup = problem.PickupForDelivery[next];
                if (!visited[next] && (pickup < 0 || visited[pickup])) {
                    firstEdge = problem.Distance(last, next);
                    break;
                }
            }
            if (firstEdge == PDTSP.NoEdge || cost + firstEdge + remaining > problem.K)
                return false;

            foreach (int next in neighbors[last]) {
                if (timerHasExpired)
                    return false;
                int pickup = problem.PickupForDelivery[next];
                if (visited[next] || (pickup >= 0 && !visited[pickup]))
                    continue;
                long nextCost = cost + problem.Distance(last, next);
                if (nextCost > problem.K)
                    continue;
                visited[next] = true;
                tour[depth] = next;
                if (Search(depth + 1, next, nextCost, remaining - minimumOutgoing[next], mask | bits[next]))
                    return true;
                visited[next] = false;
            }
            return false;
        }

        bool found = Search(1, problem.DepotIndex, 0, initialRemaining, bits[problem.DepotIndex]);
        return timerHasExpired ? SolveRun.TimedOutAnswer : found ? problem.TourCertificate(tour) : "{}";
    }
}
