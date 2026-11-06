using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_NFA;
using static API.Interfaces.Graphs.AutomatonFrames;

namespace API.Problems.P.P_NFA.Solvers;

/// <summary>
/// Runs every branch of an NFA at once: the set of states it can be in after each input symbol, following ε moves. The
/// input is accepted when an accept state is in the set at the end. Polynomial, unlike <see cref="NFASolver"/>, which
/// follows one run at a time; its steps are the "all branches at once" view of the Automaton picture.
/// </summary>
// The steps NFA Subset Simulation records, for the Automaton picture (ids from AutomatonFrames). Each shows the set of
// active states, how much of the input is read, and the states and arrows the step is about:
//   - an Update for the start: the start state and the states its ε-moves reach;
//   - an Update per symbol read: every arrow the active states take on it, then the ε-moves from there;
//   - a Reject when no active state can read the next symbol (one outside the alphabet included): every branch dies;
//   - at the end, an Accept on the accept states among the active ones (a run accepts), or a Reject on the active
//     states when none of them accepts;
//   - the Done, with whether a run accepts.
class NFASubsetSolver : ISolver<NFA, ActiveStates> {
    public string solverName { get; } = "NFA Subset Simulation";
    public string solverDefinition { get; } = "Tracks the set of states the NFA can be in after each input symbol, following ε-transitions, and accepts when an accept state is in the set once the input is used up. Every branch runs at once, so the work is polynomial. For each accept state reached it returns one accepting run.";
    public string source { get; } = "Rabin, M. O. and Scott, D. (1959). \"Finite automata and their decision problems.\" IBM Journal of Research and Development, 3(2), 114–125.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived.
    public SolverType solverType { get; } = SolverType.StateTransition;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Each of the n input symbols moves every active state along its transitions and then takes the ε-closure, each a
    // pass over at most Q states and E transitions.
    public MathematicalFunction timeComplexity { get; } = new("n * (Q + E)", ", where n = input length, Q = state count, E = transition count");

    public NFASubsetSolver() { }

    // How the steps show the simulation: the active states, how many symbols are read, and the states the step is about.
    // The arrows the step took are in `taken`.
    private static RunSteps<(IReadOnlyList<string> Active, int Read, IReadOnlyList<string> On), ActiveStates> NewSteps(
        StepRecorder<ActiveStates> rec, List<(string From, string To)> taken) => new(rec,
        // What every picture shows: the active states, and how far into the input the simulation is.
        partial: s => new ActiveStates(s.Active, s.Read),
        // The states the step is about, then every arrow it took.
        focus: s => [.. s.On.Select(StateId), .. taken.Select(t => TransitionId(t.From, t.To)).Distinct()]);

