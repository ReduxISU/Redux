using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

class WalkSAT : ISolver<SAT3> {

    // --- Fields ---
    public string solverName { get; } = "WalkSAT Algorithm";
    public string solverDefinition { get; } = "Repeats random-restart local search up to MaxTries times."
 + " In each try, starts with a random truth assignment and performs up to MaxFlips steps."
 + " Each step selects a randomly chosen unsatisfied clause. If any literal in the clause can be flipped"
 + " without breaking any satisfied clauses (break-count = 0), it flips it immediately."
 + " Otherwise, with probability p, a random literal in the clause is flipped; with probability 1-p,"
 + " the literal with the minimum break-count is flipped.";

    public string source { get; } = "Bart Selman, Henry A. Kautz, and Bram Cohen. 1994. Noise strategies for improving local search. In AAAI '94.";
    public string sourceLink { get; } = "https://cdn.aaai.org/AAAI/1994/AAAI94-051.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(n * m), n = number of variables, m = number of clauses";
    // betweeen 0.5 and 0.6, empirically 0.57 proven best
    private readonly double _p = 0.57;
    private readonly int _maxTries = 100;

    public WalkSAT() { }

    public string solve(SAT3 sat3) {
        HashSet<string> variables = new HashSet<string>(
            sat3.literals.Select(lit => lit.TrimStart('!'))
        );

        int n = variables.Count;
        if (n == 0) return "{}";

        // MaxFlips is typically scaled with n (e.g., 100 * n to 300 * n)
        int maxFlips = 300 * n;
        Random rnd = new Random();

        for (int tryIdx = 0; tryIdx < _maxTries; tryIdx++) {
            // Generate random assignment
            Dictionary<string, bool> assignments = new Dictionary<string, bool>();
            foreach (string varName in variables) {
                assignments[varName] = rnd.Next(2) == 0;
            }

            for (int flip = 0; flip < maxFlips; flip++) {
                // Identify unsatisfied clauses
                List<List<string>> unsatisfiedClauses = new List<List<string>>();
                foreach (List<string> clause in sat3.clauses) {
                    if (!IsClauseSatisfied(clause, assignments)) {
                        unsatisfiedClauses.Add(clause);
                    }
                }

                // If all clauses are satisfied, we found a solution
                if (unsatisfiedClauses.Count == 0) {
                    string certificate = string.Join(",", assignments.Select(kvp => kvp.Key + ":" + kvp.Value));
                    return "(" + certificate + ")";
                }

                // Pick an unsatisfied clause uniformly at random
                List<string> randomClause = unsatisfiedClauses[rnd.Next(unsatisfiedClauses.Count)];

                // Select variable to flip using standard WalkSAT heuristic
                string varToFlip = PickWalkSATVariable(randomClause, assignments, sat3.clauses, rnd);

                // Flip assignment
                assignments[varToFlip] = !assignments[varToFlip];
            }
        }

        return "{}"; // Failure to satisfy within caps
    }

    private bool IsClauseSatisfied(List<string> clause, Dictionary<string, bool> assignments) {
        foreach (string literal in clause) {
            string varName = literal.TrimStart('!');
            bool isNegated = literal.StartsWith('!');
            bool value = assignments[varName];
            if ((isNegated && !value) || (!isNegated && value)) {
                return true;
            }
        }
        return false;
    }

    private string PickWalkSATVariable(
        List<string> clause,
        Dictionary<string, bool> assignments,
        List<List<string>> clauses,
        Random rnd) {

        List<string> candidates = clause.Select(lit => lit.TrimStart('!')).Distinct().ToList();

        // Compute break-count for each variable candidate in the clause
        Dictionary<string, int> breakCounts = new Dictionary<string, int>();
        List<string> zeroBreakVars = new List<string>();

        foreach (string var in candidates) {
            int breaks = CountBreaks(var, assignments, clauses);
            breakCounts[var] = breaks;
            if (breaks == 0) {
                zeroBreakVars.Add(var);
            }
        }

        if (zeroBreakVars.Count > 0) {
            return zeroBreakVars[rnd.Next(zeroBreakVars.Count)];
        }

        if (rnd.NextDouble() < _p) {
            return candidates[rnd.Next(candidates.Count)];
        } else {
            int minBreaks = breakCounts.Values.Min();
            List<string> bestVars = breakCounts.Where(kvp => kvp.Value == minBreaks)
                                               .Select(kvp => kvp.Key)
                                               .ToList();
            return bestVars[rnd.Next(bestVars.Count)];
        }
    }

    private int CountBreaks(string varToFlip, Dictionary<string, bool> assignments, List<List<string>> clauses) {
        int breaks = 0;

        foreach (List<string> clause in clauses) {
            // Fast check: is varToFlip even in this clause?
            bool containsVar = clause.Any(lit => lit.TrimStart('!') == varToFlip);
            if (!containsVar) continue;

            bool currentlySatisfied = false;
            bool satisfiedAfterFlip = false;

            foreach (string literal in clause) {
                string name = literal.TrimStart('!');
                bool isNegated = literal.StartsWith('!');

                bool currentVal = assignments[name];
                if ((isNegated && !currentVal) || (!isNegated && currentVal)) {
                    currentlySatisfied = true;
                }

                bool nextVal = (name == varToFlip) ? !currentVal : currentVal;
                if ((isNegated && !nextVal) || (!isNegated && nextVal)) {
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