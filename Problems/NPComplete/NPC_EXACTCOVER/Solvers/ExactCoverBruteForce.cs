using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER.Visualizations;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;

// The steps Exact Cover Brute Force records: one per combination of sets, in the order it counts through them (in binary,
// S1 the lowest bit, so the empty combination comes last). A combination is a Reject naming the elements it leaves
// uncovered or covers more than once, or the Accept that ends the search; then the Done. The recorder's cap hides the rest
// of a long run.
class ExactCoverBruteForce : ISolver<EXACTCOVER, ChosenSets> {

    // --- Fields ---
    public string solverName { get; } = "Exact Cover Brute Force";
    public string solverDefinition { get; } = "This is a generic brute force solver for Exact Cover";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // The enumeration is over 2^s, s = |S| (the number of candidate subsets) -- NOT 2^n over the
    // universe |X|, since `binary` has one bit per subset in S (binary.Count == exactCover.S.Count).
    // Each candidate's ExactCoverVerifier.verify compares every selected subset (<= s) against every
    // subset in S (s) via OrderBy+SequenceEqual, O(n) per comparison (n = |X|, bounding subset size)
    // -- O(s^2 * n) per candidate.
    public MathematicalFunction timeComplexity { get; } = new("2^s * s^2 * n", ", s = |S| (candidate subsets), n = |X| (bounds subset-comparison cost)");

    // --- Methods Including Constructors ---
    public ExactCoverBruteForce() {

    }
    private string BinaryToCertificate(List<int> binary, List<List<string>> S) {
        string certificate = "";
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] == 1) {
                certificate += "{";
                foreach (var element in S[i]) {
                    certificate += element + ",";
                }
                certificate = certificate.TrimEnd(',') + "},";
            }
        }
        return "{" + certificate.TrimEnd(',') + "}";

    }
    private void nextBinary(List<int> binary) {
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] == 0) {
                binary[i] += 1;
                return;
            } else if (binary[i] == 1) {
                binary[i] = 0;
            }
        }
    }

    // How the steps show a candidate, given by its bits, one per set of S (1: picked): the picked sets, all in focus.
    private static RunSteps<List<int>, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec) => new(rec,
        // What every picture shows: the picked sets, every one of them in focus.
        partial: binary => new ChosenSets(Picked(binary)),
        focus: Picked);

    // The ids (S1, S2, ...) of the sets a candidate's bits pick.
    private static string[] Picked(List<int> binary) =>
        Enumerable.Range(0, binary.Count).Where(i => binary[i] == 1).Select(SetPieces.SetId).ToArray();

    public string solve(EXACTCOVER exactCover) => Solve(exactCover, StepRecorder<ChosenSets>.Off);

    // Tries every combination of the sets of S until one covers every element exactly once.
    public string Solve(EXACTCOVER exactCover, StepRecorder<ChosenSets> rec) {
        List<int> binary = new List<int>() { };
        for (int i = 0; i < exactCover.S.Count; i++) {
            binary.Add(0);
        }
        var steps = NewSteps(rec);
        // The verifier only says no, so the elements a combination misses or doubles are worked out for the steps alone.
        BipartiteSpec? spec = rec.Enabled ? ExactCoverDefaultVisualization.Spec(exactCover) : null;
        int tried = 0;
        string certificate = BinaryToCertificate(binary, exactCover.S);
        for (int i = 0; i < Math.Pow(2, binary.Count); i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, exactCover.S);
            tried++;
            if (exactCover.defaultVerifier.verify(exactCover, certificate)) {
                steps.Accept(binary, $"Try {Words.Braces(Picked(binary))}. It covers every element exactly once.");
                steps.Done(new ChosenSets(Picked(binary)), true,
                    $"{Words.Braces(Picked(binary))} covers every element exactly once, found after {Words.Count(tried, "try", "tries")}.");
                return certificate;
            }
            steps.Reject(binary, $"Try {Words.Braces(Picked(binary))}: {BipartiteFrames.WhyRejected(spec!, Picked(binary))}.");
        }
        steps.Done(new ChosenSets([]), false, $"Checked all {tried} combinations of sets. None covers every element exactly once.");
        return "{}";
    }
}
