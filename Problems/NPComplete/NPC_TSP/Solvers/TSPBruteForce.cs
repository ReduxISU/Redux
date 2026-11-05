using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

// The algorithm. The steps it records for the visualizations are in TSPBruteForce.Steps.cs.
partial class TSPBruteForce : ISolver<TSP, Tour> {

    // --- Fields ---
    public string solverName { get; } = "Traveling Salesperson Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Traveling Salesperson problem. It tries every ordering of the cities and returns the first tour that costs at most K, which is not necessarily the cheapest tour.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration over permutations -- factorial worst case.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Declared, not derived. Enumerates all n! permutations; each is checked via defaultVerifier,
    // whose verify() does two O(m) edge-list scans per node visited in the tour (n per
    // permutation), so per-permutation cost is O(n * m).
    public MathematicalFunction timeComplexity { get; } = new("n! * n * m", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public TSPBruteForce() {

    }

    private string combinationToCertificate(List<int> combination, List<string> nodes) {
        string certificate = "";
        foreach (int i in combination) {
            certificate += nodes[i - 1] + ',';
        }
        return "{" + certificate + certificate.Split(',')[0] + "}";
    }

    public static List<List<int>> GenerateCombinations(int x) => EnumerateCombinations(x).ToList();

    // Lazy, in the same order, so a solve stopped at the time limit has not first built all n! permutations.
    private static IEnumerable<List<int>> EnumerateCombinations(int x) {
        List<int> currentCombination = new List<int>();
        for (int i = 1; i <= x; i++) {
            currentCombination.Add(i);
        }

        yield return new List<int>(currentCombination);

        while (true) {
            if (GetNextCombination(currentCombination)) {
                yield return new List<int>(currentCombination);
            } else {
                break; // All combinations have been generated
            }
        }
    }

    private static bool GetNextCombination(List<int> combination) {
        int x = combination.Count;
        int i = x - 2;
        while (i >= 0 && combination[i] >= combination[i + 1]) {
            i--;
        }

        if (i < 0) {
            return false; // No more combinations
        }

        int j = x - 1;
        while (combination[j] <= combination[i]) {
            j--;
        }

        // Swap elements at indices i and j
        int temp = combination[i];
        combination[i] = combination[j];
        combination[j] = temp;

        // Reverse the sequence from i+1 to the end
        combination.Reverse(i + 1, x - i - 1);

        return true;
    }


    public string solve(TSP tsp) => Solve(tsp, StepRecorder<Tour>.Off);

    public string Solve(TSP tsp, StepRecorder<Tour> rec) {
        var steps = new Steps(tsp, rec);
        if (tsp.nodes.Count == 0) {
            steps.NoCities();
            return "{}";
        }
        IEnumerable<List<int>> combinations = EnumerateCombinations(tsp.nodes.Count);
        int tried = 0;

        foreach (List<int> combination in combinations) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            string certificate = combinationToCertificate(combination, tsp.nodes);
            tried++;
            if (tsp.defaultVerifier.verify(tsp, certificate)) {
                steps.Found(certificate, tried);
                return certificate;
            }
            steps.Rejected(certificate);
        }

        steps.NoneWithinK(tried);
        return "{}";
    }
}
