using System.Text;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_AKARI;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_AKARI;

/// <summary>
/// Reduces CNF SAT to Akari (Light Up). The full correctness and running-time proof is in
/// Documentation/proofs/sat-to-akari.md; this comment summarises the construction.
/// <para>
/// Every white region of the produced grid is a <b>wire</b>: a straight run of white cells, one cell wide, with
/// walls (or clues) along both long sides. A wire is one segment, so it holds at most one bulb, and only its two
/// end cells ever touch a clue. Vertical and horizontal wires may cross at a single white cell. All clues are 1 or 2.
/// </para>
/// <list type="bullet">
/// <item><b>Literal occurrences</b> each get their own column. Columns are grouped by variable; within a variable the
/// negative occurrences come first.</item>
/// <item><b>Variable chain</b> (row 1): for a variable with m occurrences, horizontal wires h_0 .. h_m separated by
/// clue-1 junctions, one junction per occurrence. The value of the variable is "the bulb of h_k is at its right
/// end". A junction forces h_{k+1}'s left end to be the opposite of h_k's right end, which copies the value along.</item>
/// <item><b>Occurrence clue</b> (row 2): a clue 1 under the junction's left cell (positive occurrence) or right cell
/// (negative occurrence) and above the top cell of the occurrence's vertical wire. It makes that top cell the
/// negation of the literal, so the wire's bulb sits at its bottom end exactly when the literal is true.</item>
/// <item><b>Clause</b> c (rows 4c+4 .. 4c+7): its occurrence wires end on row R = 4c+4. Under the j-th end, in
/// column order, is a clause clue (row R+1); consecutive clause clues are joined by horizontal carry wires, which
/// may cross the vertical wires of later clauses. The carry wire leaving clue j means "one of the first j literals
/// is true". The first clue is a 1 (carry = literal 1), every later clue a 2 with a free two-cell slack wire below
/// it (carry out = carry in OR literal j), and the last clue has no carry out, which forces its carry in OR its
/// literal to be true.</item>
/// </list>
/// The grid is (4m + 5) rows by O(L) columns, where m is the number of clauses and L the number of literal occurrences.
/// </summary>
class SATToAkariReduction : IReversibleReduction<SAT, AKARI, Assignment> {

    // --- Fields ---
    public string reductionName { get; } = "Akari Wire Reduction";
    public string reductionDefinition { get; } = "Builds an Akari grid from a CNF formula out of one-cell-wide wires that each hold at most one bulb. Each variable is a row of wires joined by clue-1 junctions that copy its value, each literal occurrence is a vertical wire whose bulb sits at its bottom end exactly when the literal is true, and each clause is a chain of clue-2 cells joined by horizontal carry wires that can only be completed when one of its literal wires has its bulb at the bottom. The grid has a solution exactly when the formula is satisfiable.";
    public string source { get; } = "Construction written for Redux (proof in Documentation/proofs/sat-to-akari.md). The NP-completeness of Akari was first shown by Brandon McPhail, \"Light Up is NP-complete\", 2005, with a different, circuit-based set of gadgets.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Light_Up_(puzzle)";
    public string[] contributors { get; } = { "Danny Fawson" };
    // Declared, not derived. The grid is (4m + 5) rows by (5L + 2v + 1) columns: one row band of 4 per clause and
    // 5 columns per literal occurrence (plus 2 per variable), so its size is the product of the two.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;
    // Declared, not derived. Variable chains, occurrence wires and clause chains are gadgets that only work
    // together: the clause gadget reads the bulb position the variable gadget forces on each occurrence wire.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. reduce() writes every cell of the grid a constant number of times, and finding each
    // variable's occurrences scans all L of them once per variable.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(L * (m + n)), L = literal occurrences, m = clauses, n = variables of the SAT formula; the Akari grid has (4m + 5) * (5L + 2n + 1) cells";

    private SAT _reductionFrom;
    private AKARI _reductionTo;
    private Layout _layout = null!;

