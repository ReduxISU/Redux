using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

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

    // The seed of the next solve (#932); explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    // --- Methods Including Constructors ---
    public PPZ() {

    }

    // A fixed number of tries, so tests can reach runs that restart without the computed count (#932).
    private readonly int? _trials;

    internal PPZ(int trials) {
        _trials = trials;
    }

    // One move of a try: the variable set (an index into the variables), its value, and the clause that forced it (0-based), or
    // -1 when the value was random.
    private readonly record struct Set(int Variable, bool Value, int Clause);

    // Steps (#932): each try starts with a Try that names the random order of the variables (nothing set yet; "Restart." for
    // every try after the first). Then one Try per variable, in that order: the partial holds the variables set so far (the
    // rest are left out, so a picture draws them as Background), the focus is the variable, and the clause that forces its
    // value when one does. PPZ builds its assignment variable by variable, so it is incremental. Once every variable is set the
    // try is checked: an Accept when every clause has a true literal (then the Done), else a Reject whose focus is the clauses
    // with no true literal. SatTries records the first try and the successful one in full and the tries in between as one step,
    // within the recorder's cap. A run that gives up never says the formula has no answer.
    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        if (n == 0) {
            Tries.NoVariables(rec);
            return "{}";
        }
        var clauses = formula.Clauses;

        double neededTrials = Math.Pow(2.0, n * (2.0 / 3.0));
        int trials = _trials ?? (neededTrials >= MaxTrials ? MaxTrials : (int)Math.Max(1, neededTrials));
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        var tries = new Tries(formula, rec.Enabled);
        var variables = Enumerable.Range(0, n).ToList();
        bool[]? solution = null;

        for (int t = 0; t < trials && solution == null; t++) {
            if (timerHasExpired) break;
            rec.CheckTime();
            List<int> order = variables.OrderBy(_ => rnd.Next()).ToList();
            var values = new bool?[n];
            tries.Begin(values);

            foreach (int v in order) {
                var (forced, clause) = ForcedValue(clauses, values, v);
                values[v] = forced ?? (rnd.Next(2) == 0);
                tries.Add(new Set(v, values[v]!.Value, clause));
            }

            bool[] full = Array.ConvertAll(values, x => x!.Value);
            bool solved = clauses.All(c => c.Length == 0 || SatFormula.Holds(c, full));
            if (solved) solution = full;
            tries.End(values, solved);
        }

        tries.Record(rec);
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

    // How PPZ's tries read: each move sets the next variable of the try's order, forced by a clause or at random.
    private sealed class Tries(SatFormula formula, bool recording) : SatTries<Set>(formula, recording) {
        protected override string SolverLabel => "PPZ";
        protected override string MoveNoun => "step";
        protected override string EachTry => "sets every variable";
        protected override bool ChecksAtEnd => true;

        protected override void Apply(Set s, bool?[] values) => values[s.Variable] = s.Value;

        protected override string[] MoveFocus(Set s) => s.Clause < 0
            ? [GroupedGraphFrames.VariableId(Formula.Variables[s.Variable])]
            : [GroupedGraphFrames.ClauseId(s.Clause), GroupedGraphFrames.VariableId(Formula.Variables[s.Variable])];

        // "Try 1: take the variables in a random order: x3, x1, x2." (the order is that of its moves)
        protected override string StartCaption(SatTry<Set> t, bool?[] start) {
            string intro = t.Number == 1 ? "Try 1: take the" : $"Restart. Try {t.Number}: take the";
            string random = t.Number == 1 ? "a random order" : "a new random order";
            return Formula.Variables.Count <= 8
                ? $"{intro} variables in {random}: {string.Join(", ", t.Moves.Select(m => Formula.Variables[m.Variable]))}."
                : $"{intro} {Formula.Variables.Count} variables in {random}.";
        }

        // "Step 2: set x1. Every other literal of C3 (x1 | !x2 | x3) is false, so C3 forces x1 = True."
        protected override string MoveCaption(SatTry<Set> t, Set s, bool?[] after, int number) {
            string name = Formula.Variables[s.Variable];
            return s.Clause < 0
                ? $"Step {number}: set {name}. No clause forces it, so it takes a random value: {name} = {s.Value}."
                : $"Step {number}: set {name}. Every other literal of {ClauseLabel(s.Clause)} is false, so C{s.Clause + 1} forces {name} = {s.Value}.";
        }

        protected override string AfterSkipped(bool?[] values) {
            int set = values.Count(v => v.HasValue);
            return $"{set} of the {Formula.Variables.Count} variables {(set == 1 ? "is" : "are")} set.";
        }

        protected override string FoundBy(SatTry<Set> t) => $"try {t.Number}";

        // Every variable is set: the check that ends the try.
        protected override void Close(StepRecorder<Assignment> rec, SatTry<Set> t, bool?[] end) {
            var falseClauses = FalseClauses(end);
            if (t.Solved)
                rec.Accept(() => ToAssignment(end), () => $"Try {t.Number} has set every variable: every clause has a true literal.");
            else
                rec.Reject(() => ToAssignment(end), () => $"Try {t.Number} has set every variable, but {Falsified(falseClauses, lower: true)}",
                    falseClauses.Select(GroupedGraphFrames.ClauseId).ToArray());
        }
    }
}
