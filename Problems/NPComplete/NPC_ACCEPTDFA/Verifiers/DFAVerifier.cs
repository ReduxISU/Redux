using API.Interfaces;
using API.Problems.NPComplete.NPC_ACCEPTDFA;

namespace API.Problems.NPComplete.NPC_ACCEPTDFA.Verifiers;

public class DFAVerifier : IVerifier<DFA>
{
    // --- Fields ---
    public string verifierName { get; } = "DFA Verifier";
    public string verifierDefinition { get; } = "This is a solver for DFAs";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    private string _certificate = "";

    private string[] nodes;

    public string certificate { get => _certificate; }

    // --- Methods Including Constructors ---
    public DFAVerifier()
    {

    }

    public bool verify(DFA problem, string certificate)
    {
        certificate.Trim();
        nodes = certificate.Split(',');

        string currentNode = nodes[0];
        bool foundEdge = false;

        for (int i = 0; i < nodes.Count() -1; i++)
        {
            // Check if character is in alphabet
            if (!problem.nodes.Contains(nodes[i])) { return false; }

            if (problem.nodes.Contains(nodes[i]))
            {
                foreach (var edge in problem.edges)
                {
                    if (!edge.From.Equals(currentNode)) continue;
                    else if (edge.From.Equals(nodes[i]) && !edge.To.Equals(nodes[i + 1])) continue;
                    else { currentNode = nodes[i + 1]; foundEdge = true; }
                }
            }

            // If no edge exists, DFA cannot continue
            if (!foundEdge) return false;
        }

        // Check if the last state is accepting
        if (problem.acceptStates.Contains(currentNode)) return true;
        else return false;
    }
}
