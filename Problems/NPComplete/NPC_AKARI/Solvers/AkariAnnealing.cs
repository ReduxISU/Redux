using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;

namespace API.Problems.NPComplete.NPC_AKARI.Solvers;

/// <summary>
/// Solves Akari (Light Up) with simulated annealing.
/// <para>
/// A state is a set of bulbs on white cells. Its energy counts every broken rule:
/// </para>
/// <list type="bullet">
/// <item>each white cell that no bulb lights (weight <see cref="UNLIT_WEIGHT"/>),</item>
/// <item>each pair of bulbs that see each other (weight <see cref="CONFLICT_WEIGHT"/>),</item>
/// <item>each clue wall, by how far its adjacent bulb count is from its clue (weight <see cref="CLUE_WEIGHT"/>).</item>
/// </list>
/// A state with energy 0 is a solution. Moves toggle one bulb, or slide a bulb to another cell along its line of
/// sight or next to it. The grid geometry comes from <see cref="AKARI"/>'s precomputed row and column segments: the
/// only lighting state kept is the number of bulbs in each segment, so a cell is lit when either of its segments holds
/// a bulb, and two bulbs see each other exactly when they share a segment. Energy changes are computed incrementally:
/// O(1) for the conflict and clue terms, and a walk of a segment only when that segment gains its first bulb or loses
/// its last. Worse moves are accepted with probability exp(-delta / T) as T cools geometrically; when a round cools
/// without finding a solution the search restarts from the best state seen and reheats.
/// </summary>
class AkariAnnealing : ISolver<AKARI> {

    // --- Fields ---
    public string solverName { get; } = "Akari Simulated Annealing";
    public string solverDefinition { get; } = "Places light bulbs with simulated annealing. The energy of a placement is the number of unlit white cells, plus the number of bulb pairs that light each other, plus the total distance of each clue wall from its clue. Moves toggle a bulb or slide one along its line of sight; worse placements are accepted with probability exp(-delta/T) as the temperature cools, with reheating restarts. Lighting is tracked as a bulb count per row and column segment, so energy changes are computed incrementally. An energy of zero is a valid solution. Being stochastic and incomplete, the solver may fail to find a solution that exists, and cannot prove that none exists.";
    public string source { get; } = "Kirkpatrick, S., C. D. Gelatt, and M. P. Vecchi. 1983. \"Optimization by Simulated Annealing.\" Science 220 (4598): 671-680.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    public SolverType solverType { get; } = SolverType.Heuristic | SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // The step budget is fixed: MAX_ROUNDS * max(MIN_STEPS_PER_ROUND, STEPS_PER_CELL * w) = O(w) steps. A step costs at
    // most O(n + m) (walking the two segments of a toggled cell), and each round's restart reloads the best state in
    // O(w * (n + m)), MAX_ROUNDS times.
    public MathematicalFunction timeComplexity { get; } = new("w * (n + m)", ", w = white cells, n = rows, m = columns; fixed step budget, so the search is incomplete");
    public MathematicalFunction spaceComplexity { get; } = new("n * m", ", n = rows, m = columns");
    public bool timerHasExpired { get; set; }

    private const int UNLIT_WEIGHT = 1;
    private const int CONFLICT_WEIGHT = 2;
    private const int CLUE_WEIGHT = 2;

    // Annealing schedule.
    private const double START_TEMPERATURE = 3.0;
    private const double END_TEMPERATURE = 0.05;
    private const int MAX_ROUNDS = 40;
    private const int MIN_STEPS_PER_ROUND = 20_000;
    private const int STEPS_PER_CELL = 500;

    // Stands in for the instance until the first solve, so _p is never null.
    private static readonly AKARI NoInstance = new("((#))");

    private readonly int _seed;

    // --- Per-solve state ---
    private AKARI _p = NoInstance;                   // the instance and its geometry
    private bool[] _forbidden = Array.Empty<bool>(); // adjacent to a 0 clue: a bulb there is never valid
    private int[] _movable = Array.Empty<int>();     // white cells that may hold a bulb

    private bool[] _bulb = Array.Empty<bool>();
    private int[] _segBulbs = Array.Empty<int>();    // bulbs in each row or column segment
    private int[] _clueCount = Array.Empty<int>();   // bulbs adjacent to each clue
    private List<int> _bulbs = new();
    private int[] _bulbSlot = Array.Empty<int>();    // white index -> position in _bulbs, or -1
    private int _energy;
    private Random _rng = new(0);

