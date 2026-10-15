namespace API.Interfaces.Schedule;

/// <summary>
/// Tank arithmetic for Pump Scheduling (both problems), shared by the solvers' dynamic programs and the verifiers so they
/// agree on every hour (#887, #738).
/// <para>
/// Every level-affecting input (the capacity, the starting level, the minimum, each hour's demand and each pump's flow) is
/// read as the decimal the instance wrote (<see cref="Exact"/>), so 0.1 + 0.2 is 0.3 and a level that lands exactly on a
/// limit is within it. The solvers count the tank in units of 10^-d gallons, the coarsest unit that makes every input
/// whole, so a level is a <c>long</c> and no hour rounds it: each reachable level is its own state, and a table only holds
/// the levels it reaches.
/// </para>
/// <para>
/// When no unit fits (an input has more than <see cref="MaxDecimals"/> decimals, or a level would be too big to count in
/// units), or the exact levels would give a table more states in an hour than its cap (<see cref="For"/>), the solver falls
/// back to whole gallons (<see cref="Rounded"/>): each hour's level is rounded to the nearest gallon, the way the table
/// always worked before #887, and the answer may then disagree with the verifier.
/// </para>
/// </summary>
sealed class TankLevels {
    /// <summary>The most decimals an input may have for the tank to be counted exactly.</summary>
    public const int MaxDecimals = 9;
    // The largest input, in units: a day's levels then add up (24 of them, each within the capacity) to below 2^53, so a
    // score summed in a double stays a whole number of units, and a level plus every pump's flow stays far inside a long.
    private const long MaxUnits = 100_000_000_000_000;

    /// <summary>True when the levels are whole gallons, rounded every hour; false when they are exact.</summary>
    public bool Rounded { get; }
    /// <summary>Units per gallon (10^d); 1 when <see cref="Rounded"/>.</summary>
    public long Scale { get; }
    /// <summary>The starting level, in units.</summary>
    public long Start { get; }

    private readonly int _hours;
    // Exact: the inputs in units.
    private readonly long _capacity, _minimum;
    private readonly long[] _demand = [], _maskFlow = [];
    // Rounded: the inputs in gallons, as doubles, and the top bucket (the capacity rounded up).
    private readonly double _capacityGal, _minimumGal;
    private readonly double[] _demandGal = [], _maskFlowGal = [];
    private readonly long _buckets;

    private TankLevels(double capacity, double level, double minimum, IReadOnlyList<double> demand, IReadOnlyList<double> flows, long? scale) {
        _hours = demand.Count;
        int masks = 1 << flows.Count;
        if (scale is long s) {
            Scale = s;
            _capacity = Units(capacity, s);
            _minimum = Units(minimum, s);
            Start = Units(level, s);
            _demand = demand.Select(d => Units(d, s)).ToArray();
            long[] flow = flows.Select(f => Units(f, s)).ToArray();
            _maskFlow = new long[masks];
            for (int mask = 0; mask < masks; mask++)
                for (int p = 0; p < flow.Length; p++)
                    if ((mask & (1 << p)) != 0)
                        _maskFlow[mask] += flow[p];
        } else {
            Rounded = true;
            Scale = 1;
            _capacityGal = capacity;
            _minimumGal = minimum;
            _demandGal = demand.ToArray();
            _buckets = (long)Math.Ceiling(capacity);
            _maskFlowGal = new double[masks];
            for (int mask = 0; mask < masks; mask++)
                for (int p = 0; p < flows.Count; p++)
                    if ((mask & (1 << p)) != 0)
                        _maskFlowGal[mask] += flows[p];
            Start = Bucket(level);
        }
    }

    /// <summary>
    /// The tank a solver's table counts in: exact when every input fits a unit and no hour reaches more than
    /// <paramref name="stateCap"/> (tank level, pumps on) states, in whole gallons otherwise. Deciding before the table is
    /// filled keeps its steps (one per hour) recorded once, as the table fills.
    /// </summary>
    public static TankLevels For(double capacity, double level, double minimum, IReadOnlyList<double> demand, IReadOnlyList<double> flows, int stateCap) {
        var tank = Create(capacity, level, minimum, demand, flows);
        return tank.Rounded || tank.Fits(stateCap) ? tank : Create(capacity, level, minimum, demand, flows, exact: false);
    }

    /// <summary>
    /// The tank of an instance, exact when every input fits a unit of at most <see cref="MaxDecimals"/> decimals (and
    /// <paramref name="exact"/> is true), in whole gallons otherwise. <paramref name="flows"/> are the pumps' flows in
    /// instance order; bit p of a mask is pump p.
    /// </summary>
    public static TankLevels Create(double capacity, double level, double minimum, IReadOnlyList<double> demand, IReadOnlyList<double> flows, bool exact = true) =>
        new(capacity, level, minimum, demand, flows, exact ? ScaleFor([capacity, level, minimum, .. demand, .. flows]) : null);

