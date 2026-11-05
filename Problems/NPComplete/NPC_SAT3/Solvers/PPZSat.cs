using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

partial class PPZ : ISolver<SAT3, Assignment>, IRandomizedSolver, IIncrementalSolver {
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

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        if (n == 0) {
            Steps.NoVariables(rec);
            return "{}";
        }
        var clauses = formula.Clauses;

        double neededTrials = Math.Pow(2.0, n * (2.0 / 3.0));
        int trials = _trials ?? (neededTrials >= MaxTrials ? MaxTrials : (int)Math.Max(1, neededTrials));
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        var steps = new Steps(formula, rec.Enabled);
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
                steps.SetVariable(v, values[v]!.Value, clause);
            }

            bool[] full = Array.ConvertAll(values, x => x!.Value);
            bool solved = clauses.All(c => c.Length == 0 || SatFormula.Holds(c, full));
            if (solved) solution = full;
            steps.End(values, solved);
        }

        steps.Record(rec);
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
