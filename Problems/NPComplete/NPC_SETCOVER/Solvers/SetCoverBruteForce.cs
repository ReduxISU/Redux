using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Visualizations;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

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
    public string complexity { get; } = "O(2^m * u^2), m = |subsets|, u = |universal|";

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
    public string solve(SETCOVER setCover) => Solve(setCover, StepRecorder<ChosenSets>.Off);

    // Steps: one per combination tried, in the order the solver tries them. Recorder state lives only in this call.
    public string Solve(SETCOVER setCover, StepRecorder<ChosenSets> rec) {
        // Every combination of 1, 2, ..., K sets (never more sets than S has), smallest first (#772).
        int m = setCover.subsets.Count;
        int maxSize = Math.Min(setCover.K, m);
        int tried = 0;
        List<int> combination = new List<int>();
        string[] Picked() => combination.Select(BipartiteFrames.SetId).ToArray();
        for (int size = 1; size <= maxSize; size++) {
            combination = new List<int>();
            for (int i = 0; i < size; i++) {
                combination.Add(i);
            }
            BigInteger reps = factorial(m) / (factorial(size) * factorial(m - size));
            for (BigInteger i = 0; i < reps; i++) {
                string certificate = indexListToCertificate(combination, setCover.subsets);
                tried++;
                if (setCover.defaultVerifier.verify(setCover, certificate)) {
                    rec.Accept(() => new ChosenSets(Picked()), () => $"Try {BipartiteFrames.Braces(Picked())}. It covers every element.", Picked);
                    rec.Done(new ChosenSets(Picked()), true,
                        $"{BipartiteFrames.Braces(Picked())} covers U with {size} {(size == 1 ? "set" : "sets")}, found after {tried} {(tried == 1 ? "try" : "tries")}. "
                        + "Smaller combinations were tried first, so no cover uses fewer sets.");
                    return certificate;
                }
                rec.Reject(() => new ChosenSets(Picked()), () => $"Try {BipartiteFrames.Braces(Picked())}: {Why(setCover, Picked())}.", Picked);
                combination = nextComb(combination, setCover.subsets.Count);

            }
        }
        // Nothing was found. The empty collection is the answer only when U is empty.
        bool ok = setCover.defaultVerifier.verify(setCover, "{}");
        rec.Done(new ChosenSets([]), ok,
            ok ? "U is empty, so no sets are needed."
            : m == 0 ? "S has no sets, so U can't be covered."
            : tried == 0 ? $"K = {setCover.K} allows no sets, so U can't be covered."
            : $"Checked all {tried} combinations of up to K = {setCover.K} sets. None covers U.");
        return "{}";
    }

    private static string Why(SETCOVER setCover, string[] picked) {
        string why = BipartiteFrames.Why(SetCoverDefaultVisualization.Spec(setCover), picked);
        return why.Length > 0 ? why : "the verifier rejects it";
    }

}