    public string solve(NFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    // Keeps the set of states the NFA can be in: start with the start state's ε-closure, then for each symbol move every
    // active state along its arrows for it and take the ε-closure of where they land. A run accepts when an accept state
    // is active once the input is used up.
    public string Solve(NFA problem, StepRecorder<ActiveStates> rec) {
        string input = problem.inputString == "ε" ? "" : problem.inputString;

        // A symbol outside the alphabet is a move no state has, so every branch dies on it, as on any other missing move.
        // The answer is NFASolver's for such input: it names the symbol.
        char? offAlphabet = input.Any(c => !problem.alphabet.Contains(c)) ? input.First(c => !problem.alphabet.Contains(c)) : null;

        // How each (state, position) was first reached, so one run can be read back for each accept state.
        var parent = new Dictionary<(string State, int Pos), (string State, int Pos)>();
        // The arrows the current step took (symbol and ε moves), for its focus.
        var taken = new List<(string From, string To)>();
        var steps = NewSteps(rec, taken);

        // The states reachable from `from` at position `pos` by ε moves alone (the states themselves included), in the
        // order they are reached.
        List<string> Closure(IEnumerable<string> from, int pos) {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            foreach (var s in from)
                if (seen.Add(s)) { result.Add(s); queue.Enqueue(s); }
            while (queue.Count > 0) {
                string s = queue.Dequeue();
                foreach (var e in problem.edges.Where(x => x.From == s && x.Symbol == 'ε')) {
                    if (!seen.Add(e.To)) continue;
                    parent[(e.To, pos)] = (s, pos);
                    taken.Add((s, e.To));
                    result.Add(e.To);
                    queue.Enqueue(e.To);
                }
            }
            return result;
        }

        var active = Closure([problem.startState], 0);
        if (active.Count == 1)
            steps.Update((active, 0, [problem.startState]), $"Start in {problem.startState}.");
        else
            steps.Update((active, 0, [problem.startState]), $"Start in {problem.startState}, and follow ε to {List(active.Skip(1).ToList())}. The active states are {Braces(active)}.");

        for (int i = 0; i < input.Length; i++) {
            char symbol = input[i];
            int pos = i + 1;
            taken.Clear();
            var moved = new List<string>();
            foreach (var s in active)
                foreach (var e in problem.edges.Where(x => x.From == s && x.Symbol == symbol)) {
                    taken.Add((s, e.To));
                    if (!parent.ContainsKey((e.To, pos)) && !moved.Contains(e.To)) {
                        parent[(e.To, pos)] = (s, pos - 1);
                        moved.Add(e.To);
                    }
                }
            // No arrow on the symbol from any active state (so none was taken): every branch dies.
            if (moved.Count == 0) {
                if (problem.alphabet.Contains(symbol)) {
                    steps.Reject(([], i, active), $"Read '{symbol}': no active state has a '{symbol}' transition, so every branch dies.");
                    steps.Done(new ActiveStates([], i), false, $"Rejected: every branch died at symbol {pos}, '{symbol}'.");
                } else {
                    steps.Reject(([], i, active), $"Read '{symbol}': '{symbol}' is not in the alphabet, so no active state has a move on it, and every branch dies.");
                    steps.Done(new ActiveStates([], i), false, $"Rejected: every branch died at symbol {pos}, '{symbol}', which is not in the alphabet.");
                }
                return offAlphabet is char off ? $"No Solution: Input contains character '{off}' not in NFA alphabet"
                    : "No Solution Exists: No run accepts the input";
            }
            int symbolMoves = taken.Count;
            active = Closure(moved, pos);
            bool viaEpsilon = taken.Count > symbolMoves;
            steps.Update((active, pos, []), $"Read '{symbol}'. The active states are now {Braces(active)}{(viaEpsilon ? ", including ε moves" : "")}.");
        }

        // The end check takes no arrows.
        taken.Clear();
        var accepting = active.Where(problem.acceptStates.Contains).ToList();
        if (accepting.Count == 0) {
            steps.Reject((active, input.Length, active), $"The input is used up with {Braces(active)} active, and none of them is an accept state.");
            steps.Done(new ActiveStates(active, input.Length), false, $"Rejected: no run accepts the input.");
            return "No Solution Exists: No run accepts the input";
        }

        steps.Accept((accepting, input.Length, accepting), $"The input is used up with {Braces(active)} active; {List(accepting)} {(accepting.Count == 1 ? "is an accept state" : "are accept states")}, so a run accepts.");
        if (accepting.Count == 1)
            steps.Done(new ActiveStates(accepting, input.Length), true, $"Accepted: a run ends in an accept state.");
        else
            steps.Done(new ActiveStates(accepting, input.Length), true, $"Accepted: runs end in {accepting.Count} accept states.");

        // One run per accept state reached, read back from how each (state, position) was first reached. Each line is a
        // certificate NFAVerifier accepts ("1,2").
        var lines = accepting.Select(a => {
            var run = new List<string>();
            var at = (State: a, Pos: input.Length);
            run.Add(at.State);
            while (parent.TryGetValue(at, out var before)) {
                run.Add(before.State);
                at = before;
            }
            run.Reverse();
            return string.Join(",", run);
        });
        return string.Join(Environment.NewLine, lines);
    }
}
