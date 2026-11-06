using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_DFA;
using static API.Interfaces.Graphs.AutomatonFrames;

namespace API.Problems.P.P_DFA.Solvers;

// The steps DFA Simulation records, for the Automaton picture (ids from AutomatonFrames):
//   - an Update for the start state;
//   - an Update per symbol read: the machine follows the one arrow for it to the next state;
//   - a Reject when there is no arrow to follow (the symbol has no move, or is not in the alphabet at all): the machine
//     falls into the Garbage state and stays there;
//   - a Reject when the input is used up in a state that does not accept;
//   - the Done, with the state the run ended in (none for Garbage) and whether the DFA accepts.
// A run has nothing to judge until it fails or the input ends, so its moves are Updates. Whether the DFA accepts the input
// is the Done's verdict, not an Accept step: an Accept means the solver keeps something, and a DFA run keeps nothing.
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
    public MathematicalFunction timeComplexity { get; } = new("n * E", "");

    // Methods Including Constructors //
    public DFASolver() { }

    // How the steps show the run: the state it is in (null once it has fallen into the Garbage state), how many symbols it
    // has read, and the state it has just moved from (null when the step is not a move).
    private static RunSteps<(string? In, int Read, string? From), ActiveStates> NewSteps(StepRecorder<ActiveStates> rec) => new(rec,
        // What every picture shows: the state the run is in, and how far into the input it is.
        partial: s => new ActiveStates(s.In is null ? [] : [s.In], s.Read),
        // The arrow just taken; else the state the run is in, or the Garbage state.
        focus: s => s.From is not null ? [TransitionId(s.From, s.In!)] : s.In is null ? [GarbageId] : [StateId(s.In)]);

    public string solve(DFA problem) => Solve(problem, StepRecorder<ActiveStates>.Off);

    // Starts in the start state and, for each symbol of the input in turn, follows the one arrow out of the current state
    // labelled with it. The DFA accepts when the input is used up in an accept state.
    public string Solve(DFA problem, StepRecorder<ActiveStates> rec) {
        SolveProbe.Hit(GetType());
        var steps = NewSteps(rec);
        // Input String (the empty input is written "ε", and reads no symbol) //
        string inputString = problem.inputString == "ε" ? "" : problem.inputString;
        // First Node To Be Analyzed //
        string currentNode = problem.startState;
        // Will Track Path Through Nodes //
        var nodePath = new List<string> { currentNode };
        int consumed = 0;

        steps.Update((currentNode, 0, null), $"Start in {currentNode}.");

        foreach (char character in inputString) {
            // Check If Character Is In Alphabet //
            // A symbol outside the alphabet is a missing move like any other: the DFA falls into the Garbage state.
            if (!problem.alphabet.Contains(character)) {
                steps.Reject((null, consumed, null), $"'{character}' is not in the alphabet, so {currentNode} has no move on it, and the DFA falls into the Garbage state and stays there.");
                steps.Done(new ActiveStates([], consumed), false, $"Rejected: '{character}' is not in the alphabet, so {currentNode} has no move on it and the run ends in the Garbage state.");
                return $"No Solution: Input contains character '{character}' not in DFA alphabet";
            }

            // Follow the Edge //
            bool foundEdge = false;
            foreach (var edge in problem.edges) {
                if (edge.From == currentNode && edge.Symbol == character) {
                    currentNode = edge.To;
                    nodePath.Add(currentNode);
                    consumed++;
                    foundEdge = true;
                    steps.Update((edge.To, consumed, edge.From), $"Read '{character}': move from {edge.From} to {edge.To}.");
                    break;
                }
            }

            // If No Edge, DFA Stops: It Falls Into the Garbage State //
            if (!foundEdge) {
                steps.Reject((null, consumed, null), $"{currentNode} has no move on '{character}', so the DFA falls into the Garbage state and stays there.");
                steps.Done(new ActiveStates([], consumed), false, $"Rejected: {currentNode} has no move on '{character}', so the run ends in the Garbage state.");
                return "No Solution Exists: DFA cannot transition with this character";
            }
        }

        // Check If Last State Is Accept State //
        // The answer is the run as a certificate DFAVerifier accepts ("1,2").
        if (problem.acceptStates.Contains(currentNode)) {
            steps.Done(new ActiveStates([currentNode], consumed), true, $"The input is used up in {currentNode}, an accept state, so the DFA accepts.");
            return string.Join(",", nodePath);
        } else {
            steps.Reject((currentNode, consumed, null), $"The input is used up in {currentNode}, which is not an accept state.");
            steps.Done(new ActiveStates([currentNode], consumed), false, $"Rejected: the run ended in {currentNode}, which is not an accept state.");
            return "No Solution Exists: The DFA ended in a non-accepting state";
        }
    }
}