    // --- Properties ---
    public SAT reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public AKARI reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public SATToAkariReduction(SAT from) {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public SATToAkariReduction(string instance) : this(new SAT(instance)) { }
    public SATToAkariReduction() : this(new SAT()) { }

    #region Layout

    // Rows of the fixed part of the grid. Row 0 and the last row are a wall border.
    internal const int ChainRow = 1;
    internal const int OccurrenceClueRow = 2;
    internal const int WireTopRow = 3;
    // Clause c's occurrence wires end on row ClauseRow(c); its clues and carry wires are on the row below, and
    // the slack wires on the two rows below that.
    internal static int ClauseRow(int clause) => 4 + 4 * clause;

    /// <summary>One literal occurrence: clause index, position in the clause, whether it is negated, and its column.</summary>
    internal sealed record Occurrence(int Clause, int Position, bool Negated, int Column);

    /// <summary>
    /// A variable's chain on <see cref="ChainRow"/>: wire h_k spans columns <c>Bounds[k] + 1 .. Bounds[k + 1] - 1</c>,
    /// where <c>Bounds</c> is the column before the chain, then each junction, then the column after the chain.
    /// <see cref="Occurrences"/> lists the occurrences in junction order (negative ones first), and
    /// <see cref="Readout"/> is the index k of the wire whose right end gives the variable's value when mapping back:
    /// the wire just after the last negative occurrence.
    /// </summary>
    internal sealed record Chain(string Variable, int[] Bounds, Occurrence[] Occurrences) {
        public int Readout => Occurrences.Count(o => o.Negated);
        public int LeftEnd(int k) => Bounds[k] + 1;
        public int RightEnd(int k) => Bounds[k + 1] - 1;
    }

    /// <summary>The geometry of the produced grid, kept for mapping solutions both ways.</summary>
    internal sealed record Layout(int Rows, int Cols, Chain[] Chains, Occurrence[][] ClauseOccurrences);

    internal Layout layout => _layout;

    // Columns: each variable's chain starts at a cursor; its i-th junction is 3 + 5i columns in, its occurrence
    // column one to the left of the junction (positive) or right of it (negative). That keeps every two occurrence
    // columns at least 3 apart (so carry wires are at least 2 cells long and never end on another wire), and every
    // chain wire at least 2 cells long (so its two ends are different cells).
    private static Layout BuildLayout(CnfFormula formula) {
        var chains = new List<Chain>();
        var byClause = formula.Clauses.Select(c => new Occurrence[c.Literals.Count]).ToArray();
        int cursor = 1;
        foreach (string variable in formula.Variables) {
            var occurrences = new List<(int Clause, int Position, bool Negated)>();
            for (int c = 0; c < formula.Clauses.Count; c++) {
                var literals = formula.Clauses[c].Literals;
                for (int j = 0; j < literals.Count; j++) {
                    if (literals[j].Variable == variable) occurrences.Add((c, j, literals[j].Negated));
                }
            }
            // Negative occurrences first; OrderBy is stable, so each group keeps formula order.
            occurrences = occurrences.OrderBy(o => o.Negated ? 0 : 1).ToList();

            int start = cursor;
            var bounds = new int[occurrences.Count + 2];
            var placed = new Occurrence[occurrences.Count];
            bounds[0] = start - 1;
            for (int i = 0; i < occurrences.Count; i++) {
                int junction = start + 3 + 5 * i;
                bounds[i + 1] = junction;
                var (clause, position, negated) = occurrences[i];
                placed[i] = new Occurrence(clause, position, negated, negated ? junction + 1 : junction - 1);
                byClause[clause][position] = placed[i];
            }
            int end = bounds[occurrences.Count] + 2;
            bounds[occurrences.Count + 1] = end + 1;
            chains.Add(new Chain(variable, bounds, placed));
            cursor = end + 2; // one wall column between chains
        }
        // Inside a clause the gadget runs left to right, so order each clause's occurrences by column.
        var clauseOccurrences = byClause.Select(os => os.OrderBy(o => o.Column).ToArray()).ToArray();
        return new Layout(ClauseRow(formula.Clauses.Count - 1) + 5, cursor, chains.ToArray(), clauseOccurrences);
    }

    private static char[][] BuildGrid(Layout layout) {
        var grid = new char[layout.Rows][];
        for (int r = 0; r < layout.Rows; r++) {
            grid[r] = Enumerable.Repeat(AKARI.WALL_SYMBOL, layout.Cols).ToArray();
        }

        foreach (Chain chain in layout.Chains) {
            for (int col = chain.Bounds[0] + 1; col < chain.Bounds[^1]; col++) {
                grid[ChainRow][col] = AKARI.EMPTY_SYMBOL;
            }
            for (int i = 0; i < chain.Occurrences.Length; i++) {
                grid[ChainRow][chain.Bounds[i + 1]] = '1';                       // junction
                grid[OccurrenceClueRow][chain.Occurrences[i].Column] = '1';      // occurrence clue
            }
        }

        for (int c = 0; c < layout.ClauseOccurrences.Length; c++) {
            Occurrence[] occurrences = layout.ClauseOccurrences[c];
            int row = ClauseRow(c);
            for (int j = 0; j < occurrences.Length; j++) {
                int col = occurrences[j].Column;
                for (int r = WireTopRow; r <= row; r++) {
                    grid[r][col] = AKARI.EMPTY_SYMBOL;                           // occurrence wire
                }
                grid[row + 1][col] = j == 0 ? '1' : '2';                         // clause clue
                if (j > 0) {
                    grid[row + 2][col] = AKARI.EMPTY_SYMBOL;                     // slack wire
                    grid[row + 3][col] = AKARI.EMPTY_SYMBOL;
                }
                if (j + 1 < occurrences.Length) {
                    for (int k = col + 1; k < occurrences[j + 1].Column; k++) {
                        grid[row + 1][k] = AKARI.EMPTY_SYMBOL;                   // carry wire
                    }
                }
            }
        }
        return grid;
    }

    #endregion

    public AKARI reduce() {
        _layout = BuildLayout(_reductionFrom.formula);
        char[][] grid = BuildGrid(_layout);
        var sb = new StringBuilder("(");
        for (int r = 0; r < grid.Length; r++) {
            if (r > 0) sb.Append(',');
            sb.Append('(').Append(string.Join(",", grid[r])).Append(')');
        }
        sb.Append(')');
        _reductionTo = new AKARI(sb.ToString());
        return _reductionTo;
    }

    #region Forward map

    /// <summary>
    /// Maps a SAT assignment to the Akari solution built in the proof: every wire gets exactly one bulb, at one of
    /// its ends. Variables the certificate leaves out are taken as False; names that are not variables of the formula
    /// are ignored. The result solves the grid exactly when the assignment satisfies the formula.
    /// </summary>
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, bool> assignment = ParseAssignment(problemFromSolution);
        bool Value(string variable) => assignment.TryGetValue(variable, out bool v) && v;

        var bulbs = new List<(int Row, int Col)>();
        foreach (Chain chain in _layout.Chains) {
            bool value = Value(chain.Variable);
            for (int k = 0; k + 1 < chain.Bounds.Length; k++) {
                bulbs.Add((ChainRow, value ? chain.RightEnd(k) : chain.LeftEnd(k)));
            }
        }

        var formula = _reductionFrom.formula;
        for (int c = 0; c < _layout.ClauseOccurrences.Length; c++) {
            Occurrence[] occurrences = _layout.ClauseOccurrences[c];
            int row = ClauseRow(c);
            bool carry = false; // whether one of the literals so far is true
            for (int j = 0; j < occurrences.Length; j++) {
                Occurrence o = occurrences[j];
                CnfLiteral literal = formula.Clauses[c].Literals[o.Position];
                bool truth = Value(literal.Variable) != literal.Negated;
                bulbs.Add(truth ? (row, o.Column) : (WireTopRow, o.Column));

                bool carryIn = carry;
                carry = carry || truth;
                bool last = j + 1 == occurrences.Length;
                if (!last) {
                    bulbs.Add(carry ? (row + 1, occurrences[j + 1].Column - 1) : (row + 1, o.Column + 1));
                }
                if (j > 0) {
                    // Clue 2 counts carry in + literal + slack + (1 - carry out); the last clue has no carry out.
                    int slack = 2 - (carryIn ? 1 : 0) - (truth ? 1 : 0) - (last ? 0 : (carry ? 0 : 1));
                    // An unsatisfied clause needs a slack of 2 at its last clue; one bulb is the best the wire can do,
                    // and the verifier then rejects the grid, as it should.
                    bulbs.Add(slack >= 1 ? (row + 2, o.Column) : (row + 3, o.Column));
                }
            }
        }

        var bulb = new bool[_reductionTo.WhiteCount];
        foreach (var (r, col) in bulbs) {
            bulb[_reductionTo.WhiteIndex[r][col]] = true;
        }
        return _reductionTo.Format(bulb);
    }

