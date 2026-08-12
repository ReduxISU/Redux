using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_NFA;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace API.Problems.P.P_NFA.Solvers;

class NFASolver : ISolver<NFA>
{
    public string solverName { get; } = "NFA Solver";
    public string solverDefinition { get; } = "This solver enumerates all accepting runs of a nondeterministic finite automaton (returns all successful state sequences).";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.StateTransition;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // This does NOT do the poly-time subset-construction/active-state-set simulation possible
    // for NFA acceptance; DFS enumerates every accepting run individually, backtracking
    // visitedPerPath rather than memoizing across branches. Along any single root-to-leaf
    // path, (state, position) pairs can't repeat, bounding depth by Q*(n+1); branching factor
    // is bounded by d, the max per-state out-degree for a given symbol/epsilon. Worst case:
    public string complexity { get; } = "O(d^(Q * n)), where d = max per-state out-degree, Q = state count, n = input length";

    public NFASolver() { }

    public string solve(NFA problem)
    {
        // Normalize empty-input representation "ε"
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        // Validate characters
        foreach (char c in input)
        {
            if (!problem.alphabet.Contains(c))
                return $"No Solution: Input contains character '{c}' not in NFA alphabet";
        }

        var edges = problem.edges; // List<NFAEdge>
        var acceptPaths = new List<List<string>>();

        // DFS exploring nondeterministic runs; visitedPerPath prevents infinite loops for epsilon cycles
        void DFS(string state, int pos, List<string> path, HashSet<(string, int)> visitedPerPath)
        {
            // If consumed all input and in accept state, record a copy of the path
            if (pos >= input.Length && problem.acceptStates.Contains(state))
            {
                acceptPaths.Add(new List<string>(path));
                // Do not return: still allow further epsilon transitions that may produce other accept runs
            }

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε'))
            {
                var key = (e.To, pos);
                if (visitedPerPath.Contains(key)) continue;
                visitedPerPath.Add(key);
                path.Add(e.To);
                DFS(e.To, pos, path, visitedPerPath);
                path.RemoveAt(path.Count - 1);
                visitedPerPath.Remove(key);
            }

            // Explore regular symbol transitions (advance position)
            if (pos < input.Length)
            {
                char need = input[pos];
                foreach (var e in edges.Where(x => x.From == state && x.Symbol == need))
                {
                    var key = (e.To, pos + 1);
                    if (visitedPerPath.Contains(key)) continue;
                    visitedPerPath.Add(key);
                    path.Add(e.To);
                    DFS(e.To, pos + 1, path, visitedPerPath);
                    path.RemoveAt(path.Count - 1);
                    visitedPerPath.Remove(key);
                }
            }
        }

        // Seed DFS with start state
        var startPath = new List<string> { problem.startState };
        var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };
        DFS(problem.startState, 0, startPath, startVisited);

        // Build output
        if (acceptPaths.Count == 0)
        {
            return "No Solution Exists: No run accepts the input";
        }

        var sb = new StringBuilder();
        foreach (var p in acceptPaths)
        {
            sb.AppendLine("The sequence of states to accept is: " + string.Join(", ", p));
        }

