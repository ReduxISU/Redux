using API.Interfaces;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;
using static API.Interfaces.Steps.Words;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps PPZ records. Each try starts with a Try that names the random order of the variables (nothing set yet;
// "Restart." for every try after the first). Then one Try per variable, in that order: the partial holds the variables set
// so far (the rest are left out, so a picture draws them as Background), the focus is the variable, and the clause that
// forces its value when one does. PPZ builds its assignment variable by variable, so it is incremental. Once every variable
// is set the try is checked: an Accept when every clause has a true literal (then the Done), else a Reject whose focus is
// the clauses with no true literal. RestartSteps records the first try and the successful one in full and the tries in
// between as one step, within the recorder's cap. A run that gives up never says the formula has no answer.
class PPZ : ISolver<SAT3, Assignment>, IRandomizedSolver, IIncrementalSolver {
    // --- Fields ---
    public string solverName { get; } = "PPZ Algorithm";
    public string solverDefinition { get; } = "Repeats the following trial up to a computed cap (at most 10,000 trials): picks a"
    + " uniformly random permutation of the variables, then assigns them one at a time in that order."
    + " Before assigning a variable, checks whether some clause has become a 'unit clause' under the"
    + " current partial assignment (all other literals in it already falsified); if so, the variable is"
    + " forced to satisfy that clause. Otherwise the variable is assigned True or False uniformly at"
    + " random. If the resulting full assignment satisfies all clauses, it is returned as a solution."
    + " If no satisfying assignment is found after all trials are exhausted, the algorithm reports"
    + " failure without concluding the formula is unsatisfiable.";
    public string source { get; } = "R. Paturi, P. Pudlak and F. Zane, \"Satisfiability Coding Lemma,\" Proceedings 38th Annual Symposium on Foundations of Computer Science, Miami Beach, FL, USA, 1997, pp. 566-574, doi: 10.1109/SFCS.1997.646146.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } =
        "https://ieeexplore.ieee.org/document/646146";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("2^(2n/3)", ", n = number of variables");

    // Hard cap on trials so an unsatisfiable or large instance cannot run for ~2^31 trials.
    private const int MaxTrials = 10_000;

    // The seed of the next solve; explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    // --- Methods Including Constructors ---
    public PPZ() {

    }

    // A fixed number of tries, so tests can reach runs that restart without the computed count.
    private readonly int? _trials;

    internal PPZ(int trials) {
        _trials = trials;
    }

    // One move of a try: the variable set (an index into the variables), its value, and the clause that forced it (0-based), or
    // -1 when the value was random.
    private readonly record struct Set(int Variable, bool Value, int Clause);

    // How the steps show a try. A run that is not recording gets RestartSteps.Off and makes none of these; RestartSteps calls
    // them only while recording, for the steps it records.
    private static RestartSteps<bool?[], Set, Assignment> NewSteps(SatFormula formula, StepRecorder<Assignment> rec) => !rec.Enabled ? RestartSteps<bool?[], Set, Assignment>.Off : new(rec,
        // What every picture shows: the variables set so far, and for a move the variable and the clause that forces it.
        copy: values => (bool?[])values.Clone(),
        apply: (s, values) => values[s.Variable] = s.Value,
        partial: values => ToAssignment(formula.Values(values)),
        moveFocus: s => s.Clause < 0
            ? [VariableId(formula.Variables[s.Variable])]
            : [ClauseId(s.Clause), VariableId(formula.Variables[s.Variable])],
        // What each step says. "Take the variables of try 1 in a random order: x3, x1, x2." (the order is that of its moves)
        startWhy: (t, start) => {
            string intro = t.Number == 1 ? "Take the" : "Restart: take the";
            string random = t.Number == 1 ? "a random order" : "a new random order";
            return formula.Variables.Count <= 8
                ? $"{intro} variables of try {t.Number} in {random}: {string.Join(", ", t.Moves.Select(m => formula.Variables[m.Variable]))}."
                : $"{intro} {formula.Variables.Count} variables of try {t.Number} in {random}.";
        },
        // "Set x1. Every other literal of C3 (x1 | !x2 | x3) is false, so C3 forces x1 = True."
        moveWhy: (t, s, after) => s.Clause < 0
            ? $"Set {formula.Variables[s.Variable]}. No clause forces it, so it takes a random value: {formula.Variables[s.Variable]} = {s.Value}."
            : $"Set {formula.Variables[s.Variable]}. Every other literal of C{s.Clause + 1} {ClauseText(formula.Written[s.Clause])} is false, so C{s.Clause + 1} forces {formula.Variables[s.Variable]} = {s.Value}.",
        skippedWhy: (t, from, to, after) => {
            int set = after.Count(v => v.HasValue);
            return $"{(from == to ? $"The next variable set in try {t.Number} is" : $"The next {to - from + 1} variables set in try {t.Number} are")} not shown one by one. After them, "
                + $"{set} of the {formula.Variables.Count} variables {(set == 1 ? "is" : "are")} set.";
        },
        // Every variable is set: the check that ends the try.
        closeWhy: (t, end) => {
            if (t.Solved) return $"Every variable is set in try {t.Number}: every clause has a true literal.";
            return $"Every variable is set in try {t.Number}, but {ClauseCheck(formula.FalseClauses(end))}.";
        },
        closeFocus: (t, end) => t.Solved ? [] : formula.FalseClauses(end).Select(ClauseId).ToArray(),
        betweenWhy: (from, to) => from == to
            ? $"No satisfying assignment in try {from} (it sets every variable)."
            : $"No satisfying assignment in tries {from}–{to} (each sets every variable).",
        solvedWhy: (t, end) => $"Every clause has a true literal under {Assigned(formula.Values(end))}: found by try {t.Number}.",
        gaveUpWhy: begun => $"Gave up after {Count(begun, "try", "tries")}: no answer found. PPZ can miss an answer, so the formula may still have one.",
        checksAtEnd: true);

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        var steps = NewSteps(formula, rec);
        if (n == 0) {
            steps.Done(new Assignment([]), false, $"The formula has no variables to set: no answer found.");
            return "{}";
        }
        var clauses = formula.Clauses;

        double neededTrials = Math.Pow(2.0, n * (2.0 / 3.0));
        int trials = _trials ?? (neededTrials >= MaxTrials ? MaxTrials : (int)Math.Max(1, neededTrials));
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        var variables = Enumerable.Range(0, n).ToList();
        bool[]? solution = null;

        for (int t = 0; t < trials && solution == null; t++) {
            if (timerHasExpired) break;
            rec.CheckTime();
            List<int> order = variables.OrderBy(_ => rnd.Next()).ToList();
            var values = new bool?[n];
            steps.Begin(values);

            foreach (int v in order) {
                var (forced, clause) = ForcedValue(clauses, values, v);
                values[v] = forced ?? (rnd.Next(2) == 0);
                steps.Add(new Set(v, values[v]!.Value, clause));
            }

            bool[] full = Array.ConvertAll(values, x => x!.Value);
            bool solved = clauses.All(c => c.Length == 0 || SatFormula.Holds(c, full));
            if (solved) solution = full;
            steps.End(values, solved);
        }

        steps.Record(new Assignment([]));
        // Stopped by the time limit, not out of tries: that is no verdict at all.
        if (solution == null && timerHasExpired) return SolveRun.TimedOutAnswer;
        return solution == null ? "{}" : formula.Certificate(solution);
    }

    // The value variable v must take to satisfy a clause that has become a unit clause under the current partial assignment
    // (every other literal set and false), with that clause; (null, -1) if no clause forces it. The first such clause wins.
    private static (bool? Value, int Clause) ForcedValue(List<(int Variable, bool Negated)[]> clauses, bool?[] values, int v) {
        for (int c = 0; c < clauses.Count; c++) {
            (int Variable, bool Negated)? matching = null;
            bool otherLiteralsFalsified = true;

            foreach (var literal in clauses[c]) {
                if (literal.Variable == v) {
                    matching = literal;
                    continue;
                }
                if (values[literal.Variable] is not bool value || value != literal.Negated) {
                    // Not set yet, or true: the clause does not force v.
                    otherLiteralsFalsified = false;
                    break;
                }
            }

            if (matching is { } m && otherLiteralsFalsified)
                return (!m.Negated, c);
        }
        return (null, -1);
    }
}
