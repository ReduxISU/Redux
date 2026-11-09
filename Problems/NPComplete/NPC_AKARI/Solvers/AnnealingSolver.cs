using System.Text;
using API.Interfaces;

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
/// A state with energy 0 is a solution. Moves toggle one bulb, or slide a bulb to another cell
/// along its line of sight or next to it. Energy changes are computed incrementally from cached
/// per-cell light counts and per-clue bulb counts, so a move costs O(rows + cols). Worse moves are
/// accepted with probability exp(-delta / T) as T cools geometrically; when a round cools without
/// finding a solution the search restarts from the best state seen and reheats.
/// </summary>
class AnnealingSolver : ISolver<AKARI> {

    // --- Fields ---
    public string solverName { get; } = "Akari Simulated Annealing";
    public string solverDefinition { get; } = "Places light bulbs with simulated annealing. The energy of a placement is the number of unlit white cells, plus the number of bulb pairs that light each other, plus the total distance of each clue wall from its clue. Moves toggle a bulb or slide one along its line of sight; worse placements are accepted with probability exp(-delta/T) as the temperature cools, with reheating restarts. An energy of zero is a valid solution. Being stochastic and incomplete, the solver may fail to find a solution that exists, and cannot prove that none exists.";
    public string source { get; } = "Kirkpatrick, S., C. D. Gelatt, and M. P. Vecchi. 1983. \"Optimization by Simulated Annealing.\" Science 220 (4598): 671-680.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    public SolverType solverType { get; } = SolverType.Heuristic | SolverType.Stochastic;
    public bool timerHasExpired { get; set; }

    /// <summary>Character written into the returned grid for a cell holding a bulb.</summary>
    public const char BULB = 'L';

    /// <summary>Returned when the search budget runs out without reaching energy 0.</summary>
    public const string NO_SOLUTION = "{}";

    private const int UNLIT_WEIGHT = 1;
    private const int CONFLICT_WEIGHT = 2;
    private const int CLUE_WEIGHT = 2;

    // Annealing schedule.
    private const double START_TEMPERATURE = 3.0;
    private const double END_TEMPERATURE = 0.05;
    private const int MAX_ROUNDS = 40;
    private const int MIN_STEPS_PER_ROUND = 20_000;
    private const int STEPS_PER_CELL = 500;

    private readonly int _seed;

    // --- Per-solve state ---
    private int _rows, _cols;
    private int[][] _grid = Array.Empty<int[]>();
    private int _whiteCount;
    private int[,] _cellIndex = new int[0, 0];     // grid position -> white index, or -1
    private (int r, int c)[] _position = Array.Empty<(int, int)>();
    private int[][] _sight = Array.Empty<int[]>();  // white index -> white cells it lights (excluding itself)
    private int[][] _cluesOf = Array.Empty<int[]>(); // white index -> adjacent clue indices
    private int[][] _cellsOfClue = Array.Empty<int[]>(); // clue index -> adjacent white cells
    private int[] _clueValue = Array.Empty<int>();
    private bool[] _forbidden = Array.Empty<bool>(); // adjacent to a 0 clue: a bulb there is never valid
    private int[] _movable = Array.Empty<int>();     // white cells that may hold a bulb

    private bool[] _bulb = Array.Empty<bool>();
    private int[] _litBy = Array.Empty<int>();       // number of bulbs lighting each white cell (itself included)
    private int[] _clueCount = Array.Empty<int>();   // bulbs adjacent to each clue
    private List<int> _bulbs = new();
    private int[] _bulbSlot = Array.Empty<int>();    // white index -> position in _bulbs, or -1
    private int _energy;
    private Random _rng = new(0);

    // --- Methods Including Constructors ---
    public AnnealingSolver() : this(20261109) { }

    /// <param name="seed">Seed for the random number generator, so runs are reproducible.</param>
    public AnnealingSolver(int seed) {
        _seed = seed;
    }

