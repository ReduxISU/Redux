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

    // Steps: one Try for the start state, then one Try per input symbol consumed (the transition taken).
    // The run dies with a Reject; Done always closes the run with the states it stopped in.
    // Recorder state lives only in this call, so the solver object stays stateless.
    public string Solve(DFA problem, StepRecorder<ActiveStates> rec) {
        // Input String //
        string inputString = problem.inputString;
        // First Node To Be Analyzed //
        string currentNode = problem.startState;
        // Will Track Path Through Nodes //
        var nodePath = new List<string> { currentNode };
        int consumed = 0;

        rec.Try(() => new ActiveStates([problem.startState], 0),
            () => $"Start in {problem.startState}.", problem.startState);

        string Finish(string answer, bool ok, string caption) {
            string end = currentNode;
            int at = consumed;
            rec.Done(new ActiveStates([end], at), ok, caption);
            return answer;
        }

        foreach (char character in inputString) {
            // Accept Empty String If Start State Is an Accept State //
            if (character == 'ε' && problem.acceptStates.Contains(currentNode))
                return Finish($"The sequence of states to accept is: {currentNode}", true, $"The input is empty and {currentNode} is an accept state, so the DFA accepts.");

            // Check If Character Is In Alphabet //
            if (!problem.alphabet.Contains(character)) {
                rec.Reject(() => new ActiveStates([currentNode], consumed),
                    () => $"'{character}' is not in the alphabet, so the DFA cannot read it.", currentNode);
                return Finish($"No Solution: Input contains character '{character}' not in DFA alphabet", false,
                    $"Rejected: '{character}' is not in the alphabet.");
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
                        () => $"Read '{character}': move from {edge.From} to {edge.To}.", edge.From, edge.To);
                    break;
                }
            }

            // If No Edge, DFA Stops //
            if (!foundEdge) {
                rec.Reject(() => new ActiveStates([currentNode], consumed),
                    () => $"No move from {currentNode} on '{character}', so the run stops.", currentNode);
                return Finish("No Solution Exists: DFA cannot transition with this character", false,
                    $"Rejected: {currentNode} has no move on '{character}'.");
            }
        }

        // Check If Last State Is Accept State //
        if (problem.acceptStates.Contains(currentNode)) {
            return Finish("The sequence of states to accept is: " + string.Join(", ", nodePath), true,
                $"The input is used up in {currentNode}, an accept state, so the DFA accepts.");
        } else {
            rec.Reject(() => new ActiveStates([currentNode], consumed),
                () => $"The input is used up in {currentNode}, which is not an accept state.", currentNode);
            return Finish("No Solution Exists: The DFA ended in a non-accepting state", false,
                $"Rejected: the run ended in {currentNode}, which is not an accept state.");
        }
    }
}
