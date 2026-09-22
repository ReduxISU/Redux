using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

class Schoning : ISolver<SAT3>
{

    // --- Fields ---
    public string solverName { get; } = "Schöning's k-SAT Algorithm";
    public string solverDefinition { get; } = "Repeats the following trial up to a computed cap: picks a"
    + " uniformly random truth assignment to all variables, then performs up to 3n random walk steps,"
    + " where each step finds a clause that is not yet satisfied, picks one of its three literals uniformly"
    + " at random, and flips that variable's assigned value. If at any point all clauses are satisfied,"
    + " that assignment is returned as a solution. If no satisfying assignment is found after all trials"
    + " are exhausted, the algorithm reports failure without concluding the formula is unsatisfiable.";
    public string source { get; } = "T. Schoning, \"A probabilistic algorithm for k-SAT and constraint satisfaction problems,\" 40th Annual Symposium on Foundations of Computer Science (Cat. No.99CB37039), New York, NY, USA, 1999, pp. 410-414, doi: 10.1109/SFFCS.1999.814612.";
    public string sourceLink { get; } =
        "https://ieeexplore.ieee.org/stamp/stamp.jsp?tp=&arnumber=814612";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O((4/3)^n), n = number of variables";

    // --- Methods Including Constructors ---
    public Schoning()
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
                string randomLiteral = randomClause[rnd.Next(randomClause.Count)];
                string varToFlip = randomLiteral.TrimStart('!');
                assignments[varToFlip] = !assignments[varToFlip];
            }
        }
        return "{}";
    }
}