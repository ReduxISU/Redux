using System.Globalization;
using System.Numerics;
using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPHard.NPH_ILP.Solvers;

class IntegerLinearProgrammingSolver : ISolver<INTEGERLINEARPROGRAMMING, BigInteger[]> {
    public string solverName => "Exact Integer Linear Programming Solver";
    public string solverDefinition => "Exact Fourier-Motzkin relaxation, integer branching, and objective binary search. Returns an optimal vector, infeasible, unbounded, or timeout. A work limit also returns timeout.";
    public string source => "Lawler, E. L., and Wood, D. E. (1966). Branch-and-Bound Methods: A Survey. Operations Research.";
    public string sourceLink { get; } = "https://doi.org/10.1287/opre.14.4.699";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Prashant Pant" };
    public SolverType solverType => SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public bool timerHasExpired { get; set; }
    public MathematicalFunction timeComplexity { get; } = new(
    "2^(2^n) * 2^n * log(U)",
    "n = number of integer variables, U = objective search range");
    // Elimination can grow exponentially. Never report a partial result as optimal.
    public int WorkLimit { get; set; } = 200_000;

    internal record Step(string Status, BigInteger[]? Assignment);
    private sealed class WorkExpiredException : Exception { }
    private sealed class Budget(IntegerLinearProgrammingSolver owner) {
        private int remaining = owner.WorkLimit;
        public void Check() {
            if (owner.timerHasExpired || remaining-- <= 0) throw new WorkExpiredException();
        }
    }

    public string solve(INTEGERLINEARPROGRAMMING problem) => Run(problem, null);
    public List<object> GetSteps(INTEGERLINEARPROGRAMMING problem) {
        var steps = new List<object>();
        Run(problem, steps);
        return steps;
    }

    public string Solve(INTEGERLINEARPROGRAMMING problem, StepRecorder<BigInteger[]> rec) => Run(problem, null, rec);

    private string Run(INTEGERLINEARPROGRAMMING problem, List<object>? steps, StepRecorder<BigInteger[]>? rec = null) {
        var budget = new Budget(this);
        steps?.Add(new Step("Instance", null));
        try {
            budget.Check();
            int n = problem.c.Count;
            var rows = problem.A.Select((row, i) => row.Append(problem.b[i]).ToArray()).ToList();
            var best = FindInteger(rows, n, budget);
            if (best is null) return Finish("infeasible", null);
            steps?.Add(new Step("Feasible assignment", best));
            rec?.Accept(() => (BigInteger[])best.Clone(), () => "Found a feasible integer assignment.");

            // Project z = c^T x onto z. An absent upper bound means the rational
            // program is unbounded. An integer feasible point and a rational
            // improving recession direction then make the ILP unbounded too.
            var projected = rows.Select(row => row.Take(n).Append(BigInteger.Zero).Append(row[n]).ToArray()).ToList();
            projected.Add(problem.c.Select(v => -v).Append(BigInteger.One).Append(BigInteger.Zero).ToArray());
            projected.Add(problem.c.Append(-BigInteger.One).Append(BigInteger.Zero).ToArray());
            for (int j = n - 1; j >= 0; j--) projected = Eliminate(projected, j, budget);
            BigInteger? upper = null;
            foreach (var row in projected) {
                if (row[0] <= 0) continue;
                var bound = new Rational(row[1], row[0]).Floor();
                upper = upper is null ? bound : BigInteger.Min(upper.Value, bound);
            }
            if (upper is null) return Finish("unbounded", null);

            BigInteger lower = Dot(problem.c, best);
            BigInteger high = upper.Value;
            while (lower < high) {
                budget.Check();
                BigInteger target = lower + (high - lower + 1) / 2;
                var query = new List<BigInteger[]>(rows) {
                    problem.c.Select(v => -v).Append(-target).ToArray()
                };
                var candidate = FindInteger(query, n, budget);
                if (candidate is null) high = target - 1;
                else {
                    best = candidate;
                    lower = Dot(problem.c, best);
                    steps?.Add(new Step("Improved feasible assignment", best));
                    rec?.Accept(() => (BigInteger[])best.Clone(), () => "Found a better feasible integer assignment.");
                }
            }
            return Finish(Format(best), best);
        } catch (WorkExpiredException) {
            return Finish("timeout", null);
        }

        string Finish(string result, BigInteger[]? assignment) {
            steps?.Add(new Step(assignment is null ? result : "Optimal assignment", assignment));
            rec?.Done(assignment is null ? Array.Empty<BigInteger>() : (BigInteger[])assignment.Clone(),
                assignment is not null, assignment is null ? result : "Found an optimal integer assignment.");
            return result;
        }
    }

