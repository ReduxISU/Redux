using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

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
    // worst-case guarantee of finding an assignment within its tries, which makes it a Heuristic (#842).
    public SolverType solverType { get; } = SolverType.Heuristic | SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("(4/3)^n", ", n = number of variables");

    // Hard cap on trials so an unsatisfiable or large instance cannot run for ~2^31 trials.
    private const int MaxTrials = 1_000;
    // A try succeeds with probability about (3/4)^n / sqrt(n), so (4/3)^n tries alone often miss on small formulas (2 tries
    // for 3 variables). Trials() makes 30 times the expected number, and never fewer than MinTrials.
    private const int MinTrials = 100;
    private const double TrialsPerExpectedTry = 30;

    // The seed of the next solve (#932); explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    // --- Methods Including Constructors ---
    public Schoning() {

    }

    // Fixed tries and walk length, so tests can reach runs that restart and runs too long for the cap (#932).
    private readonly int? _trials;
    private readonly int _flipsPerVariable = 3;

    internal Schoning(int trials, int flipsPerVariable) {
        _trials = trials;
        _flipsPerVariable = flipsPerVariable;
    }

    // How many tries a run of n variables makes (#932): enough that a satisfiable formula is very rarely missed.
    internal static int Trials(int n) {
        double needed = Math.Ceiling(TrialsPerExpectedTry * Math.Sqrt(n) * Math.Pow(4.0 / 3.0, n));
        return (int)Math.Clamp(needed, MinTrials, MaxTrials);
    }

    // One step of a walk: the clause it picked (0-based, no true literal) and the variable of the literal it flips.
    private readonly record struct Step(int Clause, int Variable);

    // Steps (#932), like WalkSAT's: each try starts with a Try from its random assignment ("Restart." for every try after the
    // first; an Accept when it already makes every clause true), then one step per walk step: the clause it picked and the
    // variable it flips are the focus, and the partial is the whole assignment after the flip, a candidate (not incremental). A
    // failed try ends with a Reject after 3n flips; the flip that makes every clause true is the Accept before the Done. SatTries
    // records the first try and the successful one in full and the tries in between as one step, within the recorder's cap. A
    // run that gives up never says the formula has no answer.
    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        if (n == 0) {
            Tries.NoVariables(rec);
            return "{}";
        }
        var clauses = formula.Clauses;

        int trials = _trials ?? Trials(n);
        int maxFlips = _flipsPerVariable * n;
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        var tries = new Tries(formula, rec.Enabled, maxFlips);
        bool[]? solution = null;

        for (int i = 0; i < trials && solution == null; i++) {
            if (timerHasExpired) break;

            // A uniformly random assignment.
            bool[] assignments = new bool[n];
            for (int v = 0; v < n; v++) assignments[v] = rnd.Next(2) == 0;
            tries.Begin(assignments);

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
                tries.Add(new Step(clause, literal.Variable));
            }

            tries.End(assignments, solution != null);
        }

        tries.Record(rec);
        return solution == null ? "{}" : formula.Certificate(solution);
    }

    // How Schöning's tries read: each move flips the variable of a random literal of a clause with no true literal.
    private sealed class Tries(SatFormula formula, bool recording, int maxFlips) : SatTries<Step>(formula, recording) {
        protected override string SolverLabel => "Schöning's algorithm";
        protected override string MoveNoun => "flip";
        protected override string EachTry => $"ends after {Count(maxFlips, "flip")}";

        protected override void Apply(Step s, bool?[] values) => values[s.Variable] = !values[s.Variable];

        protected override string[] MoveFocus(Step s) =>
            [GroupedGraphFrames.ClauseId(s.Clause), GroupedGraphFrames.VariableId(Formula.Variables[s.Variable])];

        // "Flip 3: C2 (x1 | !x2 | x3) has no true literal. Pick one of its literals at random and flip its variable: x2 to
        // False. Now C1 has no true literal."
        protected override string MoveCaption(SatTry<Step> t, Step s, bool?[] after, int number) {
            var still = FalseClauses(after);
            string now = still.Count == 0 ? "Every clause now has a true literal." : $"Now {Falsified(still, lower: true)}";
            return $"Flip {number}: {ClauseLabel(s.Clause)} has no true literal. Pick one of its literals at random and flip its variable: "
                + $"{Formula.Variables[s.Variable]} to {after[s.Variable]}. {now}";
        }
    }
}
