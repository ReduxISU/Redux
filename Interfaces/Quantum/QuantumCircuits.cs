using System.Globalization;

namespace API.Interfaces.Quantum;

/// <summary>A qubit's wire: its name (<c>x0</c>, <c>y</c>, <c>f0</c>, <c>c0</c>, <c>w0</c>) and its role (<c>query</c>,
/// <c>helper</c>, <c>output</c>, <c>index</c>, <c>count</c>, <c>work</c>).</summary>
sealed record QuantumWire(string Name, string Role);

/// <summary>
/// One gate: its type (<c>h</c>, <c>x</c>, <c>z</c>, <c>cx</c>, <c>measure</c> or <c>box</c>), the qubits it acts on (a box
/// spans them), an optional control qubit, an optional label (a box's text) and whether it is the oracle.
/// </summary>
sealed record QuantumGate(string Type, int[] Qubits, int? Control = null, string? Label = null, bool Oracle = false);

/// <summary>A labelled run of columns, <see cref="From"/> to <see cref="To"/> inclusive ("prepare", "query once", "read").</summary>
sealed record QuantumStage(string Label, int From, int To);

/// <summary>
/// One column of a circuit: its gates, what it does to the state (null when it only measures, or when the circuit is
/// drawn but not simulated), and how a step that applies it reads, given the outcome probabilities after it (null when
/// the circuit is not simulated).
/// </summary>
sealed class QuantumColumn {
    public IReadOnlyList<QuantumGate> Gates { get; }
    internal Action<QuantumState>? Apply { get; }
    internal Func<QuantumDistribution?, string> Caption { get; }

    public QuantumColumn(IReadOnlyList<QuantumGate> gates, Action<QuantumState>? apply, Func<QuantumDistribution?, string> caption) {
        Gates = gates;
        Apply = apply;
        Caption = caption;
    }

    /// <summary>Whether every gate of the column is a measurement.</summary>
    public bool Measures => Gates.Count > 0 && Gates.All(g => g.Type == "measure");
}

/// <summary>
/// A quantum-algorithm problem's circuit, as the Quantum Circuit picture draws it: one wire per qubit, the columns of
/// gates in the order they run, the stages, and the qubits the answer is read from. It is the textbook circuit for the
/// instance, built here from the instance alone (a quantum server compiles its own). An instance that has no circuit
/// (a table that is not a power of two long, a number that needs no quantum step) has no wires and no columns, and a
/// <see cref="Note"/> that says why.
/// </summary>
sealed class QuantumCircuitSpec {
    /// <summary>Circuits up to this many qubits are simulated (65,536 amplitudes), so frames carry outcome probabilities.</summary>
    public const int MaxSimulatedQubits = 16;
    /// <summary>Circuits up to this many qubits are drawn; a bigger instance gets a note instead.</summary>
    public const int MaxDrawnQubits = 24;

    /// <summary>Which problem: <c>deutsch</c>, <c>deutschJozsa</c>, <c>bernsteinVazirani</c>, <c>simon</c>,
    /// <c>unstructuredSearch</c> or <c>primeFactorization</c>.</summary>
    public string Problem { get; }
    public IReadOnlyList<QuantumWire> Wires { get; }
    public IReadOnlyList<QuantumColumn> Columns { get; }
    public IReadOnlyList<QuantumStage> Stages { get; }
    /// <summary>The qubits the answer is read from, the most significant first.</summary>
    public int[] Measure { get; }
    /// <summary><c>bits</c> (an outcome reads as a bit string) or <c>decimal</c> (as a number, Shor's counting register).</summary>
    public string Readout { get; }
    /// <summary>n, the number of input bits of the oracle f (0 when there is no oracle).</summary>
    public int InputBits { get; }
    /// <summary>Why there is no circuit, or what to know about it; null when there is nothing to say.</summary>
    public string? Note { get; }
    /// <summary>The number to factor (Prime Factorization only).</summary>
    public long? Number { get; }