    // Reads "(x1:True,x2:False)" the way SATVerifier does: ':' or '=' as separator, True/T/False/F as values.
    private Dictionary<string, bool> ParseAssignment(string certificate) {
        if (string.IsNullOrWhiteSpace(certificate)) {
            throw new ReductionInputException(this, certificate ?? "", _reductionFrom.certificateFormat, "the SAT assignment is empty");
        }
        var values = new Dictionary<string, bool>();
        string stripped = certificate.Replace(" ", "").Replace("(", "").Replace(")", "");
        foreach (string item in stripped.Split(',')) {
            if (item.Length == 0) continue;
            int sep = item.IndexOfAny(new[] { ':', '=' });
            if (sep <= 0) {
                throw new ReductionInputException(this, certificate, _reductionFrom.certificateFormat,
                    $"assignment '{item}' is missing a variable name and ':' or '=' separator");
            }
            string value = item[(sep + 1)..];
            values[item[..sep]] = value switch {
                "True" or "T" => true,
                "False" or "F" => false,
                _ => throw new ReductionInputException(this, certificate, _reductionFrom.certificateFormat,
                    $"assignment '{item}' has value '{value}'; expected True/False (capitalized) or T/F"),
            };
        }
        return values;
    }

    #endregion

    #region Backward map

