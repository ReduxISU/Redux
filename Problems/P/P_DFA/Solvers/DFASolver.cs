using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_DFA;

namespace API.Problems.P.P_DFA.Solvers;

class DFASolver : ISolver<DFA>
{

    // ----- Fields ----- //
    public string solverName { get; } = "DFA Solver";
    public string solverDefinition { get; } = "This a solver for a Determiistic Finite Automata that returns no solution in none exists, or a solution consisting of the set states that led to an acceptance.";
    public string source { get; } = "";
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

    private List<string> nodePath = [];

    // Methods Including Constructors //
    public DFASolver() { }

    public string solve(DFA problem)
    {
        // Input String //
        string inputString = problem.inputString;
        // First Node To Be Analyzed //
        string currentNode = problem.startState;
        // Will Track Path Through Nodes //
        nodePath = new List<string> { currentNode };

        foreach (char character in inputString)
        {
            // Accept Empty String If Start State Is an Accept State //
            if (character == 'ε' && problem.acceptStates.Contains(currentNode)) return $"The sequence of states to accept is: {currentNode}";

            // Check If Character Is In Alphabet //
            if (!problem.alphabet.Contains(character))
            {
                return $"No Solution: Input contains character '{character}' not in DFA alphabet";
            }

            // Follow the Edge //
            bool foundEdge = false;
            foreach (var edge in problem.edges)
            {
                if (edge.From == currentNode && edge.Symbol == character)
                {
                    currentNode = edge.To;
                    nodePath.Add(currentNode);
                    foundEdge = true;
                    break;
                }
            }

            // If No Edge, DFA Stops //
            if (!foundEdge)
            {
                return "No Solution Exists: DFA cannot transition with this character";
            }
        }

        // Check If Last State Is Accept State //
        if (problem.acceptStates.Contains(currentNode))
        {
            return "The sequence of states to accept is: " + string.Join(", ", nodePath);
        }
        else
        {
            return "No Solution Exists: The DFA ended in a non-accepting state";
        }
    }

    public List<Object> GetSteps(string instance)
    {
        solve(new DFA(instance));

        return nodePath.Cast<Object>().ToList();
    }

    // ----- Table Visualization Support ----- //

    public class DFATableStepRow
    {
        public int step { get; set; }
        public string symbol { get; set; } = "-";
        public string fromState { get; set; } = "-";
        public string toState { get; set; } = "";
        public bool accepting { get; set; }
    }

    // One frame of the step slider: the whole trace (`rows`) plus the row that frame is "at"
    // (`currentRow`). Every frame carries every row, so the table itself never changes shape and
    // only the highlight moves -- the same frame shape SPSP/SSSP feed to the shared DynamicTable
    // renderer, which is why no DFA-specific renderer or first/last-step special case is needed.
    public class DFATableStep
    {
        public int currentRow { get; set; }
        public List<DFATableStepRow> rows { get; set; } = new();
    }

    // GetTableSteps: Traces the single deterministic path through the DFA once, then emits one
    // frame per row of that trace, walking the highlight down the table.
    public List<Object> GetTableSteps(DFA problem)
    {
        string inputString = problem.inputString;
        string currentNode = problem.startState;

        var rows = new List<DFATableStepRow>
        {
            new DFATableStepRow
            {
                step = 0,
                toState = currentNode,
                accepting = problem.acceptStates.Contains(currentNode)
            }
        };

        int stepIndex = 1;
        foreach (char character in inputString)
        {
            if (character == 'ε' && problem.acceptStates.Contains(currentNode))
                break;

            if (!problem.alphabet.Contains(character))
                break; // Input invalid for this DFA; table stops where the trace stalls

            var matchedEdge = problem.edges.FirstOrDefault(e => e.From == currentNode && e.Symbol == character);
            if (matchedEdge == null)
                break; // No transition available; DFA rejects, table stops where the trace stalls

            currentNode = matchedEdge.To;
            rows.Add(new DFATableStepRow
            {
                step = stepIndex,
                symbol = character.ToString(),
                fromState = matchedEdge.From,
                toState = matchedEdge.To,
                accepting = problem.acceptStates.Contains(currentNode)
            });
            stepIndex++;
        }

        // `rows` is shared by reference across frames: it is finished being built here and every
        // frame is a read-only view of the same completed trace.
        return rows
            .Select((_, i) => (Object)new DFATableStep { currentRow = i, rows = rows })
            .ToList();
    }
}
