using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_NFA;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace API.Problems.P.P_NFA.Solvers;

class NFASolver : ISolver<NFA, ActiveStates> {
    public string solverName { get; } = "NFA Backtracking";
    public string solverDefinition { get; } = "This solver enumerates all accepting runs of a nondeterministic finite automaton (returns all successful state sequences).";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
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

    public string solve(NFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    // Steps: Try for each move taken (an input symbol or an epsilon move), Accept when a run uses up the
    // input in an accept state, Reject at a dead end, Backtrack when the search backs out of a move, and
    // Done with the end states of all accepting runs. Recorder state lives only in this call.
    public string Solve(NFA problem, StepRecorder<ActiveStates> rec) {
        // Normalize empty-input representation "ε"
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        // Validate characters
        foreach (char c in input) {
            if (!problem.alphabet.Contains(c)) {
                rec.Reject(() => new ActiveStates([problem.startState], 0),
                    () => $"'{c}' is not in the alphabet, so the NFA cannot read it.", problem.startState);
                rec.Done(new ActiveStates([], 0), false, $"Rejected: '{c}' is not in the alphabet.");
                return $"No Solution: Input contains character '{c}' not in NFA alphabet";
            }
        }

        var edges = problem.edges; // List<NFAEdge>
        var acceptPaths = new List<List<string>>();

        // DFS exploring nondeterministic runs; visitedPerPath prevents infinite loops for epsilon cycles
        void DFS(string state, int pos, List<string> path, HashSet<(string, int)> visitedPerPath) {
            // If consumed all input and in accept state, record a copy of the path
            bool acceptingHere = pos >= input.Length && problem.acceptStates.Contains(state);
            bool moved = false;
            if (acceptingHere) {
                acceptPaths.Add(new List<string>(path));
                rec.Accept(() => new ActiveStates([state], pos),
                    () => $"The input is used up in {state}, an accept state: this run accepts.", state);
                // Do not return: still allow further epsilon transitions that may produce other accept runs
            }

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε')) {
                var key = (e.To, pos);
                if (visitedPerPath.Contains(key)) continue;
                visitedPerPath.Add(key);
                path.Add(e.To);
                moved = true;
                rec.Try(() => new ActiveStates([e.To], pos),
                    () => $"Take an ε-move from {state} to {e.To}.", state, e.To);
                DFS(e.To, pos, path, visitedPerPath);
                rec.Backtrack(() => new ActiveStates([state], pos),
                    () => $"Back out of {e.To} to {state} to try another move.", e.To, state);
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
                    moved = true;
                    rec.Try(() => new ActiveStates([e.To], pos + 1),
                        () => $"Read '{need}': move from {state} to {e.To}.", state, e.To);
                    DFS(e.To, pos + 1, path, visitedPerPath);
                    rec.Backtrack(() => new ActiveStates([state], pos),
                        () => $"Back out of {e.To} to {state} to try another move.", e.To, state);
                    path.RemoveAt(path.Count - 1);
                    visitedPerPath.Remove(key);
                }
            }

            if (!moved && !acceptingHere) {
                rec.Reject(() => new ActiveStates([state], pos),
                    () => pos < input.Length
                        ? $"{state} has no move on '{input[pos]}': this run is a dead end."
                        : $"The input is used up in {state}, which is not an accept state: this run is a dead end.",
                    state);
            }
        }

        // Seed DFS with start state
        var startPath = new List<string> { problem.startState };
        var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };
        rec.Try(() => new ActiveStates([problem.startState], 0),
            () => $"Start in {problem.startState}.", problem.startState);
        DFS(problem.startState, 0, startPath, startVisited);

        // Build output
        if (acceptPaths.Count == 0) {
            rec.Done(new ActiveStates([], input.Length), false, "Rejected: no run accepts the input.");
            return "No Solution Exists: No run accepts the input";
        }

        rec.Done(new ActiveStates(acceptPaths.Select(p => p[^1]), input.Length), true,
            acceptPaths.Count == 1 ? "Accepted: one run accepts the input." : $"Accepted: {acceptPaths.Count} runs accept the input.");

        var sb = new StringBuilder();
        foreach (var p in acceptPaths) {
            sb.AppendLine("The sequence of states to accept is: " + string.Join(", ", p));
        }

        return sb.ToString().TrimEnd();
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
