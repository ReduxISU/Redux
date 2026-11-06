using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Visualizations;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

// The steps Set Cover Brute Force records: one per combination of sets, smallest combinations first. A combination is a
// Reject naming the elements it leaves uncovered, or the Accept that ends the search; then the Done. The recorder's cap
// hides the rest of a long run.
class SetCoverBruteForce : ISolver<SETCOVER, ChosenSets> {

    // --- Fields ---
    public string solverName { get; } = "Set Cover Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Set Cover problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Outer loop runs K times; for j = 0..K-1 the inner loop runs
    // C(m, j) times (m = |subsets|), so total iterations are bounded by sum_{j=0}^{K-1} C(m,j)
    // <= 2^m. Each iteration's verify() call additionally costs O(u^2) (List.Contains/Remove
    // scans over the universal set, u = |universal|).
    public MathematicalFunction timeComplexity { get; } = new("2^m * u^2", ", m = |subsets|, u = |universal|");

    // --- Methods Including Constructors ---
    public SetCoverBruteForce() {

    }
    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<List<string>> subsets) {

        string certificate = "{";
        foreach (int i in indecies) {
            foreach (var j in subsets[i]) {
                certificate += j + ",";
            }
            certificate = certificate.TrimEnd(',') + "},{";
        }
        certificate = certificate.TrimEnd('{');
        certificate = certificate.TrimEnd(',');

        return "{" + certificate + "}";
    }
    private List<int> nextComb(List<int> combination, int size) {
        for (int i = combination.Count - 1; i >= 0; i--) {
            if (combination[i] + 1 <= (i + size - combination.Count)) {
                combination[i] += 1;
                for (int j = i + 1; j < combination.Count; j++) {
                    combination[j] = combination[j - 1] + 1;
                }
                return combination;
            }
        }
        return combination;
    }
    // How the steps show a candidate, given by the positions in S of its sets: those sets, every one of them in focus.
    private static RunSteps<List<int>, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec) => new(rec,
        // What every picture shows: the sets of the combination, all in focus.
        partial: combination => new ChosenSets(Picked(combination)),
        focus: combination => Picked(combination));

    // The ids (S1, S2, ...) of the sets at these positions of S.
    private static string[] Picked(List<int> combination) => combination.Select(BipartiteFrames.SetId).ToArray();

    public string solve(SETCOVER setCover) => Solve(setCover, StepRecorder<ChosenSets>.Off);

    // Tries every combination of 1, 2, ..., K sets (never more sets than S has), smallest first, so the first cover found
    // uses as few sets as any.
    public string Solve(SETCOVER setCover, StepRecorder<ChosenSets> rec) {
        var steps = NewSteps(rec);
        // The verifier only says no, so the elements a combination leaves uncovered are worked out for the steps alone.
        BipartiteSpec? spec = rec.Enabled ? SetCoverDefaultVisualization.Spec(setCover) : null;
        int m = setCover.subsets.Count;
        int maxSize = Math.Min(setCover.K, m);
        int tried = 0;
        List<int> combination = new List<int>();
        for (int size = 1; size <= maxSize; size++) {
            combination = new List<int>();
            for (int i = 0; i < size; i++) {
                combination.Add(i);
            }
            BigInteger reps = factorial(m) / (factorial(size) * factorial(m - size));
            for (BigInteger i = 0; i < reps; i++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
                string certificate = indexListToCertificate(combination, setCover.subsets);
                tried++;
                if (setCover.defaultVerifier.verify(setCover, certificate)) {
                    steps.Accept(combination, $"Try {BipartiteFrames.Braces(Picked(combination))}. It covers every element.");
                    steps.Done(new ChosenSets(Picked(combination)), true,
                        $"{BipartiteFrames.Braces(Picked(combination))} covers U with {Words.Count(size, "set")}, found after {Words.Count(tried, "try", "tries")}. Smaller combinations were tried first, so no cover uses fewer sets.");
                    return certificate;
                }
                steps.Reject(combination, $"Try {BipartiteFrames.Braces(Picked(combination))}: {BipartiteFrames.WhyRejected(spec!, Picked(combination))}.");
                combination = nextComb(combination, setCover.subsets.Count);

            }
        }
        // Nothing was found. The empty collection is the answer only when U is empty.
        bool ok = setCover.defaultVerifier.verify(setCover, "{}");
        if (ok)
            steps.Done(new ChosenSets([]), true, $"U is empty, so no sets are needed.");
        else if (m == 0)
            steps.Done(new ChosenSets([]), false, $"S has no sets, so U can't be covered.");
        else if (tried == 0)
            steps.Done(new ChosenSets([]), false, $"K = {setCover.K} allows no sets, so U can't be covered.");
        else
            steps.Done(new ChosenSets([]), false, $"Checked all {tried} combinations of up to K = {setCover.K} sets. None covers U.");
        return "{}";
    }

}
