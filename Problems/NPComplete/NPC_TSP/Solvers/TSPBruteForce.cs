using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

class TSPBruteForce : ISolver<TSP, Tour> {

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

    public static List<List<int>> GenerateCombinations(int x) {
        List<int> currentCombination = new List<int>();
        for (int i = 1; i <= x; i++) {
            currentCombination.Add(i);
        }

        List<List<int>> combinations = new List<List<int>>();
        combinations.Add(new List<int>(currentCombination));

        while (true) {
            if (GetNextCombination(currentCombination)) {
                combinations.Add(new List<int>(currentCombination));
            } else {
                break; // All combinations have been generated
            }
        }

        return combinations;
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

    // Steps: one per complete permutation, in the order the solver tries them (the same permutation can come round as
    // several rotations and reversals; that is what a brute force does). A permutation that is not a tour within K is a
    // Reject that says why: the first missing edge, or a total over K. The first one that is within K is the answer, so the
    // run stops there; every other step is a Reject. There are n! permutations, far more than the recorder's cap on all but
    // tiny instances, so only the first ones are kept and the Done step says how many are not shown. Recorder state lives
    // only in this call.
    public string Solve(TSP tsp, StepRecorder<Tour> rec) {
        if (tsp.nodes.Count == 0) {
            // There is one empty "permutation", whose certificate is "{}"; the answer is "{}" either way, as it always was.
            bool ok = false;
            try { ok = tsp.defaultVerifier.verify(tsp, "{}"); } catch { }
            rec.Done(new Tour([]), ok, "There are no cities to tour.");
            return "{}";
        }
        List<List<int>> combinations = GenerateCombinations(tsp.nodes.Count);
        int tried = 0;

        foreach (List<int> combination in combinations) {
            string certificate = combinationToCertificate(combination, tsp.nodes);
            tried++;
            if (tsp.defaultVerifier.verify(tsp, certificate)) {
                rec.Accept(() => GraphTourFrames.ParseTour(certificate),
                    () => $"Try {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(certificate).Order)}: total {TSPTourSteps.Num(TSPTourSteps.Cost(tsp, GraphTourFrames.ParseTour(certificate).Order) ?? 0)} is within K = {tsp.K}, so this is a tour.",
                    () => TSPTourSteps.EdgeIds(tsp, GraphTourFrames.ParseTour(certificate).Order));
                rec.Done(GraphTourFrames.ParseTour(certificate), true, $"Found a tour within K = {tsp.K} after trying {tried} {(tried == 1 ? "permutation" : "permutations")}.");
                return certificate;
            }
            rec.Reject(() => GraphTourFrames.ParseTour(certificate),
                () => {
                    var closed = GraphTourFrames.ParseTour(certificate).Order;
                    var missing = TSPTourSteps.FirstMissingHop(tsp, closed);
                    string why = missing is { } hop
                        ? $"there is no edge between {hop.From} and {hop.To}"
                        : $"total {TSPTourSteps.Num(TSPTourSteps.Cost(tsp, closed) ?? 0)} is more than K = {tsp.K}";
                    return $"Try {GraphTourFrames.Arrows(closed)}: {why}.";
                },
                () => {
                    var closed = GraphTourFrames.ParseTour(certificate).Order;
                    return TSPTourSteps.FirstMissingHop(tsp, closed) is { } hop ? new[] { hop.From, hop.To } : TSPTourSteps.EdgeIds(tsp, closed);
                });
        }

        rec.Done(new Tour([]), false, $"Tried all {tried} permutations of the cities. None is a tour within K = {tsp.K}.");
        return "{}";
    }
}