    public QuantumCircuitSpec(string problem, IReadOnlyList<QuantumWire> wires, IReadOnlyList<QuantumColumn> columns,
        IReadOnlyList<QuantumStage> stages, int[] measure, string readout, int inputBits, string? note = null, long? number = null) {
        Problem = problem;
        Wires = wires;
        Columns = columns;
        Stages = stages;
        Measure = measure;
        Readout = readout;
        InputBits = inputBits;
        Note = note;
        Number = number;
    }

    /// <summary>A problem instance with no circuit, and why.</summary>
    public static QuantumCircuitSpec None(string problem, int inputBits, string note, long? number = null) =>
        new(problem, [], [], [], [], "bits", inputBits, note, number);

    /// <summary>Whether the circuit is simulated: it has columns, fits <see cref="MaxSimulatedQubits"/>, and every column
    /// that is not a measurement says what it does.</summary>
    public bool Simulated =>
        Wires.Count > 0 && Wires.Count <= MaxSimulatedQubits && Columns.Count > 0 && Columns.All(c => c.Apply != null || c.Measures);

    /// <summary>The id of the first oracle gate, or null when the circuit has none.</summary>
    public string? OracleGateId {
        get {
            for (int c = 0; c < Columns.Count; c++)
                foreach (var g in Columns[c].Gates)
                    if (g.Oracle) return QuantumCircuits.GateId(c, g);
            return null;
        }
    }

    /// <summary>The ids of column <paramref name="c"/> and its gates: what a step that applies the column focuses on.</summary>
    public string[] ColumnFocus(int c) =>
        [QuantumCircuits.ColumnId(c), .. Columns[c].Gates.Select(g => QuantumCircuits.GateId(c, g))];

    /// <summary>An outcome as captions write it: its bits (most significant first), or the number for a decimal readout.</summary>
    public string Outcome(long value) =>
        Readout == "decimal" ? value.ToString(CultureInfo.InvariantCulture) : QuantumCircuits.Bits(value, Measure.Length);
}

/// <summary>
/// The textbook circuits of the six quantum-algorithm problems, built from an instance (ported from the design mockup,
/// <c>Documentation/Visualizations/mockups/quantum.js</c>), and the names and ids they use.
/// <para>
/// <b>Ids</b> are positions: qubit k is <c>q{k}</c>, column c is <c>t{c}</c>, and a gate is <c>g{c}_{k}</c> for its
/// column and its first qubit (a qubit carries at most one gate per column, so ids are unique). No id is ever built
/// from the instance's text.
/// </para>
/// </summary>
static class QuantumCircuits {
    public static string QubitId(int k) => $"q{k}";
    public static string ColumnId(int c) => $"t{c}";
    public static string GateId(int column, QuantumGate gate) => $"g{column}_{gate.Qubits.Min()}";

    /// <summary><paramref name="value"/> as <paramref name="width"/> bits, most significant first.</summary>
    public static string Bits(long value, int width) =>
        width <= 0 ? "" : Convert.ToString(value, 2).PadLeft(width, '0');

