using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;

namespace API.Problems.NPComplete.NPC_AKARI.Solvers;

/// <summary>
/// The "intelligent" Akari solver: at every node of the search it first applies the logical deductions a human
/// solver uses until nothing more follows, and only then guesses, on the cell that the darkest, most constrained
/// cell depends on. Exact: returns a solution whenever one exists, "{}" when none does, and
/// <see cref="SolveRun.TimedOutAnswer"/> when the timer runs out first.
/// </summary>
class AkariConstraintPropagation : ISolver<AKARI> {

    // --- Fields ---
    public string solverName { get; } = "Akari Constraint Propagation";
    public string solverDefinition { get; } = "Backtracking search with constraint propagation. At each node it repeatedly applies the Akari deductions to a fixpoint: a bulb rules out every cell it lights; a clue with all its bulbs rules out its other neighbours, and a clue with only as many open neighbours as it still needs fills them all; a dark cell with exactly one cell left that could light it forces a bulb there; and a dark cell with none, or an over- or under-full clue, is a contradiction. It then branches (bulb, then no bulb) on a candidate of the dark cell with the fewest candidates.";
    public string source { get; } = "Constraint propagation with a minimum-remaining-values branching rule, as for other constraint satisfaction puzzles (Russell and Norvig, Artificial Intelligence: A Modern Approach, ch. 6), using the standard Akari deduction rules.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Every branch fixes at least one cell, so there are at most 2^(w+1) nodes. Each node propagates in at most w + 1
    // passes (every pass but the last fixes a cell), and a pass costs O(w * (n + m)) (each white cell scans its two
    // segments), so a node costs O(w^2 * (n + m)).
    public MathematicalFunction timeComplexity { get; } = new("2^w * w^2 * (n + m)", ", worst case; w = white cells, n = rows, m = columns");
    // One copy of the cell states and segment counters per level of the search, at most w levels deep.
    public MathematicalFunction spaceComplexity { get; } = new("n * m + w^2", ", w = white cells, n = rows, m = columns");
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public AkariConstraintPropagation() { }

    private const byte Unknown = 0;
    private const byte Bulb = 1;
    private const byte NoBulb = 2;

    /// <summary>The cell states and segment bulb counts of one search node. Copied before each guess.</summary>
    private sealed class State {
        public readonly byte[] Cell;
        public readonly int[] SegmentBulbs;

        public State(int cells, int segments) {
            Cell = new byte[cells];
            SegmentBulbs = new int[segments];
        }

        private State(State other) {
            Cell = (byte[])other.Cell.Clone();
            SegmentBulbs = (int[])other.SegmentBulbs.Clone();
        }

        public State Copy() => new State(this);
    }

    public string solve(AKARI problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        bool[]? solution = Search(problem, new State(problem.WhiteCount, problem.SegmentCells.Length));
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return solution != null ? problem.Format(solution) : AkariVerifier.NoSolution;
    }

    private bool[]? Search(AKARI b, State s) {
        if (timerHasExpired) return null;
        if (!Propagate(b, s)) return null;

        // Branch on a candidate of the dark cell with the fewest ways left to be lit (each has at least two; one
        // would have been forced). With no dark cell left there is no undecided cell either, since an undecided
        // cell is always dark (a bulb rules out every cell it lights).
        int bestCount = int.MaxValue;
        int branchCell = -1;
        for (int d = 0; d < b.WhiteCount && bestCount > 2; d++) {
            if (s.Cell[d] == Bulb || Lit(b, s, d)) continue;
            int count = Candidates(b, s, d, out int first);
            if (count < bestCount) {
                bestCount = count;
                branchCell = first;
            }
        }
        if (branchCell < 0) {
            bool[] bulbs = s.Cell.Select(c => c == Bulb).ToArray();
            return b.IsSolution(bulbs) ? bulbs : null;
        }

        State withBulb = s.Copy();
        if (PlaceBulb(b, withBulb, branchCell)) {
            bool[]? found = Search(b, withBulb);
            if (found != null || timerHasExpired) return found;
        }
        s.Cell[branchCell] = NoBulb;
        return Search(b, s);
    }

    private static bool Lit(AKARI b, State s, int i) =>
        s.SegmentBulbs[b.RowSegment[i]] > 0 || s.SegmentBulbs[b.ColSegment[i]] > 0;

    // The undecided cells that could light dark cell d (d itself included when undecided). Cells in both of d's
    // segments are counted once.
    private static int Candidates(AKARI b, State s, int d, out int first) {
        first = -1;
        int count = 0;
        foreach (int u in b.SegmentCells[b.RowSegment[d]]) {
            if (s.Cell[u] != Unknown) continue;
            if (first < 0) first = u;
            count++;
        }
        foreach (int u in b.SegmentCells[b.ColSegment[d]]) {
            if (u == d || s.Cell[u] != Unknown) continue;
            if (first < 0) first = u;
            count++;
        }
        return count;
    }

    // Puts a bulb on undecided cell i and rules out every cell it lights. False if i is already lit.
    private static bool PlaceBulb(AKARI b, State s, int i) {
        if (Lit(b, s, i)) return false;
        s.Cell[i] = Bulb;
        s.SegmentBulbs[b.RowSegment[i]]++;
        s.SegmentBulbs[b.ColSegment[i]]++;
        foreach (int u in b.SegmentCells[b.RowSegment[i]]) if (s.Cell[u] == Unknown) s.Cell[u] = NoBulb;
        foreach (int u in b.SegmentCells[b.ColSegment[i]]) if (s.Cell[u] == Unknown) s.Cell[u] = NoBulb;
        return true;
    }

    // Applies the deductions until none changes anything. False on a contradiction. Invariant: an undecided cell is
    // never lit, because PlaceBulb rules out everything a new bulb lights.
    private bool Propagate(AKARI b, State s) {
        bool changed = true;
        while (changed) {
            if (timerHasExpired) return false;
            changed = false;

            // Clues: too many or too few possible bulbs is a contradiction; otherwise a full clue rules out its other
            // neighbours, and a clue that needs all its open neighbours gets them.
            for (int k = 0; k < b.ClueValue.Length; k++) {
                int bulbs = 0, open = 0;
                foreach (int i in b.ClueNeighbors[k]) {
                    if (s.Cell[i] == Bulb) bulbs++;
                    else if (s.Cell[i] == Unknown) open++;
                }
                int need = b.ClueValue[k] - bulbs;
                if (need < 0 || need > open) return false;
                if (open == 0) continue;
                if (need == 0) {
                    foreach (int i in b.ClueNeighbors[k]) if (s.Cell[i] == Unknown) s.Cell[i] = NoBulb;
                    changed = true;
                } else if (need == open) {
                    foreach (int i in b.ClueNeighbors[k]) {
                        if (s.Cell[i] == Unknown && !PlaceBulb(b, s, i)) return false;
                    }
                    changed = true;
                }
            }

            // Dark cells: one with no way left to be lit is a contradiction; one with a single way forces it.
            for (int d = 0; d < b.WhiteCount; d++) {
                if (s.Cell[d] == Bulb || Lit(b, s, d)) continue;
                int count = Candidates(b, s, d, out int only);
                if (count == 0) return false;
                if (count == 1) {
                    if (!PlaceBulb(b, s, only)) return false;
                    changed = true;
                }
            }
        }
        return true;
    }
}
