using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;
using API.Interfaces.Packing;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;

// The steps Fast Approximation records, for the Packing picture (item i is S[i]; the one container is the subset):
//   - an Update per number, once the sums it reaches are merged in and trimmed, showing the best subset kept so far and
//     saying how many sums are kept;
//   - the Done, saying whether the best sum kept is T.
// The merges have no verdicts (nothing is judged against T until the end), so they are Updates. Incremental: each step
// shows the best subset kept so far, which the run goes on from (the next number can still replace it), never a whole
// candidate judged against T. So the picture draws it Committed and only the Done says whether T was hit.
class FastApproximation : ISolver<SUBSETSUM, Placement>, IIncrementalSolver {
    // --- Fields ---
    public string solverName { get; } = "Fast Approximation Algorithm";
    public string solverDefinition { get; } = "A fast approximation algorithm for subset sum that trades"
    + " solution quality for speed. Given error tolerance e, it processes elements one at a time,"
    + " maintaining a list of achievable subset sums; after each element, the list is sorted and trimmed"
    + " by discarding any sum that lies within a relative distance of e/(2n) of a sum already kept,"
    + " bounding the list size to O(n/e). This yields, in O(n^2/e) time, a subset sum guaranteed to be"
    + " within a factor of (1 - e) of the optimal solution not exceeding the target. Currently, the"
    + " algorithm is hard-coded to a default 5% error tolerance layer. Because it only approximates, it"
    + " returns {} whenever its best subset does not sum exactly to the target, even if an exact"
    + " subset exists.";
    public string source { get; } = "Oscar H. Ibarra and Chul E. Kim. 1975. Fast Approximation Algorithms for the Knapsack and Sum of Subset Problems. J. ACM 22, 4 (Oct. 1975), 463–468. https://doi.org/10.1145/321906.321909";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/321906.321909";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("n^2 * 1/e", ", n = |S|, e = error tolerance");

    // Defining a standard 5% default error tolerance layer for the approximation scheme
    private readonly double _epsilon = 0.05;

    private struct SumState {
        public double Sum;
        public List<int> ChosenItems;
        // The positions in S of ChosenItems, for the picture (two equal numbers are two items).
        public List<int> Positions;

        public SumState(double sum, List<int> items, List<int> positions) {
            Sum = sum;
            ChosenItems = items;
            Positions = positions;
        }
    }

    // --- Methods Including Constructors ---
    public FastApproximation() {
    }

    // How the steps show the pass, given by the sums kept so far (ascending) and the position of the number just merged
    // in: the best (largest) sum kept, and that number.
    private static RunSteps<(List<SumState> Kept, int Position), Placement> NewSteps(StepRecorder<Placement> rec, int n) => new(rec,
        // What every picture shows: the numbers of the best sum kept, and the number just merged in.
        partial: s => Best(s.Kept, n),
        focus: s => [PackingFrames.ItemId(s.Position)]);

    public string solve(SUBSETSUM subsetsum) => Solve(subsetsum, StepRecorder<Placement>.Off);

    // Builds the sums of subsets up to T one number at a time: each number is added to every sum kept so far, and sums
    // within a factor (1 + delta) of a smaller one kept are dropped, so the list stays short. The best sum kept at the end
    // is within a factor (1 - e) of the best sum up to T.
    public string Solve(SUBSETSUM subsetsum, StepRecorder<Placement> rec) {

        int target = subsetsum.T;
        List<int> numbers = subsetsum.S.Select(int.Parse).ToList();
        int n = numbers.Count;
        var steps = NewSteps(rec, n);

        if (target < 0) {
            // No subset of the input can sum to a negative target.
            steps.Done(new Placement([]), false, $"T is {target}. This approximation only builds sums from 0 up to T, so it answers no.");
            return "{}";
        }

        if (target == 0) {
            // The empty subset is the only subset this solver reports for T = 0, written as {}.
            steps.Done(new Placement([]), true, $"T is 0, so the empty set is the answer.");
            return "{}";
        }

        if (n == 0) {
            steps.Done(new Placement([]), false, $"There are no numbers, so nothing adds up to T.");
            return "{}";
        }

        double delta = _epsilon / (2 * n);

        List<SumState> L = new List<SumState> { new SumState(0, new List<int>(), new List<int>()) };

        for (int position = 0; position < n; position++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            int item = numbers[position];
            List<SumState> LPrime = new List<SumState>(L);
            foreach (var state in L) {
                double nextSum = state.Sum + item;
                if (nextSum <= target) {
                    var nextItems = new List<int>(state.ChosenItems) { item };
                    LPrime.Add(new SumState(nextSum, nextItems, new List<int>(state.Positions) { position }));
                }
            }

            // Order elements in ascending order by their accumulated sum
            LPrime.Sort((a, b) => a.Sum.CompareTo(b.Sum));

            // Run list-trimming function to drop values that are mathematically too close
            L = Trim(LPrime, delta);
            // The why says how many sums the trim dropped, when it dropped any.
            int dropped = LPrime.Count - L.Count;
            if (dropped > 0)
                steps.Update((L, position), $"Add {item} to every sum kept so far that stays within {target}. {Words.Count(L.Count, "sum")} kept, {dropped} trimmed as too close to another. Best so far: {(int)L[^1].Sum}.");
            else
                steps.Update((L, position), $"Add {item} to every sum kept so far that stays within {target}. {Words.Count(L.Count, "sum")} kept. Best so far: {(int)L[^1].Sum}.");
        }

        // Extract the optimal subset path tracking record from the back of the remaining sparse list
        SumState bestState = L[L.Count - 1];

        // Format the certificate like {1,7,12}
        string certificate = "{" + string.Join(",", bestState.ChosenItems) + "}";

        // The trimmed list only guarantees a sum close to the optimum, so the best subset can
        // miss the target. Only report it if it is actually a solution.
        bool ok = subsetsum.defaultVerifier.verify(subsetsum, certificate);
        if (ok) {
            steps.Done(Best(L, n), true, $"The best sum kept is exactly {target}: {PackingFrames.Braces(bestState.ChosenItems.Select(PackingFrames.Num))}.");
            return certificate;
        }
        steps.Done(new Placement([]), false, $"The best sum kept is {(int)bestState.Sum}, not {target}, so the approximation answers no, even if an exact subset exists.");
        return "{}";
    }

    // The best (largest) sum kept, as a placement: its numbers in the one container.
    private static Placement Best(List<SumState> kept, int n) {
        var assign = Enumerable.Repeat(-1, n).ToArray();
        foreach (int position in kept[^1].Positions) assign[position] = 0;
        return new Placement(assign);
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
