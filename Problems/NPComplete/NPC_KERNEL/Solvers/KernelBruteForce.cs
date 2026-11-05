using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_KERNEL.Solvers;

class KernelBruteForce : ISolver<KERNEL> {

    // --- Fields ---
    public string solverName { get; } = "Kernel Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Kernel problem";
    public string source { get; } = "V. Chvatal, On the computational complexity of finding a kernel, Report CRM-300, Universite de Montreal, 1973; author's online account. Subset enumeration is an original implementation of the definition, not an algorithm attributed to the report.";
    public string sourceLink { get; } = "https://users.encs.concordia.ca/~chvatal/kernel.html";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Bektur Akkabakov", "Prashant Pant" };
    // Declared, not derived: read the algorithm below and decide; don't guess from the
    // solver's name. Getting these wrong is a public correctness claim the API serves to callers.
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/SolverType.cs for what each value means.
    public SolverType solverType { get; } = SolverType.BruteForce;
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/SolverComplexityBucket.cs for what each value means.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // TODO: replace "" with a confidently-known Big-O string such as "O(n log n)", never a guess. A test in redux-tests/Metadata fails until you do, on purpose.
    public MathematicalFunction timeComplexity { get; } = new("2^n*(n+m)", ", n = |V|, m = |E|");
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }


    // --- Methods Including Constructors ---
    public KernelBruteForce() { }

    public string solve(KERNEL problem) {

        int[] vertices = problem.vertices.ToArray();
        int n = vertices.Length;

        long nSubsets = (long)Math.Pow(2, n);

        // Enumerate all 2^n subsets
        for (long candidate = 0; candidate < nSubsets; candidate++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            bool valid = true;
            HashSet<int> kernel = new HashSet<int>();
            HashSet<int> absorbed = new HashSet<int>();

            // Construct the candidate kernel
            long mask = candidate;
            while (mask != 0) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                int i = BitOperations.TrailingZeroCount((ulong)mask);
                kernel.Add(vertices[i]);
                // Remove the lowest set bit
                mask &= mask - 1;
            }

            // Check independence and track absorption
            foreach (var (u, v) in problem.edges) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                bool uInKernel = kernel.Contains(u);
                bool vInKernel = kernel.Contains(v);

                // Independence: no edge between vertices in S
                if (uInKernel && vInKernel) {
                    valid = false;
                    break;
                }

                // Absorption: an outside vertex points to S
                if (!uInKernel && vInKernel)
                    absorbed.Add(u);
            }

            if (!valid)
                continue;

            // Every vertex outside S must be absorbed
            foreach (int u in vertices) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                if (!kernel.Contains(u) && !absorbed.Contains(u)) {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return "{" + string.Join(", ", kernel.OrderBy(v => v)) + "}";
        }

        return "{}";
    }
}