    /// <summary>A probability as captions write it: <c>100%</c>, <c>47.3%</c>, <c>0%</c>.</summary>
    public static string Percent(double p) => (p * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>A count as captions write it, with thousands separators.</summary>
    public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>log2 of <paramref name="length"/> when it is a power of two (1, 2, 4, ...), else -1.</summary>
    public static int Log2(int length) =>
        length > 0 && (length & (length - 1)) == 0 ? System.Numerics.BitOperations.Log2((uint)length) : -1;

    private static QuantumGate H(int k) => new("h", [k]);
    private static QuantumGate X(int k) => new("x", [k]);
    private static QuantumGate Meter(int k) => new("measure", [k]);

    private static QuantumCircuitSpec None(string problem, int inputBits, string note, long? number = null) =>
        QuantumCircuitSpec.None(problem, inputBits, note, number);

    private static string Plural(long n, string one, string many) => n == 1 ? one : many;

    // "a", "a and b", "a, b and c".
    private static string And(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";

    // At most this many tied outcomes are named; more are counted (#907).
    private const int NamedTies = 4;

    /// <summary>
    /// The likeliest outcomes of <paramref name="d"/> as a Measure caption says them: <c>101 with probability 94.5%</c> when
    /// one outcome is likeliest, and when several share the top probability, all of them, never one picked from the tie
    /// (#907): <c>every item is equally likely (25% each)</c>, <c>01 and 10 are equally likely (50% each)</c>,
    /// <c>6 outcomes are equally likely (12.5% each)</c>. <paramref name="name"/> names an outcome, and
    /// <paramref name="what"/> is what an outcome is called (<c>item</c>, <c>outcome</c>).
    /// </summary>
    internal static string Likeliest(QuantumDistribution d, Func<long, string> name, string what) {
        var top = d.Likeliest;
        string p = Percent(d.Probabilities[top[0]]);
        if (top.Count == 1) return $"{name(top[0])} with probability {p}";
        if (top.Count == d.Probabilities.Length) return $"every {what} is equally likely ({p} each)";
        if (top.Count <= NamedTies) return $"{And(top.Select(name).ToList())} are equally likely ({p} each)";
        return $"{Count(top.Count)} {what}s are equally likely ({p} each)";
    }

    /// <summary>
    /// The likeliest outcomes of <paramref name="d"/> as a step that changes the probabilities says them:
    /// <c>101 now has probability 94.5%</c>, or on a tie <c>01 and 10 now have probability 50% each</c> (#907).
    /// </summary>
    internal static string NowLikeliest(QuantumDistribution d, Func<long, string> name, string what) {
        var top = d.Likeliest;
        string p = Percent(d.Probabilities[top[0]]);
        if (top.Count == 1) return $"{name(top[0])} now has probability {p}";
        if (top.Count == d.Probabilities.Length) return $"every {what} now has probability {p}";
        if (top.Count <= NamedTies) return $"{And(top.Select(name).ToList())} now have probability {p} each";
        return $"{Count(top.Count)} {what}s now have probability {p} each";
    }

    // ---- Deutsch, Deutsch-Jozsa, Bernstein-Vazirani: phase kickback ----

    /// <summary>Deutsch: f given as (f(0), f(1)).</summary>
    public static QuantumCircuitSpec Deutsch(IReadOnlyList<bool> f) =>
        PhaseKickback("deutsch", f.Select(b => b ? 1 : 0).ToArray());

    /// <summary>Deutsch-Jozsa: f given as its truth table. A circuit needs 2, 4, 8, ... entries, each 0 or 1.</summary>
    public static QuantumCircuitSpec DeutschJozsa(IReadOnlyList<int> table) {
        int n = Log2(table.Count);
        if (n < 1) return None("deutschJozsa", Math.Max(n, 0), TableNote(table.Count));
        if (table.Any(v => v is not (0 or 1))) return None("deutschJozsa", n, "f must output bits (0 or 1), so this table has no circuit.");
        return PhaseKickback("deutschJozsa", table.ToArray());
    }

    /// <summary>Bernstein-Vazirani: f given as its truth table (2, 4, 8, ... bits).</summary>
    public static QuantumCircuitSpec BernsteinVazirani(IReadOnlyList<bool> table) {
        int n = Log2(table.Count);
        if (n < 1) return None("bernsteinVazirani", Math.Max(n, 0), TableNote(table.Count));
        return PhaseKickback("bernsteinVazirani", table.Select(b => b ? 1 : 0).ToArray());
    }

    private static string TableNote(int length) => length == 1
        ? "A one-entry table has no input bits, so there is nothing for a circuit to query."
        : $"The table has {Count(length)} entries; a circuit needs a power of two (2, 4, 8, ...).";

    // |x>|y> -> |x>|y xor f(x)>: data on qubits [0, n), output on [n, n + m).
    private static Action<QuantumState> OracleXor(int m, IReadOnlyList<long> f) => st => st.Permute(i => {
        long mask = (1L << m) - 1;
        long x = i >> m, y = i & mask;
        return (x << m) | (y ^ f[(int)x]);
    });

    private static QuantumCircuitSpec PhaseKickback(string problem, int[] f) {
        int n = Log2(f.Length);
        if (n + 1 > QuantumCircuitSpec.MaxDrawnQubits)
            return None(problem, n, $"The circuit needs {n + 1} qubits, too many to draw here.");
        int N = 1 << n, helper = n, ones = f.Count(v => v == 1);
        var data = Enumerable.Range(0, n).ToArray();
        var all = Enumerable.Range(0, n + 1).ToArray();
        var wires = data.Select(k => new QuantumWire($"x{k}", "query")).Append(new QuantumWire("y", "helper")).ToList();
        bool deutsch = problem == "deutsch";
        string queryQubits = deutsch ? "x0" : "the query qubits";
        string inputs = deutsch ? "both inputs, 0 and 1," : $"all {Count(N)} inputs";
        var spec = default(QuantumCircuitSpec);
        string Outcome(long value) => spec!.Outcome(value);
        var columns = new List<QuantumColumn> {
            new([X(helper)], st => st.X(helper), _ => "Flip the helper qubit y to 1."),
            new(all.Select(H).ToList(), st => { foreach (int k in all) st.H(k); },
                _ => $"Apply H to every qubit: {queryQubits} now {(deutsch ? "holds" : "hold")} {inputs} at once, each with amplitude 1/√{N}, and y becomes |−⟩."),
            new([new QuantumGate("box", all, Label: "U_f", Oracle: true)], OracleXor(1, f.Select(v => (long)v).ToArray()),
                _ => $"One call to the oracle U_f. Because y is |−⟩, every input x with f(x) = 1 ({ones} of {Count(N)}) gets its sign flipped: phase kickback."),
            new(data.Select(H).ToList(), st => { foreach (int k in data) st.H(k); }, d => {
                string start = $"Apply H to {queryQubits} again. The signs interfere";
                if (d == null) return start + ".";
                return problem == "deutschJozsa"
                    ? $"{start}: the all-zero outcome {Bits(0, n)} now has probability {Percent(d.Probabilities[0])}."
                    : $"{start}: {NowLikeliest(d, Outcome, "outcome")}.";
            }),
            new(data.Select(Meter).ToList(), null, d => d == null
                ? $"Measure {queryQubits}."
                : $"Measure {queryQubits}: {Likeliest(d, Outcome, "outcome")}."),
        };
        var stages = new List<QuantumStage> { new("prepare", 0, 1), new("query once", 2, 2), new("interfere", 3, 3), new("read", 4, 4) };
        spec = new QuantumCircuitSpec(problem, wires, columns, stages, data, "bits", n);
        return spec;
    }

    // ---- Simon's Problem ----

    /// <summary>Simon's Problem: f given as its table of outputs (2, 4, 8, ... whole numbers, 0 or more).</summary>
    public static QuantumCircuitSpec Simon(IReadOnlyList<int> table) {
        int n = Log2(table.Count);
        if (n < 1) return None("simon", Math.Max(n, 0), TableNote(table.Count));
        if (table.Any(v => v < 0)) return None("simon", n, "f's outputs must be whole numbers, 0 or more, to be written into output qubits.");
        int m = Math.Max(1, 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)table.Max()));
        if (n + m > QuantumCircuitSpec.MaxDrawnQubits)
            return None("simon", n, $"The circuit needs {n + m} qubits ({n} query, {m} output), too many to draw here.");
        int N = 1 << n;
        var data = Enumerable.Range(0, n).ToArray();
        var outs = Enumerable.Range(n, m).ToArray();
        var wires = data.Select(k => new QuantumWire($"x{k}", "query")).Concat(outs.Select((q, k) => new QuantumWire($"f{k}", "output"))).ToList();
        var columns = new List<QuantumColumn> {
            new(data.Select(H).ToList(), st => { foreach (int k in data) st.H(k); },
                _ => $"Apply H to the query qubits: they now hold all {Count(N)} inputs at once, each with amplitude 1/√{N}."),
            new([new QuantumGate("box", [.. data, .. outs], Label: "U_f", Oracle: true)], OracleXor(m, table.Select(v => (long)v).ToArray()),
                _ => "One call to the oracle writes f(x) into the output qubits. Inputs with the same output are now tied together."),
            new(outs.Select(Meter).ToList(), null,
                _ => "Measuring the output (optional) leaves the query qubits holding one pair {x, x ⊕ s}."),
            new(data.Select(H).ToList(), st => { foreach (int k in data) st.H(k); }, d => d == null
                ? "Apply H to the query qubits again. Only outcomes y with y · s = 0 survive."
                : $"Apply H to the query qubits again. Only outcomes y with y · s = 0 survive: {d.Probabilities.Count(p => p > QuantumDistribution.Epsilon)} of {Count(N)}."),
            new(data.Select(Meter).ToList(), null,
                _ => "Measure the query qubits: whatever comes up, y · s = 0 (mod 2). Each run gives one such equation for s."),
        };
        var stages = new List<QuantumStage> { new("prepare", 0, 0), new("query once", 1, 1), new("interfere", 2, 3), new("read", 4, 4) };
        return new QuantumCircuitSpec("simon", wires, columns, stages, data, "bits", n);
    }

