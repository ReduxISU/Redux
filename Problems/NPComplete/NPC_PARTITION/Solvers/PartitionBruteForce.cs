using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Packing;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_PARTITION.Solvers;

// The steps Partition Brute Force records: one per split, in the order the counter visits them. A split is a Reject saying
// what each side adds up to and how far apart they are, or the Accept that ends the search; then the Done. The recorder's
// cap hides the rest of a long run.
// Not incremental: every step judges a whole split (S1 is the items the counter marks 1, S2 the rest).
class PartitionBruteForce : ISolver<PARTITION, Placement> {

    // --- Fields ---
    public string solverName { get; } = "Partition Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the Partition problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all 2^n binary partition vectors; each candidate's
    // verify() call is O(n^2) (nested Count()/Contains() scans over S), dominating the O(n)
    // certificate-building cost per candidate.
    public MathematicalFunction timeComplexity { get; } = new("n^2 * 2^n", ", n = |S|");

    // --- Methods Including Constructors ---
    public PartitionBruteForce() {

    }
    // The vectors run over positions of S, so two equal numbers are two separate items. verify() counts S in a dictionary,
    // O(n) per candidate, so the declared O(n^2 * 2^n) above is a loose upper bound; it is left as is.
    private string BinaryToCertificate(List<int> binary, List<string> S) {
        string certificate = "(";
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] == 1) {
                certificate += S[i] + ",";
            }
        }
        certificate = certificate.TrimEnd(',');
        certificate += "),(";
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] == 0) {
                certificate += S[i] + ",";
            }
        }
        return "{" + certificate.TrimEnd(',') + ")}";

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

    // How the steps show a split, given by its bits, one per number of S (1: in S1): S1 in the first container and S2 in the
    // second, with S1's numbers in focus.
    private static RunSteps<List<int>, Placement> NewSteps(StepRecorder<Placement> rec) => new(rec,
        // What every picture shows: the two sides, and S1's numbers.
        partial: binary => new Placement(binary.Select(b => b == 1 ? 0 : 1)),
        focus: binary => Side(binary, 1).Select(PackingFrames.ItemId).ToArray());

    // The positions of the numbers on one side of a split: bit 1 for S1, 0 for S2.
    private static List<int> Side(List<int> binary, int bit) => Enumerable.Range(0, binary.Count).Where(i => binary[i] == bit).ToList();

    public string solve(PARTITION partition) => Solve(partition, StepRecorder<Placement>.Off);

    // Tries every split of S into S1 and S2 until both add up to the same.
    public string Solve(PARTITION partition, StepRecorder<Placement> rec) {
        List<int> binary = new List<int>() { 1 };
        int counter = 0;
        for (int i = 0; i < partition.S.Count - 1; i++) {
            binary.Add(0);
        }
        string certificate = BinaryToCertificate(binary, partition.S);
        int tried = 0;
        var steps = NewSteps(rec);
        while (counter < Math.Pow(2, partition.S.Count)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, partition.S);
            tried++;
            // The verifier only says yes or no, so what each side adds up to is worked out for the steps alone.
            int a = rec.Enabled ? Side(binary, 1).Sum(i => int.Parse(partition.S[i])) : 0;
            int b = rec.Enabled ? Side(binary, 0).Sum(i => int.Parse(partition.S[i])) : 0;
            if (partition.defaultVerifier.verify(partition, certificate)) {
                if (a == b)
                    steps.Accept(binary, $"Try S1 = {Words.Braces(Side(binary, 1).Select(i => partition.S[i]))}: {a} against {b}. Equal.");
                else
                    steps.Accept(binary, $"Try S1 = {Words.Braces(Side(binary, 1).Select(i => partition.S[i]))}: {a} against {b}, off by {Math.Abs(a - b)}.");
                steps.Done(new Placement(binary.Select(bit => bit == 1 ? 0 : 1)), true, $"Both sides add up to {a}, found after {Words.Count(tried, "split")}.");
                return certificate;
            }
            if (a == b)
                steps.Reject(binary, $"Try S1 = {Words.Braces(Side(binary, 1).Select(i => partition.S[i]))}: {a} against {b}. Equal.");
            else
                steps.Reject(binary, $"Try S1 = {Words.Braces(Side(binary, 1).Select(i => partition.S[i]))}: {a} against {b}, off by {Math.Abs(a - b)}.");
            counter++;
        }
        int total = partition.S.Sum(int.Parse);
        if (total % 2 != 0)
            steps.Done(new Placement([]), false, $"The total is odd ({total}), so no split can balance. Checked all {Words.Count(tried, "split")}.");
        else
            steps.Done(new Placement([]), false, $"Checked all {Words.Count(tried, "split")}. None balances.");
        return "{}";
    }
}
