using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;

// The steps Subset Sum Brute Force records: one per subset that holds something, in mask order. A subset is a Reject
// saying what it adds up to, or the Accept that ends the search; then the Done. The recorder's cap hides the rest of a
// long run.
// Not incremental: every step judges a whole subset.
class SubsetSumBruteForce : ISolver<SUBSETSUM, Placement> {

    // --- Fields ---
    public string solverName { get; } = "Subset Sum Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for Subset Sum";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley", "Garret Stouffer" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all 2^n subset bitmasks; each candidate does O(n)
    // certificate building and an O(n) dictionary-based verify() -- no per-candidate factor
    // beyond linear.
    public MathematicalFunction timeComplexity { get; } = new("n * 2^n", ", n = |S|");

    // --- Methods Including Constructors ---
    public SubsetSumBruteForce() {

    }

    // Written out by position, not added to a SPADE set, so a number S has twice can be picked twice: {7,7}.
    private static string SubsetToCertificate(bool[] picked, List<string> S) {
        List<string> chosen = new List<string>();
        for (int i = 0; i < S.Count; i++) {
            if (picked[i])
                chosen.Add(S[i]);
        }
        return "{" + string.Join(",", chosen) + "}";
    }

    // Moves to the next subset in mask order (bit i picks item i), like adding 1 to the mask. Returns false once every
    // subset has been seen. A bool per item, not an int mask, so there is no limit on the number of items.
    private static bool NextSubset(bool[] picked) {
        for (int i = 0; i < picked.Length; i++) {
            picked[i] = !picked[i];
            if (picked[i]) return true;
        }
        return false;
    }

    // How the steps show a candidate, given by its picks, one per number of S: the numbers picked, all in focus.
    private static RunSteps<bool[], Placement> NewSteps(StepRecorder<Placement> rec) => new(rec,
        // What every picture shows: the numbers picked, in the one container, every one of them in focus.
        partial: picked => new Placement(picked.Select(p => p ? 0 : -1)),
        focus: picked => Members(picked).Select(PackingFrames.ItemId).ToArray());

    // The positions of the numbers a candidate picks.
    private static List<int> Members(bool[] picked) => Enumerable.Range(0, picked.Length).Where(i => picked[i]).ToList();

    public string solve(SUBSETSUM subsetSum) => Solve(subsetSum, StepRecorder<Placement>.Off);

    public string Solve(SUBSETSUM subsetSum, StepRecorder<Placement> rec) {
        int n = subsetSum.S.Count;

        // Enumerate every subset in bitmask order (mask 1, 2, 3, ...) so that all 2^n subsets -- including
        // the singleton {S[0]} -- are tested exactly once. Mask 0 (the empty subset)
        // is skipped: with positive integers and T > 0 it is never a witness, and
        // "{}" doubles as the no-solution sentinel.
        bool[] picked = new bool[n];
        int tried = 0;
        var steps = NewSteps(rec);
        while (NextSubset(picked)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit

            string certificate = SubsetToCertificate(picked, subsetSum.S);
            tried++;
            // The verifier only says yes or no, so what the subset adds up to is worked out inside the step line, for a kept
            // step alone.
            if (subsetSum.defaultVerifier.verify(subsetSum, certificate)) {
                steps.Accept(picked, $"Try {Words.Braces(Members(picked).Select(i => subsetSum.S[i]))}: {PackingFrames.SumText(Members(picked).Select(i => int.Parse(subsetSum.S[i])).ToList())}. That's T.");
                steps.Done(new Placement(picked.Select(p => p ? 0 : -1)), true,
                    $"{Words.Braces(Members(picked).Select(i => subsetSum.S[i]))} adds up to exactly {subsetSum.T}, found after {Words.Count(tried, "subset")}.");
                return certificate;
            }
            steps.Reject(picked, $"Try {Words.Braces(Members(picked).Select(i => subsetSum.S[i]))}: {PackingFrames.SumText(Members(picked).Select(i => int.Parse(subsetSum.S[i])).ToList())}, not {subsetSum.T}.");
        }
        // "{}" is the answer only when T = 0 (the empty set adds up to 0); otherwise it means none was found. The verifier says
        // which; a plain solve skips asking.
        if (rec.Enabled) {
            bool empty = subsetSum.defaultVerifier.verify(subsetSum, "{}");
            if (empty)
                steps.Done(new Placement([]), true, $"Checked all {Words.Count(tried, "subset")} that hold something. None adds up to {subsetSum.T}, but the empty set does, so it is the answer.");
            else
                steps.Done(new Placement([]), false, $"Checked all {Words.Count(tried, "subset")}. None adds up to {subsetSum.T}.");
        }
        return "{}";
    }
}
