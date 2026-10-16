using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_NFA;

namespace API.Problems.P.P_NFA.Solvers;

/// <summary>
/// Runs every branch of an NFA at once: the set of states it can be in after each input symbol, following ε moves. The
/// input is accepted when an accept state is in the set at the end. Polynomial, unlike <see cref="NFASolver"/>, which
/// follows one run at a time; its steps are the "all branches at once" view of the Automaton picture.
/// </summary>
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

    public string solve(NFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    // Steps, for the Automaton picture (ids from AutomatonFrames): a Try for the start (the start state and its ε-closure;
    // focus: the start state and the ε arrows followed), a Try per symbol read (the new set; focus: every arrow taken,
    // ε moves included), a Reject when no active state can read the next symbol, one outside the alphabet included (every
    // branch dies; the set is empty), then an Accept on the accept states in the final set or a Reject on the set, and Done.
    // Recorder state lives only in this call.
    public string Solve(NFA problem, StepRecorder<ActiveStates> rec) {
        string input = problem.inputString == "ε" ? "" : problem.inputString;

        // A symbol outside the alphabet is a move no state has, so every branch dies on it, as on any other missing move.
        // The answer is NFASolver's for such input: it names the symbol.
        char? offAlphabet = input.Any(c => !problem.alphabet.Contains(c)) ? input.First(c => !problem.alphabet.Contains(c)) : null;

        // How each (state, position) was first reached, so one run can be read back for each accept state.
        var parent = new Dictionary<(string State, int Pos), (string State, int Pos)>();
        var taken = new List<(string From, string To)>();

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

        string Names(IEnumerable<string> states) => AutomatonFrames.Braces(states);
        string[] Arrows() => taken.Select(t => AutomatonFrames.TransitionId(t.From, t.To)).Distinct().ToArray();

        var active = Closure([problem.startState], 0);
        {
            var extra = active.Skip(1).ToList();
            rec.Try(() => new ActiveStates(active, 0),
                () => extra.Count == 0 ? $"Start in {problem.startState}."
                    : $"Start in {problem.startState}, and follow ε to {AutomatonFrames.List(extra)}. The active states are {Names(active)}.",
                () => [AutomatonFrames.StateId(problem.startState), .. Arrows()]);
        }

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
            if (moved.Count == 0) {
                var stuck = active;
                bool known = problem.alphabet.Contains(symbol);
                rec.Reject(() => new ActiveStates([], i),
                    () => known ? $"Read '{symbol}': no active state has a '{symbol}' transition, so every branch dies."
                        : $"Read '{symbol}': '{symbol}' is not in the alphabet, so no active state has a move on it, and every branch dies.",
                    () => stuck.Select(AutomatonFrames.StateId).ToArray());
                rec.Done(new ActiveStates([], i), false, known ? $"Rejected: every branch died at symbol {pos}, '{symbol}'."
                    : $"Rejected: every branch died at symbol {pos}, '{symbol}', which is not in the alphabet.");
                return offAlphabet is char off ? $"No Solution: Input contains character '{off}' not in NFA alphabet"
                    : "No Solution Exists: No run accepts the input";
            }
            int symbolMoves = taken.Count;
            active = Closure(moved, pos);
            var now = active;
            bool viaEpsilon = taken.Count > symbolMoves;
            rec.Try(() => new ActiveStates(now, pos),
                () => $"Read '{symbol}'. The active states are now {Names(now)}{(viaEpsilon ? ", including ε moves" : "")}.",
                () => Arrows());
        }

        var accepting = active.Where(problem.acceptStates.Contains).ToList();
        if (accepting.Count == 0) {
            var end = active;
            rec.Reject(() => new ActiveStates(end, input.Length),
                () => $"The input is used up with {Names(end)} active, and none of them is an accept state.",
                () => end.Select(AutomatonFrames.StateId).ToArray());
            rec.Done(new ActiveStates(end, input.Length), false, "Rejected: no run accepts the input.");
            return "No Solution Exists: No run accepts the input";
        }

        rec.Accept(() => new ActiveStates(accepting, input.Length),
            () => $"The input is used up with {Names(active)} active; {AutomatonFrames.List(accepting)} {(accepting.Count == 1 ? "is an accept state" : "are accept states")}, so a run accepts.",
            () => accepting.Select(AutomatonFrames.StateId).ToArray());
        rec.Done(new ActiveStates(accepting, input.Length), true,
            accepting.Count == 1 ? "Accepted: a run ends in an accept state." : $"Accepted: runs end in {accepting.Count} accept states.");

        // One run per accept state reached, read back from how each (state, position) was first reached.
        var lines = accepting.Select(a => {
            var run = new List<string>();
            var at = (State: a, Pos: input.Length);
            run.Add(at.State);
            while (parent.TryGetValue(at, out var before)) {
                run.Add(before.State);
                at = before;
            }
            run.Reverse();
            return "The sequence of states to accept is: " + string.Join(", ", run);
        });
        return string.Join(Environment.NewLine, lines);
    }
}
