using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_ACCEPTDFA;
using Xunit;

namespace API.Problems.NPComplete.NPC_ACCEPTDFA.Solvers;

public class DFASolver : ISolver<DFA>
{

    // ----- Fields ----- //
    public string solverName { get; } = "DFA Solver";
    public string solverDefinition { get; } = "This a solver for a Determiistic Finite Automata that returns no solution in none exists, or a solution consisting of the set states that led to an acceptance.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };

    // ----- Methods Including Constructors ----- //
    public DFASolver() { }

    public string solve(DFA problem)
    {
        string inputString = problem.inputString;
        string currentNode = problem.startState;
        List<string> nodePath = new List<string> { currentNode };

        foreach (char character in inputString)
        {
            // Check if character is in alphabet
            if (!problem.alphabet.Contains(character))
            {
                // Skip or fail gracefully
                return $"No Solution: Input contains character '{character}' not in DFA alphabet";
            }

            // Follow the edge
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

            // If no edge exists, DFA cannot continue
            if (!foundEdge)
            {
                return "No Solution Exists: DFA cannot transition with this character";
            }
        }

        // Check if the last state is accepting
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