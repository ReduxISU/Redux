using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_NFA;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static API.Interfaces.Graphs.AutomatonFrames;

namespace API.Problems.P.P_NFA.Solvers;

// The steps NFA Backtracking records, for the Automaton picture (ids from AutomatonFrames). The search follows one run
// at a time, depth first:
//   - an Update for the start state;
//   - an Update per move a run makes (an ε-move, or reading the next symbol): the run follows one arrow;
//   - an Accept when a run uses up the input in an accept state: the search keeps that run;
//   - a Reject when a run is a dead end: it has no move left and does not accept;
//   - a Backtrack each time the search backs out of a move to try the next one;
//   - the Done, with the end states of every accepting run.
// NFATableVisualization reads the runs back from these steps (RunsFromSteps): an Update is recorded as a Try, the first
// one is the start, and each later one is a move.
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
    public MathematicalFunction timeComplexity { get; } = new("d^(Q * n)", ", where d = max per-state out-degree, Q = state count, n = input length");

    public NFASolver() { }

    // How the steps show the search: the state the run is in, how many symbols it has read, and the arrow the step is
    // about (a move taken or backed out of; null when the step is about the state alone).
    private static RunSteps<(string In, int Read, (string From, string To)? Arrow), ActiveStates> NewSteps(StepRecorder<ActiveStates> rec) => new(rec,
        // What every picture shows: the state the run is in, and how far into the input it is.
        partial: s => new ActiveStates([s.In], s.Read),
        // The arrow the step is about; else the state the run is in.
        focus: s => s.Arrow is { } arrow ? [TransitionId(arrow.From, arrow.To)] : [StateId(s.In)]);

    public string solve(NFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    // A depth-first search over the NFA's runs: from each (state, position), follow every ε-move, and every move on the
    // next symbol, recursing on each and backing out to try the next. A run that uses up the input in an accept state is
    // kept. A (state, position) already on the current run is not entered again, so ε cycles end.
    public string Solve(NFA problem, StepRecorder<ActiveStates> rec) {
        SolveProbe.Hit(GetType());
        var steps = NewSteps(rec);
        // Normalize empty-input representation "ε"
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        // A symbol outside the alphabet is a move no state has: every branch that reaches it dies there, as at any other
        // missing move, so the search still runs (and shows those dead ends). The answer still names the symbol.
        char? offAlphabet = input.Any(c => !problem.alphabet.Contains(c)) ? input.First(c => !problem.alphabet.Contains(c)) : null;

        var edges = problem.edges; // List<NFAEdge>
        var acceptPaths = new List<List<string>>();

        // DFS exploring nondeterministic runs; visitedPerPath prevents infinite loops for epsilon cycles
        void DFS(string state, int pos, List<string> path, HashSet<(string, int)> visitedPerPath) {
            // If consumed all input and in accept state, record a copy of the path
            bool acceptingHere = pos >= input.Length && problem.acceptStates.Contains(state);
            bool moved = false;
            if (acceptingHere) {
                acceptPaths.Add(new List<string>(path));
                steps.Accept((state, pos, null), $"The input is used up in {state}, an accept state: this run accepts.");
                // Do not return: still allow further epsilon transitions that may produce other accept runs
            }

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε')) {
                var key = (e.To, pos);
                if (visitedPerPath.Contains(key)) continue;
                visitedPerPath.Add(key);
                path.Add(e.To);
                moved = true;
                steps.Update((e.To, pos, (state, e.To)), $"Take an ε-move from {state} to {e.To}.");
                DFS(e.To, pos, path, visitedPerPath);
                steps.Backtrack((state, pos, (state, e.To)), $"Back out of {e.To} to {state} to try another move.");
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
                    steps.Update((e.To, pos + 1, (state, e.To)), $"Read '{need}': move from {state} to {e.To}.");
                    DFS(e.To, pos + 1, path, visitedPerPath);
                    steps.Backtrack((state, pos, (state, e.To)), $"Back out of {e.To} to {state} to try another move.");
                    path.RemoveAt(path.Count - 1);
                    visitedPerPath.Remove(key);
                }
            }

            // A dead end: no move left, and the run does not accept here.
            if (!moved && !acceptingHere) {
                if (pos >= input.Length)
                    steps.Reject((state, pos, null), $"The input is used up in {state}, which is not an accept state: this run is a dead end.");
                else if (problem.alphabet.Contains(input[pos]))
                    steps.Reject((state, pos, null), $"{state} has no move on '{input[pos]}': this run is a dead end.");
                else
                    steps.Reject((state, pos, null), $"'{input[pos]}' is not in the alphabet, so {state} has no move on it: this run is a dead end.");
            }
        }

        // Seed DFS with start state
        var startPath = new List<string> { problem.startState };
        var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };
        steps.Update((problem.startState, 0, null), $"Start in {problem.startState}.");
        DFS(problem.startState, 0, startPath, startVisited);

        // Build output
        if (offAlphabet is char off) {
            steps.Done(new ActiveStates([], input.Length), false, $"Rejected: '{off}' is not in the alphabet, so no run can read the whole input.");
            return $"No Solution: Input contains character '{off}' not in NFA alphabet";
        }
        if (acceptPaths.Count == 0) {
            steps.Done(new ActiveStates([], input.Length), false, $"Rejected: no run accepts the input.");
            return "No Solution Exists: No run accepts the input";
        }

        // The Done shows the state every accepting run ends in.
        var ends = new ActiveStates(acceptPaths.Select(p => p[^1]), input.Length);
        if (acceptPaths.Count == 1)
            steps.Done(ends, true, $"Accepted: one run accepts the input.");
        else
            steps.Done(ends, true, $"Accepted: {acceptPaths.Count} runs accept the input.");

        // One accepting run per line, each a certificate NFAVerifier accepts ("1,2").
        var sb = new StringBuilder();
        foreach (var p in acceptPaths) {
            sb.AppendLine(string.Join(",", p));
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
        SolveProbe.Hit(GetType());
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
    // The run table now builds its tables from the typed run (TableStepsFrom); this second run is kept as the reference
    // the tests compare against.
    public List<Object> GetTableSteps(NFA problem) => TablesOf(problem, GetPathRuns(problem)).Cast<Object>().ToList();

    /// <summary>
    /// The runs the typed steps record, in the order <see cref="GetPathRuns"/> lists them (accepting runs first): the
    /// steps walk the same search, a Try for each move (the first is the start), a Backtrack for each move undone, an
    /// Accept or Reject where a run ends. An Accept counts only where the search is (and once there), so one kept out
    /// of order past the step cap is not taken for another run. Also how many steps the recorder did not keep: when
    /// that is more than zero, only the runs found within the kept steps are listed.
    /// </summary>
    internal static (List<NFAPathRun> Runs, int NotShown) RunsFromSteps(NFA problem, IEnumerable<object> steps) {
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;
        var runs = new List<NFAPathRun>();
        var states = new List<string>();
        var positions = new List<int>();
        var transitions = new List<NFAPathTransition>();
        int notShown = 0;
        SolverStep<ActiveStates>? previous = null;

        NFAPathRun Snapshot(bool accepted, string fallbackState) => new NFAPathRun {
            states = states.Count > 0 ? new List<string>(states) : new List<string> { fallbackState },
            transitions = new List<NFAPathTransition>(transitions),
            accepted = accepted
        };

        foreach (var step in steps.OfType<SolverStep<ActiveStates>>()) {
            string state = step.Partial.States.FirstOrDefault() ?? problem.startState;
            switch (step.Event) {
                case StepEvent.Try when states.Count == 0:
                    states.Add(state);
                    positions.Add(step.Partial.Position);
                    break;
                case StepEvent.Try:
                    int from = positions[^1];
                    int to = step.Partial.Position;
                    transitions.Add(new NFAPathTransition {
                        from = states[^1],
                        symbol = to > from && from < input.Length ? input[from].ToString() : "ε",
                        to = state
                    });
                    states.Add(state);
                    positions.Add(to);
                    break;
                case StepEvent.Backtrack when states.Count > 1:
                    states.RemoveAt(states.Count - 1);
                    positions.RemoveAt(positions.Count - 1);
                    transitions.RemoveAt(transitions.Count - 1);
                    break;
                case StepEvent.Accept when states.Count > 0 && state == states[^1] && step.Partial.Position == positions[^1]
                                           && !(previous is { Event: StepEvent.Accept } && previous.Partial.Equals(step.Partial)):
                    runs.Add(Snapshot(accepted: true, state));
                    break;
                case StepEvent.Reject:
                    runs.Add(Snapshot(accepted: false, state));
                    break;
                case StepEvent.Done:
                    notShown = StepCaptions.NotShown(step.Caption);
                    break;
            }
            previous = step;
        }
        return (runs.Where(r => r.accepted).Concat(runs.Where(r => !r.accepted)).ToList(), notShown);
    }

    /// <summary>
    /// Whether <paramref name="steps"/> can be this solver's search: every step before Done is at one state, and a run
    /// whose steps were all kept backs out of every move but the start. Another NFA solver's steps (the subset
    /// solver's, which hold sets of states and never back out) are not.
    /// </summary>
    internal static bool IsSearchTrace(IEnumerable<object> steps) {
        var typed = steps.OfType<SolverStep<ActiveStates>>().ToList();
        if (typed.Count == 0 || typed.Any(s => s.Event != StepEvent.Done && s.Partial.States.Length != 1))
            return false;
        var done = typed[^1];
        if (done.Event != StepEvent.Done || StepCaptions.NotShown(done.Caption) > 0 || StepCaptions.IsStopped(done.Caption))
            return true;
        int tries = typed.Count(s => s.Event == StepEvent.Try), backtracks = typed.Count(s => s.Event == StepEvent.Backtrack);
        return tries == 0 || tries == backtracks + 1;
    }

    /// <summary>One table per run, made from the typed steps (see <see cref="RunsFromSteps"/>), with no second run.</summary>
    internal static (List<NFATableStep> Tables, int NotShown) TableStepsFrom(NFA problem, IEnumerable<object> steps) {
        var (runs, notShown) = RunsFromSteps(problem, steps);
        return (TablesOf(problem, runs), notShown);
    }

    private static List<NFATableStep> TablesOf(NFA problem, List<NFAPathRun> runs) {
        var steps = new List<NFATableStep>();

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
