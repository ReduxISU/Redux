using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_DFA;

namespace API.Problems.P.P_DFA.Solvers;

class DFASolver : ISolver<DFA, ActiveStates> {

    // ----- Fields ----- //
    public string solverName { get; } = "DFA Simulation";
    public string solverDefinition { get; } = "This a solver for a Determiistic Finite Automata that returns no solution in none exists, or a solution consisting of the set states that led to an acceptance.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Analyzes the input string and transitions between DFA states
    // according to the edge relation, matching this SolverType value's own definition.
    public SolverType solverType { get; } = SolverType.StateTransition;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // For each of the up-to-n input characters, solve() does "foreach (var edge in problem.edges)"
    // — a linear scan of all E edges to find the matching transition — instead of an O(1)
    // dictionary/table lookup, so this is O(n * E), not the ideal O(n).
    public string complexity { get; } = "O(n * E)";

    // Methods Including Constructors //
    public DFASolver() { }

    public string solve(DFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    // Steps, for the Automaton picture (ids from AutomatonFrames): a Try for the start state (focus: the state), then a
    // Try per symbol read (partial: the state reached; focus: the arrow taken). A missing move is a Reject whose partial
    // has no state: the machine falls into the Garbage state (focus: its id). A symbol outside the alphabet is a missing
    // move too, and falls into Garbage the same way. Input used up in a state that does not accept is a Reject on that
    // state. Done closes the run with the state it ended in (none
    // for Garbage) and whether the DFA accepts. Recorder state lives only in this call, so the solver object stays stateless.
    public string Solve(DFA problem, StepRecorder<ActiveStates> rec) {
        // Input String (the empty input is written "ε", and reads no symbol) //
        string inputString = problem.inputString == "ε" ? "" : problem.inputString;
        // First Node To Be Analyzed //
        string currentNode = problem.startState;
        // Will Track Path Through Nodes //
        var nodePath = new List<string> { currentNode };
        int consumed = 0;

        rec.Try(() => new ActiveStates([problem.startState], 0),
            () => $"Start in {problem.startState}.", () => [AutomatonFrames.StateId(problem.startState)]);

        string Finish(string answer, bool ok, string caption, bool inGarbage = false) {
            string end = currentNode;
            int at = consumed;
            rec.Done(new ActiveStates(inGarbage ? [] : [end], at), ok, caption);
            return answer;
        }

        foreach (char character in inputString) {
            // Check If Character Is In Alphabet //
            // A symbol outside the alphabet is a missing move like any other: the DFA falls into the Garbage state.
            if (!problem.alphabet.Contains(character)) {
                rec.Reject(() => new ActiveStates([], consumed),
                    () => $"'{character}' is not in the alphabet, so {currentNode} has no move on it, and the DFA falls into the Garbage state and stays there.",
                    () => [AutomatonFrames.GarbageId]);
                return Finish($"No Solution: Input contains character '{character}' not in DFA alphabet", false,
                    $"Rejected: '{character}' is not in the alphabet, so {currentNode} has no move on it and the run ends in the Garbage state.",
                    inGarbage: true);
            }

            // Follow the Edge //
            bool foundEdge = false;
            foreach (var edge in problem.edges) {
                if (edge.From == currentNode && edge.Symbol == character) {
                    currentNode = edge.To;
                    nodePath.Add(currentNode);
                    consumed++;
                    foundEdge = true;
                    rec.Try(() => new ActiveStates([edge.To], consumed),
                        () => $"Read '{character}': move from {edge.From} to {edge.To}.", () => [AutomatonFrames.TransitionId(edge.From, edge.To)]);
                    break;
                }
            }

            // If No Edge, DFA Stops: It Falls Into the Garbage State //
            if (!foundEdge) {
                rec.Reject(() => new ActiveStates([], consumed),
                    () => $"{currentNode} has no move on '{character}', so the DFA falls into the Garbage state and stays there.",
                    () => [AutomatonFrames.GarbageId]);
                return Finish("No Solution Exists: DFA cannot transition with this character", false,
                    $"Rejected: {currentNode} has no move on '{character}', so the run ends in the Garbage state.", inGarbage: true);
            }
        }

        // Check If Last State Is Accept State //
        if (problem.acceptStates.Contains(currentNode)) {
            return Finish("The sequence of states to accept is: " + string.Join(", ", nodePath), true,
                $"The input is used up in {currentNode}, an accept state, so the DFA accepts.");
        } else {
            rec.Reject(() => new ActiveStates([currentNode], consumed),
                () => $"The input is used up in {currentNode}, which is not an accept state.", () => [AutomatonFrames.StateId(currentNode)]);
            return Finish("No Solution Exists: The DFA ended in a non-accepting state", false,
                $"Rejected: the run ended in {currentNode}, which is not an accept state.");
        }
    }
}