    public string solve(AKARI problem) {
        Build(problem.grid);
        _rng = new Random(_seed);

        if (_whiteCount == 0) {
            return ClueCheckWithoutBulbs() ? Render() : NO_SOLUTION;
        }
        if (!CluesFeasible()) {
            return NO_SOLUTION;
        }

        Reset();
        if (_energy == 0) { return Render(); }

        bool[] best = (bool[])_bulb.Clone();
        int bestEnergy = _energy;

        long stepsPerRound = Math.Max(MIN_STEPS_PER_ROUND, (long)STEPS_PER_CELL * _whiteCount);
        double cooling = Math.Pow(END_TEMPERATURE / START_TEMPERATURE, 1.0 / stepsPerRound);

        for (int round = 0; round < MAX_ROUNDS; round++) {
            // Later rounds start from the best state seen, with a little less heat, so the search
            // both refines its best find and still escapes the local minimum it settled in.
            if (round > 0) { Load(best); }
            double temperature = START_TEMPERATURE * (round == 0 ? 1.0 : 0.6);

            for (long step = 0; step < stepsPerRound; step++) {
                if ((step & 1023) == 0 && timerHasExpired) {
                    return "timeout";
                }

                Step(temperature);

                if (_energy < bestEnergy) {
                    bestEnergy = _energy;
                    Array.Copy(_bulb, best, best.Length);
                    if (bestEnergy == 0) { return Render(); }
                }
                temperature *= cooling;
            }
        }

        return NO_SOLUTION;
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void Build(int[][] grid) {
        _grid = grid;
        _rows = grid.Length;
        _cols = _rows == 0 ? 0 : grid[0].Length;

        _cellIndex = new int[_rows, _cols];
        var positions = new List<(int, int)>();
        var clueIndex = new int[_rows, _cols];
        var clueValues = new List<int>();
        for (int r = 0; r < _rows; r++) {
            for (int c = 0; c < _cols; c++) {
                _cellIndex[r, c] = -1;
                clueIndex[r, c] = -1;
                if (grid[r][c] == AKARI.EMPTY_CELL) {
                    _cellIndex[r, c] = positions.Count;
                    positions.Add((r, c));
                } else if (grid[r][c] >= 0) {
                    clueIndex[r, c] = clueValues.Count;
                    clueValues.Add(grid[r][c]);
                }
            }
        }
        _whiteCount = positions.Count;
        _position = positions.ToArray();
        _clueValue = clueValues.ToArray();

        // Line of sight: walk the four directions from each white cell until a wall or the edge.
        _sight = new int[_whiteCount][];
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        for (int i = 0; i < _whiteCount; i++) {
            var (r, c) = _position[i];
            var seen = new List<int>();
            for (int d = 0; d < 4; d++) {
                int nr = r + dr[d], nc = c + dc[d];
                while (nr >= 0 && nr < _rows && nc >= 0 && nc < _cols && _cellIndex[nr, nc] >= 0) {
                    seen.Add(_cellIndex[nr, nc]);
                    nr += dr[d];
                    nc += dc[d];
                }
            }
            _sight[i] = seen.ToArray();
        }

        // Clue adjacency.
        var cellsOfClue = new List<int>[_clueValue.Length];
        for (int k = 0; k < cellsOfClue.Length; k++) { cellsOfClue[k] = new List<int>(); }
        _cluesOf = new int[_whiteCount][];
        _forbidden = new bool[_whiteCount];
        for (int i = 0; i < _whiteCount; i++) {
            var (r, c) = _position[i];
            var clues = new List<int>();
            for (int d = 0; d < 4; d++) {
                int nr = r + dr[d], nc = c + dc[d];
                if (nr < 0 || nr >= _rows || nc < 0 || nc >= _cols) { continue; }
                int k = clueIndex[nr, nc];
                if (k < 0) { continue; }
                clues.Add(k);
                cellsOfClue[k].Add(i);
                if (_clueValue[k] == 0) { _forbidden[i] = true; }
            }
            _cluesOf[i] = clues.ToArray();
        }
        _cellsOfClue = cellsOfClue.Select(l => l.ToArray()).ToArray();
        _movable = Enumerable.Range(0, _whiteCount).Where(i => !_forbidden[i]).ToArray();
    }

    // A clue asking for more bulbs than it has usable neighbours can never be met.
    private bool CluesFeasible() {
        for (int k = 0; k < _clueValue.Length; k++) {
            if (_cellsOfClue[k].Count(i => !_forbidden[i]) < _clueValue[k]) { return false; }
        }
        return true;
    }

    private bool ClueCheckWithoutBulbs() => _clueValue.All(v => v == 0);

    // Starting state: no bulbs, then a randomized greedy pass that lights cells without conflicts.
    private void Reset() {
        _bulb = new bool[_whiteCount];
        _litBy = new int[_whiteCount];
        _clueCount = new int[_clueValue.Length];
        _bulbs = new List<int>();
        _bulbSlot = Enumerable.Repeat(-1, _whiteCount).ToArray();
        _energy = UNLIT_WEIGHT * _whiteCount;
        for (int k = 0; k < _clueValue.Length; k++) { _energy += CLUE_WEIGHT * _clueValue[k]; }

        foreach (int i in _movable.OrderBy(_ => _rng.Next())) {
            if (_litBy[i] == 0 && ToggleDelta(i) < 0) { Toggle(i); }
        }
    }

    // Rebuilds the cached state for a given bulb placement.
    private void Load(bool[] placement) {
        for (int i = 0; i < _whiteCount; i++) {
            if (_bulb[i] != placement[i]) { Toggle(i); }
        }
    }

    // ------------------------------------------------------------------
    // Energy bookkeeping
    // ------------------------------------------------------------------

    // Change in energy if cell i were toggled. Does not modify state.
    private int ToggleDelta(int i) {
        int delta = 0;
        if (!_bulb[i]) {
            // Adding: cells that were dark become lit; each bulb already lighting i is a new conflict.
            if (_litBy[i] == 0) { delta -= UNLIT_WEIGHT; }
            foreach (int j in _sight[i]) {
                if (_litBy[j] == 0) { delta -= UNLIT_WEIGHT; }
            }
            delta += CONFLICT_WEIGHT * _litBy[i];
            foreach (int k in _cluesOf[i]) {
                int cnt = _clueCount[k], v = _clueValue[k];
                delta += CLUE_WEIGHT * (Math.Abs(cnt + 1 - v) - Math.Abs(cnt - v));
            }
        } else {
            // Removing: cells lit only by i go dark; conflicts with the other bulbs lighting i vanish.
            if (_litBy[i] == 1) { delta += UNLIT_WEIGHT; }
            foreach (int j in _sight[i]) {
                if (_litBy[j] == 1) { delta += UNLIT_WEIGHT; }
            }
            delta -= CONFLICT_WEIGHT * (_litBy[i] - 1);
            foreach (int k in _cluesOf[i]) {
                int cnt = _clueCount[k], v = _clueValue[k];
                delta += CLUE_WEIGHT * (Math.Abs(cnt - 1 - v) - Math.Abs(cnt - v));
            }
        }
        return delta;
    }

    // Toggles cell i and updates all cached counts and the energy.
    private void Toggle(int i) {
        _energy += ToggleDelta(i);
        int change = _bulb[i] ? -1 : 1;
        _bulb[i] = !_bulb[i];
        _litBy[i] += change;
        foreach (int j in _sight[i]) { _litBy[j] += change; }
        foreach (int k in _cluesOf[i]) { _clueCount[k] += change; }

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
        int[] clues = _cluesOf[i];
        if (clues.Length > 0 && _rng.Next(2) == 0) {
            int[] cells = _cellsOfClue[clues[_rng.Next(clues.Length)]];
            return cells[_rng.Next(cells.Length)];
        }
        int[] sight = _sight[i];
        return sight.Length == 0 ? -1 : sight[_rng.Next(sight.Length)];
    }

    // Samples a few cells and returns one that is breaking a rule (dark, in conflict, or next to an
    // unsatisfied clue), mapped to a cell whose toggle could fix it. Falls back to a random cell.
    private int PickTroubledCell() {
        for (int attempt = 0; attempt < 8; attempt++) {
            int i = _rng.Next(_whiteCount);
            if (_litBy[i] == 0) {
                // Dark cell: light it, either directly or from somewhere in its line of sight.
                int[] sight = _sight[i];
                int pick = _rng.Next(sight.Length + 1);
                int cell = pick == sight.Length ? i : sight[pick];
                if (!_forbidden[cell]) { return cell; }
            } else if (_bulb[i] && _litBy[i] > 1) {
                return i; // bulb in conflict
            } else {
                foreach (int k in _cluesOf[i]) {
                    if (_clueCount[k] != _clueValue[k] && !_forbidden[i]) { return i; }
                }
            }
        }
        return _movable[_rng.Next(_movable.Length)];
    }

    private bool Accept(int delta, double temperature) {
        if (delta <= 0) { return true; }
        return _rng.NextDouble() < Math.Exp(-delta / temperature);
    }

    // ------------------------------------------------------------------
    // Output
    // ------------------------------------------------------------------

    // The input grid in its own format, with each bulb cell written as BULB.
    private string Render() {
        var sb = new StringBuilder("(");
        for (int r = 0; r < _rows; r++) {
            if (r > 0) { sb.Append(','); }
            sb.Append('(');
            for (int c = 0; c < _cols; c++) {
                if (c > 0) { sb.Append(','); }
                int v = _grid[r][c];
                int i = _cellIndex[r, c];
                sb.Append(v switch {
                    AKARI.EMPTY_CELL => i >= 0 && _bulb.Length > i && _bulb[i] ? BULB : '_',
                    AKARI.WALL => '#',
                    _ => (char)('0' + v),
                });
            }
            sb.Append(')');
        }
        sb.Append(')');
        return sb.ToString();
    }
}
