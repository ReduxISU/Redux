using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_NFA;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.P.P_NFA.Solvers;

class NFASolver : ISolver<NFA> {
    public string solverName { get; } = "NFA Backtracking";
    public string solverDefinition { get; } = "Searches the runs of a Nondeterministic Finite Automaton depth-first, backtracking at dead ends, and returns the first accepting run as its sequence of states, e.g. 1,2. Returns {} if no run accepts the input.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned backtracking search that stops at the first accepting run.
    public SolverType solverType { get; } = SolverType.StateTransition;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // This does NOT do the poly-time subset-construction/active-state-set simulation possible
    // for NFA acceptance; DFS tries runs one at a time, backtracking visitedPerPath rather than
    // memoizing across branches. It stops at the first accepting run, but a rejected input still
    // explores every run. Along any single root-to-leaf path, (state, position) pairs can't
    // repeat, bounding depth by Q*(n+1); branching factor is bounded by d, the max per-state
    // out-degree for a given symbol/epsilon. Worst case:
    public string complexity { get; } = "O(d^(Q * n)), where d = max per-state out-degree, Q = state count, n = input length";

    public NFASolver() { }

    public string solve(NFA problem) => solveDetailed(problem).certificate ?? "{}";

    public SolveResult solveDetailed(NFA problem) {
        // Normalize empty-input representation "ε"
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        // Validate characters
        foreach (char c in input) {
            if (!problem.alphabet.Contains(c))
                return SolveResult.NoSolution($"The input contains '{c}', which is not in the NFA's alphabet.");
        }

        var edges = problem.edges; // List<NFAEdge>

        // DFS exploring nondeterministic runs; visitedPerPath prevents infinite loops for epsilon cycles.
        // Returns true once `path` holds an accepting run, leaving it in place for the caller.
        bool DFS(string state, int pos, List<string> path, HashSet<(string, int)> visitedPerPath) {
            // Consumed all input and in an accept state: this run is the certificate
            if (pos >= input.Length && problem.acceptStates.Contains(state)) {
                return true;
            }

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε')) {
                var key = (e.To, pos);
                if (visitedPerPath.Contains(key)) continue;
                visitedPerPath.Add(key);
                path.Add(e.To);
                if (DFS(e.To, pos, path, visitedPerPath)) return true;
                path.RemoveAt(path.Count - 1);
                visitedPerPath.Remove(key);
            }

            // Explore regular symbol transitions (advance position)
            if (pos < input.Length) {
                char need = input[pos];
                foreach (var e in edges.Where(x => x.From == state && x.Symbol == need)) {
                    var key = (e.To, pos + 1);
                    if (visitedPerPath.Contains(key)) continue;
                    visitedPerPath.Add(key);
                    path.Add(e.To);
                    if (DFS(e.To, pos + 1, path, visitedPerPath)) return true;
                    path.RemoveAt(path.Count - 1);
                    visitedPerPath.Remove(key);
                }
            }

            return false;
        }

        // Seed DFS with start state
        var path = new List<string> { problem.startState };
        var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };

        // Same discovery order as GetPathRuns, so this is the run the visualization shows first
        return DFS(problem.startState, 0, path, startVisited)
            ? SolveResult.Solved(string.Join(",", path), $"The NFA accepts the input. This is the first accepting run found, ending in accept state {path[^1]}; others may exist.")
            : SolveResult.NoSolution("No run of the NFA accepts the input.");
    }

    // GetSteps: The default steps for an NFA are the states of its default run (first accepting
    // run, or first rejected run if none accept) — needed so that IVisualization's non-generic
    // `StepsVisualization` guard (which short-circuits to empty when GetSteps is empty) doesn't
    // skip table-style visualizations that recompute their own step data via GetTableSteps.
    public List<Object> GetSteps(NFA problem) {
        var runs = GetPathRuns(problem);
        return runs.Count > 0 ? runs[0].states.Cast<Object>().ToList() : new List<Object>();
    }

    // ----- Table Visualization Support ----- //

    public class NFAPathTransition {
        public string from { get; set; } = "";
        public string symbol { get; set; } = "";
        public string to { get; set; } = "";
    }

    public class NFAPathRun {
        public List<string> states { get; set; } = new();
        public List<NFAPathTransition> transitions { get; set; } = new();
        public bool accepted { get; set; }
    }

    // GetPathRuns: Re-runs the same nondeterministic DFS as `solve`, but instead of only keeping
    // the final accepted-paths strings, it records every explored run with full transition detail:
    // every accepting run (same detection condition as `solve`) plus every rejected "dead end"
    // (a branch where no further epsilon/symbol transition applies). Rejected leaves are only
    // discovered here; `solve` already visits them, it just never recorded them.
    public List<NFAPathRun> GetPathRuns(NFA problem) {
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        var edges = problem.edges;
        var runs = new List<NFAPathRun>();

        void DFS(string state, int pos, List<string> path, List<NFAPathTransition> transitions, HashSet<(string, int)> visitedPerPath) {
            bool isAcceptingHere = pos >= input.Length && problem.acceptStates.Contains(state);
            if (isAcceptingHere) {
                runs.Add(new NFAPathRun {
                    states = new List<string>(path),
                    transitions = new List<NFAPathTransition>(transitions),
                    accepted = true
                });
            }

            bool expanded = false;

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε')) {
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
            if (pos < input.Length) {
                char need = input[pos];
                foreach (var e in edges.Where(x => x.From == state && x.Symbol == need)) {
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

            if (!expanded && !isAcceptingHere) {
                runs.Add(new NFAPathRun {
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

    public class NFATableStepRow {
        public int step { get; set; }
        public string symbol { get; set; } = "-";
        public string fromState { get; set; } = "-";
        public string toState { get; set; } = "";
        public bool accepting { get; set; }
    }

    public class NFATableStep {
        public int pathIndex { get; set; }
        public int pathCount { get; set; }
        public bool accepted { get; set; }
        public List<NFATableStepRow> rows { get; set; } = new();
    }

    // GetTableSteps: One table per explored path/run (not a time-progression within a single run,
    // since NFA has no single run). Each table lists every transition in that run, in full, so it
    // can be shown at once in a scrollable table. Stepping through the existing step slider pages
    // between runs.
    public List<Object> GetTableSteps(NFA problem) {
        var runs = GetPathRuns(problem);
        var steps = new List<Object>();

        for (int i = 0; i < runs.Count; i++) {
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

            for (int t = 0; t < run.transitions.Count; t++) {
                var tr = run.transitions[t];
                rows.Add(new NFATableStepRow {
                    step = t + 1,
                    symbol = tr.symbol,
                    fromState = tr.from,
                    toState = tr.to,
                    accepting = problem.acceptStates.Contains(tr.to)
                });
            }

            steps.Add(new NFATableStep {
                pathIndex = i,
                pathCount = runs.Count,
                accepted = run.accepted,
                rows = rows
            });
        }

        return steps;
    }
}
