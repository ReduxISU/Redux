using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3.Visualizations;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;

// The steps 3-Dimensional Matching Brute Force records: one per set of |X| triples, in the order Combinations hands them
// out. A set is a Reject naming the elements it uses more than once (or never), or the Accept that ends the search; then
// the Done. The recorder's cap hides the rest of a long run.
// Not incremental: every step judges a whole set of |X| triples.
class ThreeDimensionalMatchingBruteForce : ISolver<DM3, ChosenTriples> {

    // --- Fields ---
    public string solverName { get; } = "3-Dimensional Matching Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for 3-Dimensional Matching. It enumerates every combination of |X| triples from M and returns the first one that is a perfect matching.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // SURPRISING: the number of combinations tried is at most the binomial coefficient C(|M|,|X|)
    // (Combinations below hands them out one at a time) -- NOT a flat 2^n or n! bound. |M|
    // (candidate triples) is an input size independent of |X|, |Y|, |Z|; when |M| is large relative
    // to n = |X| (up to n^3 possible triples), C(|M|,n) grows asymptotically worse than n! (e.g.
    // n=3, |M|=27 already gives C(27,3)=2925 > 3!=6). Exponential is kept as the closest available
    // bucket -- same reasoning as CliqueCoverBruteForce's (K+1)^n case, this combinatorial growth
    // isn't cleanly "exponential" or "factorial" in a single variable either. Each combination costs
    // O(n^2) to verify (GenericVerifierDM3's parse plus per-coordinate List.Contains membership scans).
    public MathematicalFunction timeComplexity { get; } = new("C(m, n) * n^2", ", m = |M| (candidate triples), n = |X| = |Y| = |Z|");

    // --- Methods Including Constructors ---
    public ThreeDimensionalMatchingBruteForce() {

    }


    private string indexListToCertificate(List<int> indecies, List<List<string>> M) {
        string certificate = "";
        foreach (int i in indecies) {
            string set = "";
            foreach (string e in M[i]) {
                set += "," + e;
            }
            set = "{" + set.Substring(1) + "}";
            certificate += "," + set;
        }
        return "{" + certificate.Substring(1) + "}";
    }

    /// <summary>
    /// Every way to pick <paramref name="size"/> of <paramref name="count"/> triples, one at a time, as ascending positions
    /// in lexicographic order ({0,1,2}, {0,1,3}, ...), the order the solver always tried them in. Each is a new list.
    /// Nothing when <paramref name="size"/> is more than <paramref name="count"/>.
    /// </summary>
    public static IEnumerable<List<int>> Combinations(int count, int size) {
        if (size > count) yield break;
        var combination = Enumerable.Range(0, size).ToList();
        while (true) {
            yield return new List<int>(combination);
            int i = size - 1;
            while (i >= 0 && combination[i] == count - size + i) i--;
            if (i < 0) yield break;
            combination[i]++;
            for (int j = i + 1; j < size; j++) combination[j] = combination[j - 1] + 1;
        }
    }

    // How the steps show a candidate, given by the positions in M of its triples: those triples, and the elements they use
    // more than once. The Accept says its own focus, none.
    private static RunSteps<List<int>, ChosenTriples> NewSteps(StepRecorder<ChosenTriples> rec, TripartiteSpec? spec) => new(rec,
        // What every picture shows: the triples tried, and the elements they use more than once.
        partial: TripartiteFrames.Chosen,
        focus: combination => TripartiteFrames.Repeated(spec!, combination).Select(r => r.Id).ToArray());

    public string solve(DM3 problem) => Solve(problem, StepRecorder<ChosenTriples>.Off);

    // Tries every set of |X| triples of M until the verifier accepts one.
    public string Solve(DM3 problem, StepRecorder<ChosenTriples> rec) {
        int n = problem.X.Count, m = problem.M.Count;
        // The verifier only says no, so the elements a set of triples uses twice or never are worked out for the steps alone.
        TripartiteSpec? spec = rec.Enabled ? DM3Visualization.Spec(problem) : null;
        var steps = NewSteps(rec, spec);
        // A matching needs |X| triples; with fewer in M there is none.
        if (m < n) {
            if (m == 0)
                steps.Done(new ChosenTriples([]), false, $"M has no triples, so nothing can match the {Words.Count(n, "element")} of X.");
            else
                steps.Done(new ChosenTriples([]), false, $"M has only {Words.Count(m, "triple")}, but a matching needs {n}, one for each element of X. There is nothing to try.");
            return "{}";
        }
        long tried = 0;
        foreach (List<int> combination in Combinations(m, n)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            tried++;
            string certificate = indexListToCertificate(combination, problem.M);
            if (problem.defaultVerifier.verify(problem, certificate)) {
                // With |Y| or |Z| bigger than |X|, a matching can leave some of their elements unused.
                var unused = rec.Enabled ? TripartiteFrames.Unused(spec!, combination) : [];
                string which = rec.Enabled ? Words.List(combination.Select(TripartiteFrames.TripleId).ToList()) : "";
                string triples = rec.Enabled ? Words.List(combination.Select(i => TripartiteFrames.TripleLabel(spec!, i)).ToList()) : "";
                if (unused.Count == 0) {
                    steps.Accept(combination, $"Try {which}: every element of X, Y and Z is used exactly once, so the verifier accepts it.", focus: () => []);
                    if (tried == 1)
                        steps.Done(TripartiteFrames.Chosen(combination), true, $"{triples} {(combination.Count == 1 ? "uses" : "use")} every element of X, Y and Z exactly once. Found on the first try.");
                    else
                        steps.Done(TripartiteFrames.Chosen(combination), true, $"{triples} {(combination.Count == 1 ? "uses" : "use")} every element of X, Y and Z exactly once. Found after {Words.Number(tried)} tries.");
                } else {
                    steps.Accept(combination, $"Try {which}: no element is used twice, so the verifier accepts it.", focus: () => []);
                    if (tried == 1)
                        steps.Done(TripartiteFrames.Chosen(combination), true, $"The verifier accepts {triples}, found on the first try, though {TripartiteFrames.Names(spec!, unused)} {(unused.Count == 1 ? "is" : "are")} never used.");
                    else
                        steps.Done(TripartiteFrames.Chosen(combination), true, $"The verifier accepts {triples}, found after {Words.Number(tried)} tries, though {TripartiteFrames.Names(spec!, unused)} {(unused.Count == 1 ? "is" : "are")} never used.");
                }
                return certificate;
            }
            steps.Reject(combination,
                $"Try {Words.List(combination.Select(TripartiteFrames.TripleId).ToList())}: {TripartiteFrames.WhyRejected(spec!, combination)}.");
        }
        if (tried == 1)
            steps.Done(new ChosenTriples([]), false, $"There is only one way to pick the triples, and the verifier rejects it, so there is no perfect matching.");
        else
            steps.Done(new ChosenTriples([]), false, $"Checked all {Words.Number(tried)} ways to pick {n} of the {m} triples. The verifier rejects every one, so there is no perfect matching.");
        return "{}";
    }
}