    // ---- Unstructured Search (Grover) ----

    /// <summary>The number of Grover rounds for <paramref name="marked"/> marked items of <paramref name="items"/>:
    /// ⌊π/4 · √(N/M)⌋, at least 1, and none when nothing or everything is marked.</summary>
    public static int GroverRounds(int items, int marked) =>
        marked == 0 || marked == items ? 0 : Math.Max(1, (int)Math.Floor(Math.PI / 4 * Math.Sqrt((double)items / marked)));

    /// <summary>Unstructured Search: f given as its table (2, 4, 8, ... entries; an entry other than 0 is marked).</summary>
    public static QuantumCircuitSpec UnstructuredSearch(IReadOnlyList<int> table) {
        int n = Log2(table.Count);
        if (n < 1) return None("unstructuredSearch", Math.Max(n, 0), TableNote(table.Count));
        if (n > QuantumCircuitSpec.MaxDrawnQubits)
            return None("unstructuredSearch", n, $"The circuit needs {n} qubits, too many to draw here.");
        int N = 1 << n;
        var marked = Enumerable.Range(0, N).Where(i => table[i] != 0).ToArray();
        int M = marked.Length, rounds = GroverRounds(N, M);
        var data = Enumerable.Range(0, n).ToArray();
        var wires = data.Select(k => new QuantumWire($"x{k}", "index")).ToList();
        // Qubit k holds x_k, bit k of the item (x0 the least significant), as the solver's SAT encoding numbers them. The
        // state's index has qubit 0 as its most significant bit, so item i is the index with i's n bits reversed, and the
        // answer is read high bit first (measure = x_{n-1}, ..., x0), so an outcome is the item number (#848).
        long Item(long index) {
            long item = 0;
            for (int k = 0; k < n; k++) item |= ((index >> (n - 1 - k)) & 1) << k;
            return item;
        }
        var highFirst = data.Reverse().ToArray();
        string items = Plural(M, "item", "items");
        double MarkedChance(QuantumDistribution d) => marked.Sum(i => d.Probabilities[i]);
        var columns = new List<QuantumColumn> {
            new(data.Select(H).ToList(), st => { foreach (int k in data) st.H(k); },
                _ => $"Apply H to every qubit: all {Count(N)} items are equally likely, {Percent(1.0 / N)} each."),
        };
        var stages = new List<QuantumStage> { new("spread", 0, 0) };
        var markedSet = marked.ToHashSet();
        for (int t = 1; t <= rounds; t++) {
            int round = t;
            columns.Add(new([new QuantumGate("box", data, Label: "Oracle", Oracle: true)], st => st.FlipSigns(i => markedSet.Contains((int)Item(i))),
                _ => $"Round {round}: the oracle flips the sign of the marked {items}. The probabilities stay the same; only the {Plural(M, "sign changes", "signs change")}."));
            columns.Add(new([new QuantumGate("box", data, Label: "Diffuse")], st => st.Diffuse(), d => d == null
                ? $"Round {round}: reflect every amplitude about the average. The marked {items} {Plural(M, "grows", "grow")}, the rest shrink."
                : $"Round {round}: reflect every amplitude about the average. The marked {items} now {Plural(M, "has", "have")} probability {Percent(MarkedChance(d))}."));
            stages.Add(new($"round {round}", columns.Count - 2, columns.Count - 1));
        }
        columns.Add(new(data.Select(Meter).ToList(), null, d => {
            if (M == 0) return "Measure. Nothing is marked, so every item is still equally likely: no outcome means anything.";
            if (d == null) return "Measure.";
            return $"Measure: {Likeliest(d, i => $"item {i} ({Bits(i, n)})", "item")}.";
        }));
        stages.Add(new("read", columns.Count - 1, columns.Count - 1));
        return new QuantumCircuitSpec("unstructuredSearch", wires, columns, stages, highFirst, "bits", n);
    }

