using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER.Visualizations;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;

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

    public string solve(EXACTCOVER exactCover) => Solve(exactCover, StepRecorder<ChosenSets>.Off);

    // Steps: one per combination of sets tried, in the order the solver tries them (counting in binary, S1 the lowest
    // bit, so the empty combination comes last). Recorder state lives only in this call.
    public string Solve(EXACTCOVER exactCover, StepRecorder<ChosenSets> rec) {
        List<int> binary = new List<int>() { };
        for (int i = 0; i < exactCover.S.Count; i++) {
            binary.Add(0);
        }
        string[] Picked() => Enumerable.Range(0, binary.Count).Where(i => binary[i] == 1).Select(BipartiteFrames.SetId).ToArray();
        int tried = 0;
        string certificate = BinaryToCertificate(binary, exactCover.S);
        for (int i = 0; i < Math.Pow(2, binary.Count); i++) {
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, exactCover.S);
            tried++;
            if (exactCover.defaultVerifier.verify(exactCover, certificate)) {
                rec.Accept(() => new ChosenSets(Picked()), () => $"Try {BipartiteFrames.Braces(Picked())}. It covers every element exactly once.", Picked);
                rec.Done(new ChosenSets(Picked()), true,
                    $"{BipartiteFrames.Braces(Picked())} covers every element exactly once, found after {tried} {(tried == 1 ? "try" : "tries")}.");
                return certificate;
            }
            rec.Reject(() => new ChosenSets(Picked()), () => $"Try {BipartiteFrames.Braces(Picked())}: {Why(exactCover, Picked())}.", Picked);
        }
        rec.Done(new ChosenSets([]), false, $"Checked all {tried} combinations of sets. None covers every element exactly once.");
        return "{}";
    }

    private static string Why(EXACTCOVER exactCover, string[] picked) {
        string why = BipartiteFrames.Why(ExactCoverDefaultVisualization.Spec(exactCover), picked);
        return why.Length > 0 ? why : "the verifier rejects it";
    }
}
