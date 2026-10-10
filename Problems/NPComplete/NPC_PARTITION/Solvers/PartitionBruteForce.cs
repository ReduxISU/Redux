using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Packing;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_PARTITION.Solvers;

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
    public string complexity { get; } = "O(n^2 * 2^n), n = |S|";

    // --- Methods Including Constructors ---
    public PartitionBruteForce() {

    }
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

    public string solve(PARTITION partition) => Solve(partition, StepRecorder<Placement>.Off);

    // Steps: one per split tried, in the order the counter visits them; the split that balances is accepted.
    public string Solve(PARTITION partition, StepRecorder<Placement> rec) {
        List<int> binary = new List<int>() { 1 };
        int counter = 0;
        for (int i = 0; i < partition.S.Count - 1; i++) {
            binary.Add(0);
        }
        string certificate = BinaryToCertificate(binary, partition.S);
        int tried = 0;
        // Read only inside recorder lambdas (and for Done), so they cost nothing when recording is off.
        Placement Split() => new(binary.Select(b => b == 1 ? 0 : 1));
        List<int> Side(int bit) => Enumerable.Range(0, binary.Count).Where(i => binary[i] == bit).ToList();
        int SumOf(List<int> side) => side.Sum(i => int.Parse(partition.S[i]));
        string[] S1Ids() => Side(1).Select(PackingFrames.ItemId).ToArray();
        string Try() {
            List<int> s1 = Side(1), s2 = Side(0);
            int a = SumOf(s1), b = SumOf(s2);
            string tried = $"Try S1 = {PackingFrames.Braces(s1.Select(i => partition.S[i]))}: {a} against {b}";
            return a == b ? $"{tried}. Equal." : $"{tried}, off by {Math.Abs(a - b)}.";
        }
        while (counter < Math.Pow(2, partition.S.Count)) {
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, partition.S);
            tried++;
            if (partition.defaultVerifier.verify(partition, certificate)) {
                rec.Accept(Split, Try, S1Ids);
                rec.Done(Split(), true, $"Both sides add up to {SumOf(Side(1))}, found after {Tries(tried)}.");
                return certificate;
            }
            rec.Reject(Split, Try, S1Ids);
            counter++;
        }
        int total = partition.S.Sum(int.Parse);
        rec.Done(new Placement([]), false, total % 2 != 0
            ? $"The total is odd ({total}), so no split can balance. Checked all {Tries(tried)}."
            : $"Checked all {Tries(tried)}. None balances.");
        return "{}";
    }

    private static string Tries(int n) => n == 1 ? "1 split" : $"{n} splits";
}