        return sb.ToString().TrimEnd();
    }

    // GetSteps: The default steps for an NFA are the states of its default run (first accepting
    // run, or first rejected run if none accept) — needed so that IVisualization's non-generic
    // `StepsVisualization` guard (which short-circuits to empty when GetSteps is empty) doesn't
    // skip table-style visualizations that recompute their own step data via GetTableSteps.
    public List<Object> GetSteps(NFA problem)
    {
        var runs = GetPathRuns(problem);
        return runs.Count > 0 ? runs[0].states.Cast<Object>().ToList() : new List<Object>();
    }

    // ----- Table Visualization Support ----- //

    public class NFAPathTransition
    {
        public string from { get; set; } = "";
        public string symbol { get; set; } = "";
        public string to { get; set; } = "";
    }

    public class NFAPathRun
    {
        public List<string> states { get; set; } = new();
        public List<NFAPathTransition> transitions { get; set; } = new();
        public bool accepted { get; set; }
    }

    // GetPathRuns: Re-runs the same nondeterministic DFS as `solve`, but instead of only keeping
    // the final accepted-paths strings, it records every explored run with full transition detail:
    // every accepting run (same detection condition as `solve`) plus every rejected "dead end"
    // (a branch where no further epsilon/symbol transition applies). Rejected leaves are only
    // discovered here; `solve` already visits them, it just never recorded them.
    public List<NFAPathRun> GetPathRuns(NFA problem)
    {
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        var edges = problem.edges;
        var runs = new List<NFAPathRun>();

        void DFS(string state, int pos, List<string> path, List<NFAPathTransition> transitions, HashSet<(string, int)> visitedPerPath)
        {
            bool isAcceptingHere = pos >= input.Length && problem.acceptStates.Contains(state);
            if (isAcceptingHere)
            {
                runs.Add(new NFAPathRun
                {
                    states = new List<string>(path),
                    transitions = new List<NFAPathTransition>(transitions),
                    accepted = true
                });
            }

            bool expanded = false;

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε'))
            {
                var key = (e.To, pos);
                if (visitedPerPath.Contains(key)) continue;
                visitedPerPath.Add(key);
                path.Add(e.To);
                transitions.Add(new NFAPathTransition { from = state, symbol = "ε", to = e.To });
                expanded = true;
                DFS(e.To, pos, path, transitions, visitedPerPath);
                transitions.RemoveAt(transitions.Count - 1);
                path.RemoveAt(path.Count - 1);
                visitedPerPath.Remove(key);
            }

            // Explore regular symbol transitions (advance position)
            if (pos < input.Length)
            {
                char need = input[pos];
                foreach (var e in edges.Where(x => x.From == state && x.Symbol == need))
                {
                    var key = (e.To, pos + 1);
                    if (visitedPerPath.Contains(key)) continue;
                    visitedPerPath.Add(key);
                    path.Add(e.To);
                    transitions.Add(new NFAPathTransition { from = state, symbol = need.ToString(), to = e.To });
                    expanded = true;
                    DFS(e.To, pos + 1, path, transitions, visitedPerPath);
                    transitions.RemoveAt(transitions.Count - 1);
                    path.RemoveAt(path.Count - 1);
                    visitedPerPath.Remove(key);
                }
            }

            if (!expanded && !isAcceptingHere)
            {
                runs.Add(new NFAPathRun
                {
                    states = new List<string>(path),
                    transitions = new List<NFAPathTransition>(transitions),
                    accepted = false
                });
            }
        }

        var startPath = new List<string> { problem.startState };
        var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };
        DFS(problem.startState, 0, startPath, new List<NFAPathTransition>(), startVisited);

        // Accepting runs first (in discovery order), rejected runs after, so the default/first
        // entry is the first accepting run, or the first rejected run if none accept.
        return runs.Where(r => r.accepted).Concat(runs.Where(r => !r.accepted)).ToList();
    }

    public class NFATableStepRow
    {
        public int step { get; set; }
        public string symbol { get; set; } = "-";
        public string fromState { get; set; } = "-";
        public string toState { get; set; } = "";
        public bool accepting { get; set; }
    }

    public class NFATableStep
    {
        public int pathIndex { get; set; }
        public int pathCount { get; set; }
        public bool accepted { get; set; }
        public List<NFATableStepRow> rows { get; set; } = new();
    }

    // GetTableSteps: One table per explored path/run (not a time-progression within a single run,
    // since NFA has no single run). Each table lists every transition in that run, in full, so it
    // can be shown at once in a scrollable table. Stepping through the existing step slider pages
    // between runs.
    public List<Object> GetTableSteps(NFA problem)
    {
        var runs = GetPathRuns(problem);
        var steps = new List<Object>();

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var rows = new List<NFATableStepRow>
            {
                new NFATableStepRow
                {
                    step = 0,
                    toState = run.states[0],
                    accepting = problem.acceptStates.Contains(run.states[0])
                }
            };

            for (int t = 0; t < run.transitions.Count; t++)
            {
                var tr = run.transitions[t];
                rows.Add(new NFATableStepRow
                {
                    step = t + 1,
                    symbol = tr.symbol,
                    fromState = tr.from,
                    toState = tr.to,
                    accepting = problem.acceptStates.Contains(tr.to)
                });
            }

            steps.Add(new NFATableStep
            {
                pathIndex = i,
                pathCount = runs.Count,
                accepted = run.accepted,
                rows = rows
            });
        }

        return steps;
    }
}
