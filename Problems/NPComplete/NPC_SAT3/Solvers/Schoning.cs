using API.Interfaces;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;
using static API.Interfaces.Steps.Words;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps Schöning's algorithm records, like WalkSAT's. Each try starts with a Try from its random assignment ("Restart."
// for every try after the first; an Accept when it already makes every clause true). Then one Try per walk step: the clause
// it picked and the variable it flips are the focus, and the partial is the whole assignment after the flip, a candidate
// (not incremental). A failed try ends with a Reject after 3n flips; the flip that makes every clause true is the Accept
// before the Done. RestartSteps records the first try and the successful one in full and the tries in between as one step,
// within the recorder's cap. A run that gives up never says the formula has no answer.
class Schoning : ISolver<SAT3, Assignment>, IRandomizedSolver {

    // --- Fields ---
    public string solverName { get; } = "Schöning's k-SAT Algorithm";
    public string solverDefinition { get; } = "Repeats the following trial up to a computed cap (at most 1,000 trials): picks a"
    + " uniformly random truth assignment to all variables, then performs up to 3n random walk steps,"
    + " where each step finds a clause that is not yet satisfied, picks one of its three literals uniformly"
    + " at random, and flips that variable's assigned value. If at any point all clauses are satisfied,"
    + " that assignment is returned as a solution. If no satisfying assignment is found after all trials"
    + " are exhausted, the algorithm reports failure without concluding the formula is unsatisfiable.";
    public string source { get; } = "T. Schoning, \"A probabilistic algorithm for k-SAT and constraint satisfaction problems,\" 40th Annual Symposium on Foundations of Computer Science (Cat. No.99CB37039), New York, NY, USA, 1999, pp. 410-414, doi: 10.1109/SFFCS.1999.814612.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } =
        "https://ieeexplore.ieee.org/stamp/stamp.jsp?tp=&arnumber=814612";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Randomized local search: random choices make it Stochastic, and it has no
    // worst-case guarantee of finding an assignment within its tries, which makes it a Heuristic.
    public SolverType solverType { get; } = SolverType.Heuristic | SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("(4/3)^n", ", n = number of variables");

    // Hard cap on trials so an unsatisfiable or large instance cannot run for ~2^31 trials.
    private const int MaxTrials = 1_000;
    // A try succeeds with probability about (3/4)^n / sqrt(n), so (4/3)^n tries alone often miss on small formulas (2 tries
    // for 3 variables). Trials() makes 30 times the expected number, and never fewer than MinTrials.
    private const int MinTrials = 100;
    private const double TrialsPerExpectedTry = 30;

    // The seed of the next solve; explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    // --- Methods Including Constructors ---
    public Schoning() {

    }

    // Fixed tries and walk length, so tests can reach runs that restart and runs too long for the cap.
    private readonly int? _trials;
    private readonly int _flipsPerVariable = 3;

    internal Schoning(int trials, int flipsPerVariable) {
        _trials = trials;
        _flipsPerVariable = flipsPerVariable;
    }

    // One step of a walk: the clause it picked (0-based, no true literal) and the variable of the literal it flips.
    private readonly record struct Flip(int Clause, int Variable);

    // How the steps show a try. A run that is not recording gets RestartSteps.Off and makes none of these; RestartSteps calls
    // them only while recording, for the steps it records.
    private static RestartSteps<bool[], Flip, Assignment> NewSteps(SatFormula formula, int maxFlips, StepRecorder<Assignment> rec) => !rec.Enabled ? RestartSteps<bool[], Flip, Assignment>.Off : new(rec,
        // What every picture shows: the whole assignment, and for a flip the clause it picked and the variable it flips.
        copy: values => (bool[])values.Clone(),
        apply: (f, values) => values[f.Variable] = !values[f.Variable],
        partial: values => ToAssignment(formula.Values(values)),
        moveFocus: f => [ClauseId(f.Clause), VariableId(formula.Variables[f.Variable])],
        // What each step says. "Try 1: start from a random assignment, x1 = True, x2 = False. C2 has no true literal."
        startWhy: (t, start) => {
            string intro = t.Number == 1 ? "Try 1: start from a random assignment" : $"Restart. Try {t.Number}: start from a new random assignment";
            string listed = formula.Variables.Count <= 8 ? $", {Assigned(formula.Values(start))}" : $" of the {formula.Variables.Count} variables";
            var falseClauses = formula.FalseClauses(start);
            return falseClauses.Count == 0
                ? $"{intro}{listed}: every clause has a true literal."
                : $"{intro}{listed}. {NoTrueLiteral(falseClauses)}.";
        },
        // "Flip 3: C2 (x1 | !x2 | x3) has no true literal. Pick one of its literals at random and flip its variable: x2 to
        // False. Now C1 has no true literal."
        moveWhy: (t, f, after, number) => {
            var still = formula.FalseClauses(after);
            string now = still.Count == 0 ? "Every clause now has a true literal." : $"Now {NoTrueLiteral(still)}.";
            return $"Flip {number}: {FailedClause(f.Clause, formula.Written[f.Clause])}. Pick one of its literals at random and flip its variable: "
                + $"{formula.Variables[f.Variable]} to {after[f.Variable]}. {now}";
        },
        skippedWhy: (t, from, to, after) =>
            $"{(from == to ? $"Flip {from} of try {t.Number} is" : $"Flips {from}–{to} of try {t.Number} are")} not shown one by one. After them, "
                + $"{ClauseCheck(formula.FalseClauses(after))}.",
        closeWhy: (t, end) => $"Try {t.Number} ends after {Count(t.Moves.Count, "flip")}: {ClauseCheck(formula.FalseClauses(end))}.",
        betweenWhy: (from, to) => from == to
            ? $"Try {from} ends after {Count(maxFlips, "flip")}: no satisfying assignment."
            : $"Tries {from}–{to}: no satisfying assignment (each ends after {Count(maxFlips, "flip")}).",
        solvedWhy: (t, end) => $"Every clause has a true literal under {Assigned(formula.Values(end))}: found by "
            + (t.Moves.Count == 0 ? $"the random start of try {t.Number}." : $"try {t.Number} after {Count(t.Moves.Count, "flip")}."),
        gaveUpWhy: begun => $"Gave up after {Count(begun, "try", "tries")}: no answer found. Schöning's algorithm can miss an answer, so the formula may still have one.");

    // How many tries a run of n variables makes: enough that a satisfiable formula is very rarely missed.
    internal static int Trials(int n) {
        double needed = Math.Ceiling(TrialsPerExpectedTry * Math.Sqrt(n) * Math.Pow(4.0 / 3.0, n));
        return (int)Math.Clamp(needed, MinTrials, MaxTrials);
    }

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        int maxFlips = _flipsPerVariable * n;
        var steps = NewSteps(formula, maxFlips, rec);
        if (n == 0) {
            steps.Done(new Assignment([]), false, $"The formula has no variables to set: no answer found.");
            return "{}";
        }
        var clauses = formula.Clauses;

        int trials = _trials ?? Trials(n);
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        bool[]? solution = null;

        for (int i = 0; i < trials && solution == null; i++) {
            if (timerHasExpired) break;
            rec.CheckTime();   // every try, so a run whose first try succeeds is still stopped at the limit

            // A uniformly random assignment.
            bool[] assignments = new bool[n];
            for (int v = 0; v < n; v++) assignments[v] = rnd.Next(2) == 0;
            steps.Begin(assignments);

            // Up to 3n walk steps; the assignment after the last one is checked too.
            for (int j = 0; j <= maxFlips; j++) {
                var unsatisfied = new List<int>();
                for (int c = 0; c < clauses.Count; c++)
                    if (clauses[c].Length > 0 && !SatFormula.Holds(clauses[c], assignments)) unsatisfied.Add(c);
                if (unsatisfied.Count == 0) {
                    solution = assignments;
                    break;
                }
                if (j == maxFlips) break;   // out of steps: the try failed
                rec.CheckTime();

                // A random clause with no true literal, and one of its literals at random: flip that literal's variable.
                int clause = unsatisfied[rnd.Next(unsatisfied.Count)];
                var literal = clauses[clause][rnd.Next(clauses[clause].Length)];
                assignments[literal.Variable] = !assignments[literal.Variable];
                steps.Add(new Flip(clause, literal.Variable));
            }

            steps.End(assignments, solution != null);
        }

        steps.Record(new Assignment([]));
        // Stopped by the time limit, not out of tries: that is no verdict at all.
        if (solution == null && timerHasExpired) return SolveRun.TimedOutAnswer;
        return solution == null ? "{}" : formula.Certificate(solution);
    }
}
