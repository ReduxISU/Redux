using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;

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

    // Written out by position, not added to a SPADE set, so a number S has twice can be picked twice (#628): {7,7}.
    private static string SubsetToCertificate(bool[] picked, List<string> S) {
        List<string> chosen = new List<string>();
        for (int i = 0; i < S.Count; i++) {
            if (picked[i])
                chosen.Add(S[i]);
        }
        return "{" + string.Join(",", chosen) + "}";
    }

    // Moves to the next subset in mask order (bit i picks item i), like adding 1 to the mask. Returns false once every
    // subset has been seen. A bool per item, not an int mask, so there is no limit on the number of items (#791).
    private static bool NextSubset(bool[] picked) {
        for (int i = 0; i < picked.Length; i++) {
            picked[i] = !picked[i];
            if (picked[i]) return true;
        }
        return false;
    }

    public string solve(SUBSETSUM subsetSum) => Solve(subsetSum, StepRecorder<Placement>.Off);

    // Steps: one per subset tried, in mask order; the subset that adds up to T is accepted.
    public string Solve(SUBSETSUM subsetSum, StepRecorder<Placement> rec) {
        int n = subsetSum.S.Count;

        // Enumerate every subset in bitmask order (mask 1, 2, 3, ...) so that all 2^n subsets -- including
        // the singleton {S[0]} -- are tested exactly once. Mask 0 (the empty subset)
        // is skipped: with positive integers and T > 0 it is never a witness, and
        // "{}" doubles as the no-solution sentinel.
        bool[] picked = new bool[n];
        int tried = 0;
        // Read only inside recorder lambdas (and for Done), so they cost nothing when recording is off.
        Placement Chosen() => new(picked.Select(p => p ? 0 : -1));
        List<int> Members() => Enumerable.Range(0, n).Where(i => picked[i]).ToList();
        string[] Focus() => Members().Select(PackingFrames.ItemId).ToArray();
        string Set() => PackingFrames.Braces(Members().Select(i => subsetSum.S[i]));
        string Adds() => PackingFrames.SumText(Members().Select(i => int.Parse(subsetSum.S[i])).ToList());
        while (NextSubset(picked)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit

            string certificate = SubsetToCertificate(picked, subsetSum.S);
            tried++;
            if (subsetSum.defaultVerifier.verify(subsetSum, certificate)) {
                rec.Accept(Chosen, () => $"Try {Set()}: {Adds()}. That's T.", Focus);
                rec.Done(Chosen(), true, $"{Set()} adds up to exactly {subsetSum.T}, found after {Tries(tried)}.");
                return certificate;
            }
            rec.Reject(Chosen, () => $"Try {Set()}: {Adds()}, not {subsetSum.T}.", Focus);
        }
        // "{}" is the answer only when T = 0 (the empty set adds up to 0); otherwise it means none was found. The verifier says
        // which.
        if (rec != StepRecorder<Placement>.Off) {
            bool empty = subsetSum.defaultVerifier.verify(subsetSum, "{}");
            rec.Done(new Placement([]), empty, empty
                ? $"Checked all {Tries(tried)} that hold something. None adds up to {subsetSum.T}, but the empty set does, so it is the answer."
                : $"Checked all {Tries(tried)}. None adds up to {subsetSum.T}.");
        }
        return "{}";
    }

    private static string Tries(int n) => n == 1 ? "1 subset" : $"{n} subsets";
}