    // ---- Prime Factorization (Shor) ----

    /// <summary>
    /// Prime Factorization: the period-finding circuit of Shor's algorithm for the first part of the number
    /// that needs one (odd, not prime, not a prime power), with the smallest base a that works, as the mockup picks it. The
    /// circuit is drawn, not simulated. A number the classical checks factor on their own, or whose circuit is too big to
    /// draw, has no circuit and a note.
    /// </summary>
    public static QuantumCircuitSpec PrimeFactorization(string instance) {
        if (!long.TryParse(instance.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) || number < 2)
            return None("primeFactorization", 0, "Only whole numbers of at least 2 have prime factors to find.",
                long.TryParse(instance.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long given) ? given : null);
        long odd = number;
        while (odd % 2 == 0) odd /= 2;
        if (odd == 1 || IsPrime(odd) || PrimePowerBase(odd) != null)
            return None("primeFactorization", 0, $"No quantum circuit is needed for {Count(number)}: the classical checks factor it completely.", number);
        int nb = 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)odd), t = 2 * nb, Q = t + nb;
        if (Q > QuantumCircuitSpec.MaxDrawnQubits)
            return None("primeFactorization", 0, $"Shor's algorithm finds a period for {Count(odd)} on {Q} qubits, too many to draw here.", number);
        // The smallest base a with an even order r and a^(r/2) not -1 (mod odd): a real run picks a at random.
        long a = 0, r = 0;
        for (long c = 2; c < odd; c++) {
            if (Gcd(c, odd) > 1) continue;
            long k = 1;
            while (ModPow(c, k, odd) != 1) k++;
            if (k % 2 == 1 || ModPow(c, k / 2, odd) == odd - 1) continue;
            a = c;
            r = k;
            break;
        }
        var count = Enumerable.Range(0, t).ToArray();
        var work = Enumerable.Range(t, nb).ToArray();
        var wires = count.Select(k => new QuantumWire($"c{k}", "count")).Concat(work.Select((q, k) => new QuantumWire($"w{k}", "work"))).ToList();
        var columns = new List<QuantumColumn> {
            new([X(work[^1])], null, _ => "Set the work register to 1."),
            new(count.Select(H).ToList(), null, _ => $"Apply H to the {t} counting qubits: every exponent from 0 to {Count((1L << t) - 1)} at once."),
        };
        for (int j = 0; j < t; j++) {
            int ctrl = t - 1 - j;
            // The power is fixed here: the caption runs later, when the loop's j has moved on (#903).
            long power = 1L << j;
            long mult = ModPow(a, power, odd);
            columns.Add(new([new QuantumGate("box", work, Control: ctrl, Label: $"×{mult}")], null,
                _ => $"If c{ctrl} is 1, multiply the work register by {a}^{power} mod {odd} = {mult}.{(mult == 1 ? " That is ×1, so nothing happens." : "")}"));
        }
        columns.Add(new([new QuantumGate("box", count, Label: "QFT†")], null,
            _ => $"Inverse quantum Fourier transform on the counting qubits: the period {r} shows up as evenly spaced peaks."));
        columns.Add(new(count.Select(Meter).ToList(), null, _ => "Measure the counting qubits."));
        var stages = new List<QuantumStage> {
            new("prepare", 0, 1), new($"a^x mod {odd}", 2, 1 + t), new("find period", 2 + t, 2 + t), new("read", 3 + t, 3 + t),
        };
        return new QuantumCircuitSpec("primeFactorization", wires, columns, stages, count, "decimal", 0,
            $"Shor's period finding for {Count(odd)} with a = {a}, whose period is {r}. A real run picks a at random.", number);
    }

    private static bool IsPrime(long x) {
        if (x < 2) return false;
        for (long d = 2; d * d <= x; d++)
            if (x % d == 0) return false;
        return true;
    }

    // The base c when x = c^k for some k >= 2, else null.
    private static long? PrimePowerBase(long x) {
        for (int k = 2; (1L << k) <= x; k++) {
            long b = (long)Math.Round(Math.Pow(x, 1.0 / k));
            for (long c = Math.Max(2, b - 1); c <= b + 1; c++) {
                long p = 1;
                for (int i = 0; i < k && p <= x; i++) p *= c;
                if (p == x) return c;
            }
        }
        return null;
    }

    private static long Gcd(long a, long b) {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    private static long ModPow(long a, long e, long m) {
        long r = 1 % m;
        a %= m;
        while (e > 0) {
            if ((e & 1) == 1) r = r * a % m;
            a = a * a % m;
            e >>= 1;
        }
        return r;
    }
}

