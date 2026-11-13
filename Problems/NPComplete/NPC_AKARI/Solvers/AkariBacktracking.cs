using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;

namespace API.Problems.NPComplete.NPC_AKARI.Solvers;

/// <summary>
/// Decides the white cells one at a time in row-major order (bulb first, then no bulb) and backs up as soon as the
/// partial placement can no longer be completed. Returns "{}" when the instance has no solution, and
/// <see cref="SolveRun.TimedOutAnswer"/> when the timer runs out first.
/// </summary>
class AkariBacktracking : ISolver<AKARI> {

    // --- Fields ---
    public string solverName { get; } = "Akari Backtracking";
    public string solverDefinition { get; } = "Depth-first search that decides each white cell in row-major order, trying a bulb before no bulb. A bulb is only placed on an unlit cell whose adjacent clues are not yet full. A branch is abandoned when a clue can no longer reach its number, or when a decided cell is still dark and no undecided, unlit cell that could light it remains.";
    public string source { get; } = "Standard chronological backtracking over the white cells.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // At most 2^(w+1) search nodes. Each does O((n + m)^2) pruning work (each cell in the decided cell's two segments
    // scans its own two segments), and a leaf adds an O(n * m) final check, which is also within O((n + m)^2).
    public MathematicalFunction timeComplexity { get; } = new("2^w * (n + m)^2", ", worst case; w = white cells, n = rows, m = columns");
    public MathematicalFunction spaceComplexity { get; } = new("n * m", ", board, counters, and recursion depth w <= n * m");
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public AkariBacktracking() { }

    public string solve(AKARI problem) {
        var search = new Search(problem, this);
        bool found = search.Run(0);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return found ? problem.Format(search.Bulb) : AkariVerifier.NoSolution;
    }

    private sealed class Search {
        private readonly AKARI _b;
        private readonly AkariBacktracking _solver;
        public readonly bool[] Bulb;
        private readonly int[] _segmentBulbs;   // bulbs in each segment
        private readonly int[] _clueBulbs;      // bulbs next to each clue
        private readonly int[] _clueUndecided;  // undecided white cells next to each clue

        public Search(AKARI board, AkariBacktracking solver) {
            _b = board;
            _solver = solver;
            Bulb = new bool[board.WhiteCount];
            _segmentBulbs = new int[board.SegmentCells.Length];
            _clueBulbs = new int[board.ClueValue.Length];
            _clueUndecided = board.ClueNeighbors.Select(n => n.Length).ToArray();
        }

        private bool Lit(int i) => _segmentBulbs[_b.RowSegment[i]] > 0 || _segmentBulbs[_b.ColSegment[i]] > 0;

        // Cells 0..index-1 are decided; true when a solution extends the current placement (left in Bulb).
        public bool Run(int index) {
            if (_solver.timerHasExpired) return false;
            if (index == _b.WhiteCount) return _b.IsSolution(Bulb);

            foreach (int clue in _b.CellClues[index]) _clueUndecided[clue]--;

            // Option 1: a bulb, if nothing lights this cell and no adjacent clue is already full.
            if (!Lit(index) && _b.CellClues[index].All(k => _clueBulbs[k] < _b.ClueValue[k])) {
                SetBulb(index, true);
                if (Consistent(index) && Run(index + 1)) return true;
                SetBulb(index, false);
                if (_solver.timerHasExpired) return false;
            }

            // Option 2: no bulb.
            if (Consistent(index) && Run(index + 1)) return true;

            foreach (int clue in _b.CellClues[index]) _clueUndecided[clue]++;
            return false;
        }

        private void SetBulb(int i, bool on) {
            int delta = on ? 1 : -1;
            Bulb[i] = on;
            _segmentBulbs[_b.RowSegment[i]] += delta;
            _segmentBulbs[_b.ColSegment[i]] += delta;
            foreach (int clue in _b.CellClues[i]) _clueBulbs[clue] += delta;
        }

        // After deciding cell `index`, checks the constraints that decision can break. Only cells sharing a segment
        // with `index` can have lost their last chance of being lit, so only they are checked here; a cell that loses
        // it later, through another cell, is caught when one of its own segments is next decided, or by the final check.
        private bool Consistent(int index) {
            foreach (int clue in _b.CellClues[index]) {
                if (_clueBulbs[clue] + _clueUndecided[clue] < _b.ClueValue[clue]) return false;
            }
            return StillLightable(_b.SegmentCells[_b.RowSegment[index]], index)
                && StillLightable(_b.SegmentCells[_b.ColSegment[index]], index);
        }

        private bool StillLightable(int[] cells, int index) {
            foreach (int d in cells) {
                if (d > index || Lit(d)) continue; // undecided, or already lit
                if (!HasCandidate(_b.SegmentCells[_b.RowSegment[d]], index)
                    && !HasCandidate(_b.SegmentCells[_b.ColSegment[d]], index)) return false;
            }
            return true;
        }

        // An undecided cell that could still take a bulb (bulbs only ever go on unlit cells).
        private bool HasCandidate(int[] cells, int index) {
            foreach (int u in cells) {
                if (u > index && !Lit(u)) return true;
            }
            return false;
        }
    }
}
