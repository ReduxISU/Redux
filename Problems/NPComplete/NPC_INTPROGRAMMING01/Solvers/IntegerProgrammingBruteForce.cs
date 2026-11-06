using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Table;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;

// The steps 0-1 ILP Brute Force records, for the Table picture: one per assignment tried, in the counter's order (x1 flips
// fastest). An assignment is a Reject naming the rows that go over their bounds (those rows in focus), or the Accept of the
// first assignment every row allows; then the Done, with the answer, or with the last assignment tried when none works.
// The recorder's cap hides the rest of a long run.
// Not incremental: every step judges a whole 0-1 assignment. The partial names the variables x1 ... xn, as the Table
// picture's columns do.
class IntegerProgrammingBruteForce : ISolver<INTPROGRAMMING01, Assignment> {

    // --- Fields ---
    public string solverName { get; } = "0-1 ILP Brute Force";
    public string solverDefinition { get; } = "Tests every binary assignment for 0-1 Integer Linear Programming and returns the first one that satisfies all constraints.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all 2^n 0/1 assignments (n = number of variables,
    // i.e. columns of C); each is verified via a full C*x matrix-vector product, O(m*n).
    public MathematicalFunction timeComplexity { get; } = new("2^n * m * n", ", n = number of variables, m = number of constraints");

    // --- Methods Including Constructors ---
    public IntegerProgrammingBruteForce() {

    }
    private string BinaryToCertificate(List<int> binary) {
        string certificate = "";
        for (int i = 0; i < binary.Count; i++) {
            certificate += binary[i] + " ";

        }
        return "(" + certificate.TrimEnd() + ")";

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

    /// <summary>The variable name of column <paramref name="j"/> (0-based): x1, x2, ...</summary>
    internal static string Variable(int j) => $"x{j + 1}";

    // How the steps show an assignment, given by its bits (x1 first) and the constraints it breaks.
    private static RunSteps<(List<int> Binary, IReadOnlyList<int> Over), Assignment> NewSteps(StepRecorder<Assignment> rec) => new(rec,
        // What every picture shows: the assignment, and the Table picture's rows of the constraints over their bounds (row
        // 0 is the assignment, so constraint r is TableFrames.RowId(r + 1)).
        partial: s => Current(s.Binary),
        focus: s => s.Over.Select(r => TableFrames.RowId(r + 1)).ToArray());

    // The assignment as the picture names it: x1 = 0, x2 = 1, ...
    private static Assignment Current(List<int> binary) => new(binary.Select((v, j) => new KeyValuePair<string, string>(Variable(j), v.ToString())));

    public string solve(INTPROGRAMMING01 intPrograming) => Solve(intPrograming, StepRecorder<Assignment>.Off);

    // Tries every 0-1 assignment until one keeps every row of C x within its bound in d.
    public string Solve(INTPROGRAMMING01 intPrograming, StepRecorder<Assignment> rec) {
        List<int> binary = new List<int>();
        for (int i = 0; i < intPrograming.C[0].Count; i++) {
            binary.Add(0);
        }
        int tried = 0;
        var steps = NewSteps(rec);
        double total = Math.Pow(2, binary.Count);
        string last = "";
        for (int i = 0; i < total; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            string certificate = BinaryToCertificate(binary);
            last = certificate;
            tried++;
            if (intPrograming.defaultVerifier.verify(intPrograming, certificate)) {
                steps.Accept((binary, []), $"Try {certificate}: every row stays within its bound.");
                steps.Done(Current(binary), true, $"{certificate} satisfies every row, found on try {tried} of {total}.");
                return certificate;
            }
            // The verifier only says yes or no, so which rows go over their bounds is worked out for the steps alone (only
            // while recording).
            IReadOnlyList<int> over = rec.Enabled
                ? Enumerable.Range(0, intPrograming.C.Count).Where(r => r < intPrograming.d.Count && Total(intPrograming.C[r], binary) > intPrograming.d[r]).ToList()
                : [];
            if (over.Count == 0)
                steps.Reject((binary, over), $"Try {certificate}: the verifier rejects it.");
            else if (over.Count == 1)
                steps.Reject((binary, over), $"Try {certificate}: row {over[0] + 1} adds up to {Total(intPrograming.C[over[0]], binary)}, over its bound {intPrograming.d[over[0]]}.");
            else
                steps.Reject((binary, over), $"Try {certificate}: {string.Join("; ", over.Select(r => $"row {r + 1} adds up to {Total(intPrograming.C[r], binary)}, over its bound {intPrograming.d[r]}"))}.");
            // The last try is kept, not wrapped back to all zeros, so the Done below can show it.
            if (i + 1 < total) nextBinary(binary);
        }
        // The Done keeps the last assignment tried, so the Table picture closes on it with the rows it breaks; the run's
        // verdict is still no.
        steps.Done(Current(binary), false, $"No 0-1 assignment satisfies every row: all {tried} were tried, the last {last}.");
        // No assignment works: "{}", the empty set every solver answers when there is no answer.
        return "{}";
    }

    /// <summary>Row <paramref name="row"/> of C times the assignment.</summary>
    internal static int Total(List<int> row, IReadOnlyList<int> x) {
        int total = 0;
        for (int j = 0; j < row.Count && j < x.Count; j++) total += row[j] * x[j];
        return total;
    }
}
