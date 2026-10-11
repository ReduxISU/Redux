using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Table;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;

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
    public string complexity { get; } = "O(2^n * m * n), n = number of variables, m = number of constraints";

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

    public string solve(INTPROGRAMMING01 intPrograming) => Solve(intPrograming, StepRecorder<Assignment>.Off);

    // Steps: one per assignment tried, in the counter's order (x1 flips fastest): a Reject naming the rows that go over
    // their bounds, or the Accept of the first assignment every row allows; then Done. Focus: the Table picture's rows of
    // the constraints over their bounds (row 0 is the assignment, so constraint r is TableFrames.RowId(r + 1)), none for
    // the Accept.
    public string Solve(INTPROGRAMMING01 intPrograming, StepRecorder<Assignment> rec) {
        List<int> binary = new List<int>();
        for (int i = 0; i < intPrograming.C[0].Count; i++) {
            binary.Add(0);
        }
        int tried = 0;
        // Read only inside recorder lambdas, so they cost nothing when recording is off.
        Assignment Current() => new(binary.Select((v, j) => new KeyValuePair<string, string>(Variable(j), v.ToString())));
        List<int> Over() => Enumerable.Range(0, intPrograming.C.Count)
            .Where(r => r < intPrograming.d.Count && Total(intPrograming.C[r], binary) > intPrograming.d[r]).ToList();
        for (int i = 0; i < Math.Pow(2, binary.Count); i++) {
            string certificate = BinaryToCertificate(binary);
            tried++;
            if (intPrograming.defaultVerifier.verify(intPrograming, certificate)) {
                rec.Accept(Current, () => $"Try {certificate}: every row stays within its bound.", () => []);
                int count = tried;
                rec.Done(Current(), true, $"{certificate} satisfies every row, found on try {count} of {Math.Pow(2, binary.Count)}.");
                return certificate;
            }
            rec.Reject(Current, () => $"Try {certificate}: {OverText(intPrograming, binary, Over())}",
                () => Over().Select(r => TableFrames.RowId(r + 1)).ToArray());
            nextBinary(binary);

        }
        rec.Done(new Assignment([]), false, $"All {tried} assignments break some row, so no 0-1 solution exists.");
        // No assignment works: "{}", the empty set every solver answers when there is no answer (#797).
        return "{}";
    }

    /// <summary>Row <paramref name="row"/> of C times the assignment.</summary>
    internal static int Total(List<int> row, IReadOnlyList<int> x) {
        int total = 0;
        for (int j = 0; j < row.Count && j < x.Count; j++) total += row[j] * x[j];
        return total;
    }

    private static string OverText(INTPROGRAMMING01 p, List<int> x, List<int> over) {
        if (over.Count == 0) return "the verifier rejects it.";
        string Part(int r) => $"row {r + 1} adds up to {Total(p.C[r], x)}, over its bound {p.d[r]}";
        return over.Count == 1 ? $"{Part(over[0])}." : $"{string.Join("; ", over.Select(Part))}.";
    }
}