/// <summary>
/// A state vector of Q qubits. Qubit k is bit (Q - 1 - k) of a basis index, so q0 is the most significant bit, as the
/// mockup draws it.
/// </summary>
sealed class QuantumState {
    public int Qubits { get; }
    internal double[] Re;
    internal double[] Im;

    public QuantumState(int qubits) {
        Qubits = qubits;
        Re = new double[1 << qubits];
        Im = new double[1 << qubits];
        Re[0] = 1;
    }

    private long Bit(int k) => 1L << (Qubits - 1 - k);

    public void H(int k) {
        long b = Bit(k);
        double s = Math.Sqrt(0.5);
        for (long i = 0; i < Re.Length; i++) {
            if ((i & b) != 0) continue;
            long j = i | b;
            double ar = Re[i], ai = Im[i], br = Re[j], bi = Im[j];
            Re[i] = (ar + br) * s;
            Im[i] = (ai + bi) * s;
            Re[j] = (ar - br) * s;
            Im[j] = (ai - bi) * s;
        }
    }

    public void X(int k) {
        long b = Bit(k);
        for (long i = 0; i < Re.Length; i++) {
            if ((i & b) != 0) continue;
            long j = i | b;
            (Re[i], Re[j]) = (Re[j], Re[i]);
            (Im[i], Im[j]) = (Im[j], Im[i]);
        }
    }