    private static BigInteger[]? FindInteger(List<BigInteger[]> rows, int n, Budget budget) {
        // Integer rounding detects divisibility contradictions before branching:
        // e.g. 2x+2y<=1 becomes x+y<=0, not x+y<=1/2.
        rows = rows.Select(row => {
            BigInteger gcd = BigInteger.Zero;
            for (int j = 0; j < n; j++)
                gcd = BigInteger.GreatestCommonDivisor(gcd, BigInteger.Abs(row[j]));
            if (gcd <= 1) return row;
            var reduced = row.Select(v => v / gcd).ToArray();
            reduced[n] = new Rational(row[n], gcd).Floor();
            return reduced;
        }).ToList();
        var point = Relax(rows, n, budget);
        if (point is null) return null;
        if (point.All(v => v.IsInteger)) return point.Select(v => v.Numerator).ToArray();

        // Write [A,-A,I]y=b, y>=0, with x=x+ - x-. The small-solution
        // theorem bounds each y coordinate by N*(m*a)^(2m+1). Thus [-B,B]^n
        // contains an integer witness if one exists, making branching finite.
        int m = rows.Count;
        BigInteger a = BigInteger.One;
        foreach (var row in rows)
            foreach (var value in row) a = BigInteger.Max(a, BigInteger.Abs(value));
        BigInteger bound = (2 * n + m) * BigInteger.Pow(m * a, checked(2 * m + 1));
        var bounded = new List<BigInteger[]>(rows);
        for (int j = 0; j < n; j++) {
            bounded.Add(BoundRow(n, j, 1, bound));
            bounded.Add(BoundRow(n, j, -1, bound));
        }
        var pending = new Stack<List<BigInteger[]>>();
        pending.Push(bounded);
        while (pending.Count > 0) {
            budget.Check();
            var current = pending.Pop();
            point = Relax(current, n, budget);
            if (point is null) continue;
            int split = Array.FindIndex(point, v => !v.IsInteger);
            if (split < 0) return point.Select(v => v.Numerator).ToArray();
            pending.Push(new List<BigInteger[]>(current) {
                BoundRow(n, split, -1, -point[split].Ceiling())
            });
            pending.Push(new List<BigInteger[]>(current) {
                BoundRow(n, split, 1, point[split].Floor())
            });
        }
        return null;
    }

    private static BigInteger[] BoundRow(int n, int index, int sign, BigInteger rhs) {
        var row = new BigInteger[n + 1];
        row[index] = sign;
        row[n] = rhs;
        return row;
    }

    // Each row stores coefficients followed by its right-hand side.
    private static Rational[]? Relax(List<BigInteger[]> rows, int n, Budget budget) {
        var layers = new List<BigInteger[]>[n];
        var current = rows;
        for (int j = n - 1; j >= 0; j--) {
            layers[j] = current;
            current = Eliminate(current, j, budget);
        }
        if (current.Any(row => row[0] < 0)) return null;
        var point = new Rational[n];
        for (int j = 0; j < n; j++) {
            Rational? lower = null, upper = null;
            foreach (var row in layers[j]) {
                budget.Check();
                if (row[j].IsZero) continue;
                Rational rhs = new(row[j + 1]);
                for (int k = 0; k < j; k++) rhs -= new Rational(row[k]) * point[k];
                Rational limit = rhs / new Rational(row[j]);
                if (row[j] > 0) {
                    if (upper is null || limit < upper.Value) upper = limit;
                } else if (lower is null || limit > lower.Value) lower = limit;
            }
            if (lower is not null && upper is not null && lower.Value > upper.Value) return null;
            // Prefer an integer in the interval to reduce unnecessary branching.
            BigInteger candidate = BigInteger.Zero;
            if (lower is not null) candidate = BigInteger.Max(candidate, lower.Value.Ceiling());
            if (upper is not null) candidate = BigInteger.Min(candidate, upper.Value.Floor());
            point[j] = lower is not null && new Rational(candidate) < lower.Value
                ? lower.Value : new Rational(candidate);
        }
        return point;
    }

    private static List<BigInteger[]> Eliminate(List<BigInteger[]> rows, int index, Budget budget) {
        var positive = new List<BigInteger[]>();
        var negative = new List<BigInteger[]>();
        var result = new List<BigInteger[]>();
        var seen = new HashSet<string>();
        foreach (var row in rows) {
            budget.Check();
            if (row[index] > 0) positive.Add(row);
            else if (row[index] < 0) negative.Add(row);
            else Add(row.Where((_, j) => j != index).ToArray());
        }
        foreach (var p in positive) {
            foreach (var q in negative) {
                budget.Check();
                var merged = new BigInteger[p.Length - 1];
                for (int j = 0, k = 0; j < p.Length; j++) {
                    if (j == index) continue;
                    merged[k++] = -q[index] * p[j] + p[index] * q[j];
                }
                Add(merged);
            }
        }
        return result;

        void Add(BigInteger[] row) {
            BigInteger gcd = BigInteger.Zero;
            foreach (var value in row) gcd = BigInteger.GreatestCommonDivisor(gcd, BigInteger.Abs(value));
            if (gcd > 1)
                for (int j = 0; j < row.Length; j++) row[j] /= gcd;
            if (row.Take(row.Length - 1).All(v => v.IsZero) && row[^1] >= 0) return;
            if (seen.Add(string.Join(",", row))) result.Add(row);
        }
    }

    internal static BigInteger Dot(IReadOnlyList<BigInteger> a, IReadOnlyList<BigInteger> b) {
        BigInteger sum = BigInteger.Zero;
        for (int j = 0; j < a.Count; j++) sum += a[j] * b[j];
        return sum;
    }
    internal static string Format(IEnumerable<BigInteger> values) =>
        "(" + string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture))) + ")";

    private readonly struct Rational {
        public BigInteger Numerator { get; }
        private BigInteger Denominator { get; }
        public bool IsInteger => Denominator.IsOne;
        public Rational(BigInteger numerator) : this(numerator, BigInteger.One) { }
        public Rational(BigInteger numerator, BigInteger denominator) {
            if (denominator.IsZero) throw new DivideByZeroException();
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }
        public BigInteger Floor() => Numerator.Sign < 0
            ? -((-Numerator + Denominator - 1) / Denominator) : Numerator / Denominator;
        public BigInteger Ceiling() => -new Rational(-Numerator, Denominator).Floor();
        public static Rational operator -(Rational a, Rational b) => new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
        public static Rational operator *(Rational a, Rational b) => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        public static Rational operator /(Rational a, Rational b) => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        public static bool operator <(Rational a, Rational b) => a.Numerator * b.Denominator < b.Numerator * a.Denominator;
        public static bool operator >(Rational a, Rational b) => a.Numerator * b.Denominator > b.Numerator * a.Denominator;
    }
}
