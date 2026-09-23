using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

class PPZ : ISolver<SAT3> {
    // --- Fields ---
    public string solverName { get; } = "PPZ Algorithm";
    public string solverDefinition { get; } = "Repeats the following trial up to a computed cap: picks a"
    + " uniformly random permutation of the variables, then assigns them one at a time in that order."
    + " Before assigning a variable, checks whether some clause has become a 'unit clause' under the"
    + " current partial assignment (all other literals in it already falsified); if so, the variable is"
    + " forced to satisfy that clause. Otherwise the variable is assigned True or False uniformly at"
    + " random. If the resulting full assignment satisfies all clauses, it is returned as a solution."
    + " If no satisfying assignment is found after all trials are exhausted, the algorithm reports"
    + " failure without concluding the formula is unsatisfiable.";
    public string source { get; } = "R. Paturi, P. Pudlak and F. Zane, \"Satisfiability Coding Lemma,\" Proceedings 38th Annual Symposium on Foundations of Computer Science, Miami Beach, FL, USA, 1997, pp. 566-574, doi: 10.1109/SFCS.1997.646146.";
    public string sourceLink { get; } =
        "https://ieeexplore.ieee.org/document/646146";
    public string[] contributors { get; } = { };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(2^(2n/3)), n = number of variables";

    // --- Methods Including Constructors ---
    public PPZ() {

    }

    public string solve(SAT3 sat3) {
        List<string> variables = new HashSet<string>(
            sat3.literals.Select(lit => lit.TrimStart('!'))
        ).ToList();

        int n = variables.Count;
        double neededTrials = Math.Pow(2.0, n * (2.0 / 3.0));
        int trials = neededTrials >= int.MaxValue ? int.MaxValue : (int)Math.Max(1, neededTrials);

        Random rnd = new Random();

        for (int t = 0; t < trials; t++) {
            List<string> order = variables.OrderBy(_ => rnd.Next()).ToList();
            Dictionary<string, bool> assignments = new Dictionary<string, bool>();

            foreach (string var in order) {
                bool? forced = getForcedValue(sat3.clauses, assignments, var);
                assignments[var] = forced ?? (rnd.Next(2) == 0);
            }

            string sampleCertificate = string.Join(",", assignments.Select(kvp => kvp.Key + ":" + kvp.Value));
            if (sat3.defaultVerifier.verify(sat3, sampleCertificate)) {
                return "(" + sampleCertificate + ")";
            }
        }
        return "{}";
    }

    // Returns the value 'var' must take to satisfy a clause that has become a unit clause
    // under the current partial assignment, or null if no clause forces it.
    private static bool? getForcedValue(List<List<string>> clauses, Dictionary<string, bool> assignments, string var) {
        foreach (List<string> clause in clauses) {
            string? matchingLiteral = null;
            bool otherLiteralsFalsified = true;

            foreach (string literal in clause) {
                string varName = literal.TrimStart('!');
                bool isNegated = literal.StartsWith('!');

                if (varName == var) {
                    matchingLiteral = literal;
                    continue;
                }

                if (!assignments.TryGetValue(varName, out bool value)) {
                    otherLiteralsFalsified = false;
                    break;
                }

                bool literalValue = isNegated ? !value : value;
                if (literalValue) {
                    otherLiteralsFalsified = false;
                    break;
                }
            }

            if (matchingLiteral != null && otherLiteralsFalsified) {
                bool negated = matchingLiteral.StartsWith('!');
                return !negated;
            }
        }
        return null;
    }
}