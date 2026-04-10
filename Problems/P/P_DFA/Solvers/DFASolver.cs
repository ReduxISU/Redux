using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_DFA;
using Xunit;

namespace API.Problems.P.P_DFA.Solvers;

public class DFASolver : ISolver<DFA>
{

    // ----- Fields ----- //
    public string solverName { get; } = "DFA Solver";
    public string solverDefinition { get; } = "This a solver for a Determiistic Finite Automata that returns no solution in none exists, or a solution consisting of the set states that led to an acceptance.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };

    public bool timerHasExpired { get; set; }

    // Methods Including Constructors //
    public DFASolver() { }

    public string solve(DFA problem)
    {
        // Input String //
        string inputString = problem.inputString;
        // First Node To Be Analyzed //
        string currentNode = problem.startState;
        // Will Track Path Through Nodes //
        List<string> nodePath = new List<string> { currentNode };

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
}