    public Assignment EmptyAnswer() => new Assignment([]);

    /// <summary>
    /// Maps an Akari solution back to an assignment: each variable is true exactly when the bulb of its chain wire
    /// just after the last negative occurrence (<see cref="Chain.Readout"/>) sits at that wire's right end. The proof
    /// shows this satisfies the formula for every solution of the grid, including solutions that put a bulb in the
    /// middle of a wire or on a crossing.
    /// </summary>
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        int[][] parsed = AKARI.ParseGrid(problemToSolution, allowBulbs: true, detail => new FormatException(detail));
        bool[] bulb = _reductionTo.BulbsFrom(parsed)
            ?? throw new FormatException("the certificate grid does not match the reduced Akari instance (size, walls or clues differ, or a bulb is on a wall)");

        var values = new Dictionary<string, string>();
        Assignment Snapshot() => new Assignment(values);
        foreach (Chain chain in _layout.Chains) {
            int k = chain.Readout;
            int col = chain.RightEnd(k);
            bool value = bulb[_reductionTo.WhiteIndex[ChainRow][col]];
            string word = value ? "True" : "False";
            values[chain.Variable] = word;
            rec.Accept(Snapshot, () => value
                ? $"Chain wire {k} of {chain.Variable} has its bulb at its right end (row {ChainRow}, column {col}), so {chain.Variable} = True."
                : $"Chain wire {k} of {chain.Variable} has no bulb at its right end (row {ChainRow}, column {col}), so {chain.Variable} = False.");
        }
        string answer = "(" + string.Join(",", _layout.Chains.Select(ch => $"{ch.Variable}:{values[ch.Variable]}")) + ")";
        return (answer, Snapshot());
    }

    #endregion
}
