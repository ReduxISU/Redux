using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;

class FastApproximation : ISolver<SUBSETSUM> {
    // --- Fields ---
    public string solverName { get; } = "Fast Approximation Algorithm";
    public string solverDefinition { get; } = "A fast approximation algorithm for subset sum that trades"
    + " solution quality for speed. Given error tolerance e, it processes elements one at a time,"
    + " maintaining a list of achievable subset sums; after each element, the list is sorted and trimmed"
    + " by discarding any sum that lies within a relative distance of e/(2n) of a sum already kept,"
    + " bounding the list size to O(n/e). This yields, in O(n^2/e) time, a subset sum guaranteed to be"
    + " within a factor of (1 - e) of the optimal solution not exceeding the target. Currently, the"
    + " algorithm is hard-coded to a default 5% error tolerance layer.";
    public string source { get; } = "Oscar H. Ibarra and Chul E. Kim. 1975. Fast Approximation Algorithms for the Knapsack and Sum of Subset Problems. J. ACM 22, 4 (Oct. 1975), 463–468. https://doi.org/10.1145/321906.321909";
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/321906.321909";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public string complexity { get; } = "O(n^2 * 1/e), n = |S|, e = error tolerance";

    // Defining a standard 5% default error tolerance layer for the approximation scheme
    private readonly double _epsilon = 0.05;

    private struct SumState {
        public double Sum;
        public List<int> ChosenItems;

        public SumState(double sum, List<int> items) {
            Sum = sum;
            ChosenItems = items;
        }
    }

    // --- Methods Including Constructors ---
    public FastApproximation() {
    }

    public string solve(SUBSETSUM subsetsum) {

        int target = subsetsum.T;
        List<int> numbers = subsetsum.S.Select(int.Parse).ToList();
        int n = numbers.Count;

        if (n == 0 || target <= 0) {
            return "{}";
        }

        double delta = _epsilon / (2 * n);

        List<SumState> L = new List<SumState> { new SumState(0, new List<int>()) };

        foreach (int item in numbers) {
            List<SumState> LPrime = new List<SumState>(L);
            foreach (var state in L) {
                double nextSum = state.Sum + item;
                if (nextSum <= target) {
                    var nextItems = new List<int>(state.ChosenItems) { item };
                    LPrime.Add(new SumState(nextSum, nextItems));
                }
            }

            // Order elements in ascending order by their accumulated sum
            LPrime.Sort((a, b) => a.Sum.CompareTo(b.Sum));

            // Run list-trimming function to drop values that are mathematically too close
            L = Trim(LPrime, delta);
        }

        // Extract the optimal subset path tracking record from the back of the remaining sparse list
        SumState bestState = L[L.Count - 1];

        // Return formatted certificate string matching format layout e.g. {1,7,12}
        return "{" + string.Join(",", bestState.ChosenItems) + "}";
    }

    private List<SumState> Trim(List<SumState> list, double delta) {
        if (list.Count == 0) return list;

        List<SumState> trimmed = new List<SumState> { list[0] };
        double lastValue = list[0].Sum;

        for (int i = 1; i < list.Count; i++) {
            double currentValue = list[i].Sum;

            // If the currentValue exceeds the lastValue buffer threshold criteria, preserve state
            if (currentValue > lastValue * (1 + delta)) {
                trimmed.Add(list[i]);
                lastValue = currentValue;
            }
        }

        return trimmed;
    }
}
