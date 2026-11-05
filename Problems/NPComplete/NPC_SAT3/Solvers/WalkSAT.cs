using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

partial class WalkSAT : ISolver<SAT3, Assignment>, IRandomizedSolver {

    // --- Fields ---
    public string solverName { get; } = "WalkSAT Algorithm";
    public string solverDefinition { get; } = "Repeats random-restart local search up to 100 times."
 + " In each try, starts with a random truth assignment and performs up to 300n flips (n = number of variables)."
 + " Each step selects a randomly chosen unsatisfied clause. If any literal in the clause can be flipped"
 + " without breaking any satisfied clauses (break-count = 0), it flips it immediately."
 + " Otherwise, with probability p, a random literal in the clause is flipped; with probability 1-p,"
 + " the literal with the minimum break-count is flipped.";

    public string source { get; } = "Bart Selman, Henry A. Kautz, and Bram Cohen. 1994. Noise strategies for improving local search. In AAAI '94.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cdn.aaai.org/AAAI/1994/AAAI94-051.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Randomized local search: random choices make it Stochastic, and it has no
    // worst-case guarantee of finding an assignment within its tries, which makes it a Heuristic.
    public SolverType solverType { get; } = SolverType.Heuristic | SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("n * m", ", n = number of variables, m = number of clauses");
    // betweeen 0.5 and 0.6, empirically 0.57 proven best
    private readonly double _p = 0.57;
    private readonly int _maxTries = 100;
    // MaxFlips is typically scaled with n (e.g., 100 * n to 300 * n)
    private readonly int _flipsPerVariable = 300;

    // The seed of the next solve; explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    public WalkSAT() { }

    // Fewer tries or flips, so tests can reach runs that restart.
    internal WalkSAT(int maxTries, int flipsPerVariable) {
        _maxTries = maxTries;
        _flipsPerVariable = flipsPerVariable;
    }

    // How a flip chose its variable: a free flip (it turns no clause false), a random one, or the one that turns the fewest
    // clauses false.
    private enum FlipKind { Free, Random, Greedy }

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        if (n == 0) {
            Steps.NoVariables(rec);
            return "{}";
        }
        var clauses = formula.Clauses;

        int maxFlips = _flipsPerVariable * n;
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        var steps = new Steps(formula, rec.Enabled, maxFlips);
        bool[]? solution = null;

        for (int tryIdx = 0; tryIdx < _maxTries && solution == null; tryIdx++) {
            if (timerHasExpired) break; // stop at the time limit
            rec.CheckTime();   // every try, so a run whose first try succeeds is still stopped at the limit
            // Generate random assignment
            bool[] assignments = new bool[n];
            for (int v = 0; v < n; v++) assignments[v] = rnd.Next(2) == 0;
            steps.Begin(assignments);

            for (int flip = 0; flip <= maxFlips; flip++) {
                if (timerHasExpired) break; // stop at the time limit (the try ends here; see below)
                // Identify unsatisfied clauses
                var unsatisfied = new List<int>();
                for (int c = 0; c < clauses.Count; c++)
                    if (!SatFormula.Holds(clauses[c], assignments)) unsatisfied.Add(c);

                // If all clauses are satisfied, we found a solution
                if (unsatisfied.Count == 0) {
                    solution = assignments;
                    break;
                }
                if (flip == maxFlips) break;   // out of flips: the try failed
                rec.CheckTime();

                // Pick an unsatisfied clause uniformly at random
                int clause = unsatisfied[rnd.Next(unsatisfied.Count)];

                // Select variable to flip using standard WalkSAT heuristic
                var (varToFlip, kind, breaks) = PickWalkSATVariable(clauses[clause], assignments, clauses, rnd);

                // Flip assignment
                assignments[varToFlip] = !assignments[varToFlip];
                steps.Flipped(clause, varToFlip, kind, breaks);
            }

            steps.End(assignments, solution != null);
        }

        steps.Record(rec);
        // Stopped by the time limit, not out of tries: that is no verdict at all.
        if (solution == null && timerHasExpired) return SolveRun.TimedOutAnswer;
        return solution == null ? "{}" : formula.Certificate(solution); // "{}": failure to satisfy within caps
    }

    private (int Variable, FlipKind Kind, int Breaks) PickWalkSATVariable(
        (int Variable, bool Negated)[] clause,
        bool[] assignments,
        List<(int Variable, bool Negated)[]> clauses,
        Random rnd) {

        List<int> candidates = clause.Select(lit => lit.Variable).Distinct().ToList();

        // Compute break-count for each variable candidate in the clause
        var breakCounts = new List<int>(candidates.Count);
        List<int> zeroBreakVars = new List<int>();

        foreach (int var in candidates) {
            int breaks = CountBreaks(var, assignments, clauses);
            breakCounts.Add(breaks);
            if (breaks == 0) {
                zeroBreakVars.Add(var);
            }
        }

        if (zeroBreakVars.Count > 0) {
            return (zeroBreakVars[rnd.Next(zeroBreakVars.Count)], FlipKind.Free, 0);
        }

        if (rnd.NextDouble() < _p) {
            int pick = rnd.Next(candidates.Count);
            return (candidates[pick], FlipKind.Random, breakCounts[pick]);
        } else {
            int minBreaks = breakCounts.Min();
            List<int> bestVars = candidates.Where((_, i) => breakCounts[i] == minBreaks).ToList();
            return (bestVars[rnd.Next(bestVars.Count)], FlipKind.Greedy, minBreaks);
        }
    }

    private static int CountBreaks(int varToFlip, bool[] assignments, List<(int Variable, bool Negated)[]> clauses) {
        int breaks = 0;

        foreach (var clause in clauses) {
            // Fast check: is varToFlip even in this clause?
            bool containsVar = false;
            foreach (var lit in clause) if (lit.Variable == varToFlip) { containsVar = true; break; }
            if (!containsVar) continue;

            bool currentlySatisfied = false;
            bool satisfiedAfterFlip = false;

            foreach (var (variable, negated) in clause) {
                bool currentVal = assignments[variable];
                if (currentVal != negated) {
                    currentlySatisfied = true;
                }

                bool nextVal = variable == varToFlip ? !currentVal : currentVal;
                if (nextVal != negated) {
                    satisfiedAfterFlip = true;
                }
            }

            // A break occurs when a previously satisfied clause becomes unsatisfied
            if (currentlySatisfied && !satisfiedAfterFlip) {
                breaks++;
            }
        }

        return breaks;
    }
}