    /// <summary>Moves the amplitude of basis state i to <paramref name="to"/>(i), a permutation.</summary>
    public void Permute(Func<long, long> to) {
        var re = new double[Re.Length];
        var im = new double[Im.Length];
        for (long i = 0; i < Re.Length; i++) {
            if (Re[i] == 0 && Im[i] == 0) continue;
            long j = to(i);
            re[j] += Re[i];
            im[j] += Im[i];
        }
        Re = re;
        Im = im;
    }

    /// <summary>Flips the sign of every basis state for which <paramref name="flip"/> holds.</summary>
    public void FlipSigns(Func<long, bool> flip) {
        for (long i = 0; i < Re.Length; i++)
            if (flip(i)) {
                Re[i] = -Re[i];
                Im[i] = -Im[i];
            }
    }

    /// <summary>Grover's diffusion on every qubit: each amplitude becomes twice the average minus itself.</summary>
    public void Diffuse() {
        double mr = Re.Average(), mi = Im.Average();
        for (long i = 0; i < Re.Length; i++) {
            Re[i] = 2 * mr - Re[i];
            Im[i] = 2 * mi - Im[i];
        }
    }

    /// <summary>
    /// The probability of each value of <paramref name="register"/> (qubit indices, the first the most significant), and
    /// each value's amplitude sign when every amplitude is real: the sign it has where the other qubits hold their most
    /// likely value (so a helper qubit in |−⟩ does not hide phase kickback).
    /// </summary>
    public QuantumDistribution Distribution(int[] register) {
        int r = register.Length, restBits = Qubits - r;
        var probs = new double[1 << r];
        // Every circuit here measures its first qubits, so a register value is the top bits of the index; any other
        // register is read bit by bit.
        bool prefix = register.Select((q, i) => q == i).All(x => x);
        var others = Enumerable.Range(0, Qubits).Where(q => !register.Contains(q)).ToArray();
        long ReadBits(long i, int[] qubits) {
            long v = 0;
            foreach (int q in qubits) v = (v << 1) | ((i >> (Qubits - 1 - q)) & 1);
            return v;
        }
        long Reg(long i) => prefix ? i >> restBits : ReadBits(i, register);
        long Rest(long i) => prefix ? i & ((1L << restBits) - 1) : ReadBits(i, others);
        var restWeight = new Dictionary<long, double>();
        bool real = true;
        for (long i = 0; i < Re.Length; i++) {
            double p = Re[i] * Re[i] + Im[i] * Im[i];
            if (p < QuantumDistribution.Epsilon) continue;
            if (Math.Abs(Im[i]) > 1e-9) real = false;
            probs[Reg(i)] += p;
            long rest = Rest(i);
            restWeight[rest] = restWeight.GetValueOrDefault(rest) + p;
        }
        int[]? signs = null;
        if (real) {
            long best = restWeight.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).FirstOrDefault();
            signs = new int[1 << r];
            for (long i = 0; i < Re.Length; i++) {
                if (Re[i] * Re[i] < QuantumDistribution.Epsilon || Rest(i) != best) continue;
                signs[Reg(i)] = Re[i] >= 0 ? 1 : -1;
            }
        }
        return new QuantumDistribution(probs, signs);
    }
}

