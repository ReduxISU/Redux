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
        // TODO: implement DFA Solver for DFA
        // Input / Result Strings //
        string inputString = problem.inputString;
        string outputString = "";
        // State the DFA Is Analyzing //
        string currentNode = problem.startState;
        // Saved Sequence of States //
        List<string> nodePath = new List<string>();
        nodePath.Add(currentNode);
     
        // Check Each Character In the Input //
        foreach (char character in inputString)
        {
            // See If An Edge Exists That Can Be Used //
            foreach (Tuple<string,string, char> edge in problem.edges)
            {
                // If Edge Exists, Move To Next Node //
                if (edge.Item3 == character && edge.Item1.Equals(currentNode)) { currentNode = edge.Item2; nodePath.Add(edge.Item2); }
                // If Character Is Outside Alphabet, Flag It and Return //
                if (!problem.alphabet.Contains(character) || !problem.alphabet.Contains(edge.Item3))
                { return "No Solution: The input string or DFA contains an edge with a value not inside the alphabet"; }
            }
        }
        // If We End On an Accept State //
        if (problem.acceptStates.Contains(currentNode))
        {
            outputString += "The sequence of states to accept is: ";
            foreach(string node in nodePath)
            {
                outputString += $"{node}, ";
            }
            return outputString;
        }
        // If We Land On a Garbage State //
        else { return "No Solution Exists"; }
    }
}