    // --- Methods Including Constructors ---
    public AkariAnnealing() : this(20261109) { }

    /// <param name="seed">Seed for the random number generator, so runs are reproducible.</param>
    public AkariAnnealing(int seed) {
        _seed = seed;
    }

    public string solve(AKARI problem) {
        if (timerHasExpired) { return SolveRun.TimedOutAnswer; }

        Setup(problem);

        if (_p.WhiteCount == 0) {
            var none = Array.Empty<bool>();
            return _p.IsSolution(none) ? _p.Format(none) : AkariVerifier.NoSolution;
        }
        if (!CluesFeasible()) {
            return AkariVerifier.NoSolution;
        }

        Reset();
        if (_energy == 0) { return _p.Format(_bulb); }

        bool[] best = (bool[])_bulb.Clone();
        int bestEnergy = _energy;

        long stepsPerRound = Math.Max(MIN_STEPS_PER_ROUND, (long)STEPS_PER_CELL * _p.WhiteCount);
        double cooling = Math.Pow(END_TEMPERATURE / START_TEMPERATURE, 1.0 / stepsPerRound);

        for (int round = 0; round < MAX_ROUNDS; round++) {
            // Later rounds start from the best state seen, with a little less heat, so the search
            // both refines its best find and still escapes the local minimum it settled in.
            if (round > 0) { Load(best); }
            double temperature = START_TEMPERATURE * (round == 0 ? 1.0 : 0.6);

            for (long step = 0; step < stepsPerRound; step++) {
                if ((step & 1023) == 0 && timerHasExpired) {
                    return SolveRun.TimedOutAnswer;
                }

                Step(temperature);

                if (_energy < bestEnergy) {
                    bestEnergy = _energy;
                    Array.Copy(_bulb, best, best.Length);
                    if (bestEnergy == 0) { return _p.Format(_bulb); }
                }
                temperature *= cooling;
            }
        }

        return AkariVerifier.NoSolution;
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    // Takes the instance's geometry and works out which cells may hold a bulb.
    private void Setup(AKARI problem) {
        _p = problem;
        _rng = new Random(_seed);
        _forbidden = new bool[_p.WhiteCount];
        for (int i = 0; i < _p.WhiteCount; i++) {
            foreach (int k in _p.CellClues[i]) {
                if (_p.ClueValue[k] == 0) { _forbidden[i] = true; }
            }
        }
        _movable = Enumerable.Range(0, _p.WhiteCount).Where(i => !_forbidden[i]).ToArray();
    }

    // A clue asking for more bulbs than it has usable neighbours can never be met.
    private bool CluesFeasible() {
        for (int k = 0; k < _p.ClueValue.Length; k++) {
            if (_p.ClueNeighbors[k].Count(i => !_forbidden[i]) < _p.ClueValue[k]) { return false; }
        }
        return true;
    }

    // Starting state: no bulbs, then a randomized greedy pass that lights cells without conflicts.
    private void Reset() {
        _bulb = new bool[_p.WhiteCount];
        _segBulbs = new int[_p.SegmentCells.Length];
        _clueCount = new int[_p.ClueValue.Length];
        _bulbs = new List<int>();
        _bulbSlot = Enumerable.Repeat(-1, _p.WhiteCount).ToArray();
        _energy = UNLIT_WEIGHT * _p.WhiteCount;
        foreach (int v in _p.ClueValue) { _energy += CLUE_WEIGHT * v; }

        foreach (int i in _movable.OrderBy(_ => _rng.Next())) {
            if (!Lit(i) && ToggleDelta(i) < 0) { Toggle(i); }
        }
    }

    // Rebuilds the cached state for a given bulb placement.
    private void Load(bool[] placement) {
        for (int i = 0; i < _p.WhiteCount; i++) {
            if (_bulb[i] != placement[i]) { Toggle(i); }
        }
    }

    // ------------------------------------------------------------------
    // Segment queries
    // ------------------------------------------------------------------

    // Lit when either of the cell's segments holds a bulb (a bulb lights its own cell too).
    private bool Lit(int i) => _segBulbs[_p.RowSegment[i]] > 0 || _segBulbs[_p.ColSegment[i]] > 0;

    // A bulb that shares a segment with another bulb.
    private bool InConflict(int i) => _bulb[i] && (_segBulbs[_p.RowSegment[i]] > 1 || _segBulbs[_p.ColSegment[i]] > 1);

    // The number of cells that i sees: its two segments, without i itself (which is in both).
    private int SightCount(int i) => _p.SegmentCells[_p.RowSegment[i]].Length + _p.SegmentCells[_p.ColSegment[i]].Length - 2;

    // A uniformly random cell that i sees, or -1 when it sees none. Draws from the two segments end to end and draws
    // again on i itself; i is at most two of the entries and the rest number at least one, so this takes at most three
    // draws on average and allocates nothing.
    private int RandomSightCell(int i) {
        int[] row = _p.SegmentCells[_p.RowSegment[i]];
        int[] col = _p.SegmentCells[_p.ColSegment[i]];
        int total = row.Length + col.Length;
        if (total == 2) { return -1; }
        while (true) {
            int k = _rng.Next(total);
            int u = k < row.Length ? row[k] : col[k - row.Length];
            if (u != i) { return u; }
        }
    }

    // ------------------------------------------------------------------
    // Energy bookkeeping
    // ------------------------------------------------------------------

    // Change in energy if cell i were toggled. Does not modify state.
    private int ToggleDelta(int i) {
        int rowSeg = _p.RowSegment[i], colSeg = _p.ColSegment[i];
        int change = _bulb[i] ? -1 : 1;
        int rowBefore = _segBulbs[rowSeg], colBefore = _segBulbs[colSeg];
        int rowAfter = rowBefore + change, colAfter = colBefore + change;

        // Conflicts are the bulb pairs sharing a segment, sum of C(count, 2) over segments. A segment going from s to
        // s + 1 bulbs gains s pairs; one going from s to s - 1 loses s - 1.
        int delta = change > 0
            ? CONFLICT_WEIGHT * (rowBefore + colBefore)
            : -CONFLICT_WEIGHT * (rowAfter + colAfter);

        // Unlit cells change only in a segment that gains its first bulb or loses its last. The two segments share
        // exactly one cell, i, so it is counted in the row walk and skipped in the column walk when both flip.
        bool rowFlips = (rowBefore == 0) != (rowAfter == 0);
        bool colFlips = (colBefore == 0) != (colAfter == 0);
        if (rowFlips) {
            foreach (int u in _p.SegmentCells[rowSeg]) { delta += UnlitDelta(u, rowSeg, rowAfter, colSeg, colAfter); }
        }
        if (colFlips) {
            foreach (int u in _p.SegmentCells[colSeg]) {
                if (rowFlips && u == i) { continue; }
                delta += UnlitDelta(u, rowSeg, rowAfter, colSeg, colAfter);
            }
        }

        foreach (int k in _p.CellClues[i]) {
            int cnt = _clueCount[k], v = _p.ClueValue[k];
            delta += CLUE_WEIGHT * (Math.Abs(cnt + change - v) - Math.Abs(cnt - v));
        }
        return delta;
    }

    // The unlit-term change for cell u when segment rowSeg will hold rowAfter bulbs and colSeg colAfter. Segment ids
    // share one space, but a row segment id is never a column segment id, so only u's own kind is compared.
    private int UnlitDelta(int u, int rowSeg, int rowAfter, int colSeg, int colAfter) {
        int ur = _p.RowSegment[u], uc = _p.ColSegment[u];
        bool litBefore = _segBulbs[ur] > 0 || _segBulbs[uc] > 0;
        bool litAfter = (ur == rowSeg ? rowAfter : _segBulbs[ur]) > 0 || (uc == colSeg ? colAfter : _segBulbs[uc]) > 0;
        if (litBefore == litAfter) { return 0; }
        return litAfter ? -UNLIT_WEIGHT : UNLIT_WEIGHT;
    }

    // Toggles cell i and updates all cached counts and the energy.
    private void Toggle(int i) {
        _energy += ToggleDelta(i);
        int change = _bulb[i] ? -1 : 1;
        _bulb[i] = !_bulb[i];
        _segBulbs[_p.RowSegment[i]] += change;
        _segBulbs[_p.ColSegment[i]] += change;
        foreach (int k in _p.CellClues[i]) { _clueCount[k] += change; }

        if (change > 0) {
            _bulbSlot[i] = _bulbs.Count;
            _bulbs.Add(i);
        } else {
            int slot = _bulbSlot[i];
            int last = _bulbs[^1];
            _bulbs[slot] = last;
            _bulbSlot[last] = slot;
            _bulbs.RemoveAt(_bulbs.Count - 1);
            _bulbSlot[i] = -1;
        }
    }

    /// <summary>
    /// The energy of the current placement, recomputed from <c>_bulb</c> alone without the cached counts. Tests compare
    /// it with the incrementally maintained energy.
    /// </summary>
    internal int EnergyFromScratch() {
        var segBulbs = new int[_p.SegmentCells.Length];
        for (int i = 0; i < _p.WhiteCount; i++) {
            if (!_bulb[i]) { continue; }
            segBulbs[_p.RowSegment[i]]++;
            segBulbs[_p.ColSegment[i]]++;
        }
        int energy = 0;
        for (int i = 0; i < _p.WhiteCount; i++) {
            if (segBulbs[_p.RowSegment[i]] == 0 && segBulbs[_p.ColSegment[i]] == 0) { energy += UNLIT_WEIGHT; }
        }
        foreach (int s in segBulbs) { energy += CONFLICT_WEIGHT * s * (s - 1) / 2; }
        for (int k = 0; k < _p.ClueValue.Length; k++) {
            int cnt = _p.ClueNeighbors[k].Count(i => _bulb[i]);
            energy += CLUE_WEIGHT * Math.Abs(cnt - _p.ClueValue[k]);
        }
        return energy;
    }

    // Test hooks: start on an instance without searching, toggle a cell, read the cached energy.
    internal void StartForTest(AKARI problem) {
        Setup(problem);
        Reset();
    }

    internal void ToggleForTest(int i) => Toggle(i);

    internal int Energy => _energy;

    // ------------------------------------------------------------------
    // Moves
    // ------------------------------------------------------------------

    private void Step(double temperature) {
        if (_movable.Length == 0) { return; }

        double roll = _rng.NextDouble();
        if (_bulbs.Count > 0 && roll < 0.4) {
            SlideMove(temperature);
        } else if (roll < 0.7) {
            // Focused toggle: pick a cell from the neighbourhood of a random rule violation.
            int i = PickTroubledCell();
            TryToggle(i, temperature);
        } else {
            TryToggle(_movable[_rng.Next(_movable.Length)], temperature);
        }
    }

    private void TryToggle(int i, double temperature) {
        if (_forbidden[i]) { return; }
        int delta = ToggleDelta(i);
        if (Accept(delta, temperature)) { Toggle(i); }
    }

    // Moves a bulb to a cell in its line of sight or to a cell sharing a clue with it.
    private void SlideMove(double temperature) {
        int from = _bulbs[_rng.Next(_bulbs.Count)];
        int to = PickNear(from);
        if (to < 0 || to == from || _bulb[to] || _forbidden[to]) { return; }

        int delta = ToggleDelta(from);
        Toggle(from);
        delta += ToggleDelta(to);
        if (Accept(delta, temperature)) {
            Toggle(to);
        } else {
            Toggle(from);
        }
    }

    private int PickNear(int i) {
        int[] clues = _p.CellClues[i];
        if (clues.Length > 0 && _rng.Next(2) == 0) {
            int[] cells = _p.ClueNeighbors[clues[_rng.Next(clues.Length)]];
            return cells[_rng.Next(cells.Length)];
        }
        return RandomSightCell(i);
    }

    // Samples a few cells and returns one that is breaking a rule (dark, in conflict, or next to an
    // unsatisfied clue), mapped to a cell whose toggle could fix it. Falls back to a random cell.
    private int PickTroubledCell() {
        for (int attempt = 0; attempt < 8; attempt++) {
            int i = _rng.Next(_p.WhiteCount);
            if (!Lit(i)) {
                // Dark cell: light it, either directly or from somewhere in its line of sight.
                int sight = SightCount(i);
                int cell = _rng.Next(sight + 1) == sight ? i : RandomSightCell(i);
                if (!_forbidden[cell]) { return cell; }
            } else if (InConflict(i)) {
                return i;
            } else {
                foreach (int k in _p.CellClues[i]) {
                    if (_clueCount[k] != _p.ClueValue[k] && !_forbidden[i]) { return i; }
                }
            }
        }
        return _movable[_rng.Next(_movable.Length)];
    }

    private bool Accept(int delta, double temperature) {
        if (delta <= 0) { return true; }
        return _rng.NextDouble() < Math.Exp(-delta / temperature);
    }
}