/// <summary>The outcome probabilities of a register, and each outcome's amplitude sign (null when amplitudes are complex).</summary>
sealed class QuantumDistribution {
    public const double Epsilon = 1e-12;
    public double[] Probabilities { get; }
    public int[]? Signs { get; }

    public QuantumDistribution(double[] probabilities, int[]? signs) {
        Probabilities = probabilities;
        Signs = signs;
    }

    /// <summary>The most likely outcome (the smallest one on a tie; see <see cref="Likeliest"/> for all of them).</summary>
    public long MostLikely {
        get {
            int best = 0;
            for (int i = 1; i < Probabilities.Length; i++)
                if (Probabilities[i] > Probabilities[best] + 1e-9) best = i;
            return best;
        }
    }

    /// <summary>Every outcome that shares the top probability, the smallest first (#907).</summary>
    public IReadOnlyList<long> Likeliest {
        get {
            double top = Probabilities[MostLikely];
            var tied = new List<long>();
            for (int i = 0; i < Probabilities.Length; i++)
                if (Probabilities[i] >= top - 1e-9) tied.Add(i);
            return tied;
        }
    }
}

/// <summary>
/// Runs a circuit column by column and keeps the outcome probabilities after each column it has reached. Only simulated
/// circuits (<see cref="QuantumCircuitSpec.Simulated"/>) are run; for the rest every answer is null.
/// </summary>
sealed class QuantumSimulation {
    private readonly QuantumCircuitSpec _spec;
    private QuantumState? _state;
    private int _applied;
    // Only the latest distribution is kept: a 16-qubit one is 65,536 probabilities, and steps ask in order.
    private int _lastApplied = -1;
    private QuantumDistribution? _last;

    public QuantumSimulation(QuantumCircuitSpec spec) {
        _spec = spec;
    }

    /// <summary>The outcome probabilities on the measured register once <paramref name="applied"/> columns have run, or null
    /// when the circuit is not simulated.</summary>
    public QuantumDistribution? After(int applied) {
        if (!_spec.Simulated || applied < 0 || applied > _spec.Columns.Count) return null;
        if (applied == _lastApplied) return _last;
        if (_state == null || applied < _applied) {
            _state = new QuantumState(_spec.Wires.Count);
            _applied = 0;
        }
        while (_applied < applied) {
            _spec.Columns[_applied].Apply?.Invoke(_state);
            _applied++;
        }
        _last = _state.Distribution(_spec.Measure);
        _lastApplied = applied;
        return _last;
    }

    /// <summary>The caption of the step that applies column <paramref name="column"/> (from 0).</summary>
    public string Caption(int column) => _spec.Columns[column].Caption(After(column + 1));
}