    /// <summary>
    /// The level after <paramref name="hour"/>, from <paramref name="level"/> with the pumps of <paramref name="mask"/> on;
    /// false when it leaves the limits (below the minimum or over the capacity). <paramref name="stored"/> is the water the
    /// tank holds then, for a score: in units when exact, in gallons before rounding when <see cref="Rounded"/>.
    /// </summary>
    public bool Next(long level, int hour, int mask, out long next, out double stored) {
        if (!Rounded) {
            next = level - _demand[hour] + _maskFlow[mask];
            stored = next;
            return next >= _minimum && next <= _capacity;
        }
        double after = level - _demandGal[hour] + _maskFlowGal[mask];
        stored = after;
        next = Bucket(after);
        return after >= _minimumGal && after <= _capacityGal;
    }

    /// <summary>
    /// Whether every hour reaches at most <paramref name="stateCap"/> (tank level, pumps on) states, counting every level
    /// some schedule reaches (whatever it costs) with a slot for each mask, as a table holds them. It stops as soon as an
    /// hour passes the cap, so it costs at most 24 times the cap.
    /// </summary>
    public bool Fits(int stateCap) {
        int masks = Rounded ? _maskFlowGal.Length : _maskFlow.Length;
        var levels = new HashSet<long> { Start };
        for (int h = 0; h < _hours; h++) {
            var next = new HashSet<long>();
            foreach (long level in levels)
                for (int mask = 0; mask < masks; mask++)
                    if (Next(level, h, mask, out long after, out _)) {
                        next.Add(after);
                        if ((long)next.Count * masks > stateCap) return false;
                    }
            levels = next;
        }
        return true;
    }

    /// <summary>A level, or a score, in gallons.</summary>
    public double Gallons(double units) => units / Scale;

    /// <summary>
    /// A number as the instance wrote it. The instance holds doubles; converting one to decimal keeps 15 significant digits,
    /// which gives back any number written with at most 15, so "0.1" is 0.1 again, not 0.1000000000000000055….
    /// </summary>
    public static decimal Exact(double value) => (decimal)value;

    private long Bucket(double level) => Math.Clamp((long)Math.Round(level), 0, _buckets);

    private static long Units(double value, long scale) => (long)(Exact(value) * scale);

    // The smallest power of ten that makes every value whole, within MaxUnits; null when there is none.
    private static long? ScaleFor(IEnumerable<double> values) {
        decimal[] exact;
        try {
            exact = values.Select(Exact).ToArray();
        } catch (OverflowException) {
            // Not a finite number a decimal can hold.
            return null;
        }
        long scale = 1;
        for (int d = 0; d <= MaxDecimals; d++, scale *= 10) {
            bool whole = true;
            foreach (var v in exact) {
                decimal units = v * scale;
                if (Math.Abs(units) > MaxUnits) return null;
                if (units != decimal.Truncate(units)) whole = false;
            }
            if (whole) return scale;
        }
        return null;
    }
}

/// <summary>
/// One hour of a Pump Scheduling table: the tank levels it reaches, each a row with one slot per pumps-on mask. Rows are
/// added as levels are first reached, so the table holds only reachable levels; <see cref="ByLevel"/> walks them from the
/// lowest, the order the old per-gallon table used, so ties break the same way.
/// </summary>
sealed class TankLayer<T> {
    private readonly int _masks;
    private readonly T _empty;
    private readonly Dictionary<long, int> _rows = new();
    private readonly List<T> _slots = new();

    public TankLayer(int masks, T empty) {
        _masks = masks;
        _empty = empty;
    }

    /// <summary>The level of each row, in the order first reached.</summary>
    public List<long> Levels { get; } = new();

    /// <summary>How many slots (rows times masks) the layer holds.</summary>
    public int Slots => _slots.Count;

    /// <summary>The row of a level, adding it (every slot empty) when the layer has not reached it yet.</summary>
    public int Row(long level) {
        if (_rows.TryGetValue(level, out int row)) return row;
        row = Levels.Count;
        _rows.Add(level, row);
        Levels.Add(level);
        for (int m = 0; m < _masks; m++) _slots.Add(_empty);
        return row;
    }

    public T this[int row, int mask] {
        get => _slots[row * _masks + mask];
        set => _slots[row * _masks + mask] = value;
    }

    /// <summary>The rows, lowest level first.</summary>
    public int[] ByLevel() {
        var rows = Enumerable.Range(0, Levels.Count).ToArray();
        Array.Sort(Levels.ToArray(), rows);
        return rows;
    }
}
