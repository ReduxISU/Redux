using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;

namespace API.Problems.NPComplete.NPC_AKARI.Solvers;

/// <summary>
/// Tries every subset of the white cells as the set of bulbs, in binary-counter order starting from no bulbs, and
/// returns the first one that solves the puzzle. Returns "{}" when no subset does, and
/// <see cref="SolveRun.TimedOutAnswer"/> when the timer runs out first.
/// </summary>
class AkariBruteForce : ISolver<AKARI> {

    // --- Fields ---
    public string solverName { get; } = "Akari Brute Force";
    public string solverDefinition { get; } = "Enumerates all 2^w placements of bulbs on the w white cells and checks each against the rules (every white cell lit, no bulb lit by another, every clue satisfied), returning the first placement that passes. No pruning: practical only for small grids.";
    public string source { get; } = "Exhaustive search.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // 2^w candidates, each checked in O(n * m) by AKARI.IsSolution; the binary-counter step is amortized O(1).
    public MathematicalFunction timeComplexity { get; } = new("2^w * n * m", ", w = white cells, n = rows, m = columns");
    public MathematicalFunction spaceComplexity { get; } = new("n * m", ", n = rows, m = columns");
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public AkariBruteForce() { }

    public string solve(AKARI problem) {
        var bulb = new bool[problem.WhiteCount];

        // bulb[] is a w-bit binary counter (bulb[0] the low bit), so the loop visits every subset exactly once,
        // whatever w is (no 64-bit mask to overflow).
        while (true) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (problem.IsSolution(bulb)) return problem.Format(bulb);

            int i = 0;
            while (i < bulb.Length && bulb[i]) {
                bulb[i] = false;
                i++;
            }
            if (i == bulb.Length) return AkariVerifier.NoSolution; // wrapped back to all-false: every subset tried
            bulb[i] = true;
        }
    }
}
