using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

class WalkSAT : ISolver<SAT3>
{

    // --- Fields ---
    public string solverName { get; } = "WalkSAT";
    public string solverDefinition { get; } = "Repeats the following trial up to a computed cap: picks a"
 + " uniformly random truth assignment to all variables, then performs up to 3n random walk steps,"
 + " where each step finds a clause that is not yet satisfied and selects one of its three literals to"
 + " flip using a noise parameter p: with probability p, the literal is chosen uniformly at random;"
 + " otherwise, the literal whose flip minimizes the number of currently satisfied clauses that become"
 + " unsatisfied (the break-count) is chosen, breaking ties randomly. If at any point all clauses are"
 + " satisfied, that assignment is returned as a solution. If no satisfying assignment is found after"
 + " all trials are exhausted, the algorithm reports failure without concluding the formula is"
 + " unsatisfiable.";
    public string source { get; } = "Bart Selman, Henry A. Kautz, and Bram Cohen. 1994. Noise strategies for improving local search. In Proceedings of the twelfth national conference on Artificial intelligence (vol. 1) (AAAI '94). American Association for Artificial Intelligence, USA, 337–343.";
    public string sourceLink { get; } =
        "https://cdn.aaai.org/AAAI/1994/AAAI94-051.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Randomized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O((4/3)^n), n = number of variables";

    private readonly double _p = 0.5;
    // --- Methods Including Constructors ---
    public WalkSAT()
    {

    }

    public string solve(SAT3 sat3)
    {
        HashSet<string> variables = new HashSet<string>(
            sat3.literals.Select(lit => lit.TrimStart('!'))
        );

        double neededTrials = Math.Pow(4.0 / 3.0, variables.Count);
        int trials;

        if (neededTrials >= int.MaxValue)
        {
            trials = int.MaxValue;
        }
        else
        {
            trials = (int)neededTrials;
        }

        for (int i = 0; i < trials; i++)
        {

            Random rnd = new Random();
            Dictionary<string, bool> assignments = new Dictionary<string, bool>();

            foreach (string literal in sat3.literals)
            {
                if (literal[0] == '!') assignments.TryAdd(literal.Substring(1), rnd.Next(2) == 0);
                else assignments.TryAdd(literal, rnd.Next(2) == 0);
            }

            // 3n attempts at solving
            for (int j = 0; j < 3 * assignments.Count; j++)
            {
                string sampleCertificate = string.Join(",", assignments.Select(kvp => kvp.Key + ":" + kvp.Value));
                if (sat3.defaultVerifier.verify(sat3, sampleCertificate))
                {
                    return "(" + sampleCertificate + ")";
                }

                // pick a random clause that is not satisfied
                List<List<string>> unsatisfiedClauses = new List<List<string>>();
                foreach (List<string> clause in sat3.clauses)
                {
                    bool satisfied = false;
                    foreach (string literal in clause)
                    {
                        string varName = literal.TrimStart('!');
                        bool isNegated = literal.StartsWith('!');
                        bool value = assignments[varName];
                        if ((isNegated && !value) || (!isNegated && value))
                        {
                            satisfied = true;
                            break;
                        }
                    }
                    if (!satisfied)
                    {
                        unsatisfiedClauses.Add(clause);
                    }
                }

                if (unsatisfiedClauses.Count == 0)
                {
                    return "(" + sampleCertificate + ")";
                }

                List<string> randomClause = unsatisfiedClauses[rnd.Next(unsatisfiedClauses.Count)];

                string varToFlip;
                if (rnd.NextDouble() < _p)
                {
                    // Noise step: pick a random literal from the clause (Schöning-style)
                    string randomLiteral = randomClause[rnd.Next(randomClause.Count)];
                    varToFlip = randomLiteral.TrimStart('!');
                }
                else
                {
                    // Greedy step: pick the literal whose flip breaks the fewest satisfied clauses
                    varToFlip = PickMinBreakLiteral(randomClause, assignments, sat3.clauses, rnd);
                }

                assignments[varToFlip] = !assignments[varToFlip];
            }
        }
        return "{}"; // No satisfying assignment found after all trials
    }

    private string PickMinBreakLiteral(List<string> clause, Dictionary<string, bool> assignments,
        List<List<string>> clauses, Random rnd)
    {
        List<string> candidates = clause.Select(lit => lit.TrimStart('!')).Distinct().ToList();
        int bestBreak = int.MaxValue;
        List<string> bestVars = new List<string>();

        foreach (string var in candidates)
        {
            int breakCount = CountBreaks(var, assignments, clauses);
            if (breakCount < bestBreak)
            {
                bestBreak = breakCount;
                bestVars = new List<string> { var };
            }
            else if (breakCount == bestBreak)
            {
                bestVars.Add(var);
            }
        }

        return bestVars[rnd.Next(bestVars.Count)];
    }

    private int CountBreaks(string varToFlip, Dictionary<string, bool> assignments, List<List<string>> clauses)
    {
        bool flippedValue = !assignments[varToFlip];
        int breaks = 0;

        foreach (List<string> clause in clauses)
        {
            bool currentlySatisfied = false;
            bool satisfiedAfterFlip = false;

            foreach (string literal in clause)
            {
                string name = literal.TrimStart('!');
                bool isNegated = literal.StartsWith('!');
                bool value = (name == varToFlip) ? assignments[name] : assignments[name];
                if ((isNegated && !value) || (!isNegated && value)) currentlySatisfied = true;

                bool valueAfter = (name == varToFlip) ? flippedValue : assignments[name];
                if ((isNegated && !valueAfter) || (!isNegated && valueAfter)) satisfiedAfterFlip = true;
            }

            if (currentlySatisfied && !satisfiedAfterFlip) breaks++;
        }

        return breaks;
    }
}