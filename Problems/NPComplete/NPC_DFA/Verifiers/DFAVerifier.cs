using API.Interfaces;
using API.Problems.NPComplete.NPC_DFA;

namespace API.Problems.NPComplete.NPC_DFA.Verifiers;

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
    public DFAVerifier() {}

    public bool verify(DFA problem, string certificate)
    {
        // Trim Unwanted Space //
        certificate.Trim();
        // Get Each Node //
        nodes = certificate.Split(',');

        // Start State //
        string currentNode = nodes[0];
        // Tracks If Acceptable Edge Was Found //
        bool foundEdge = false;

        // For Each Node //
        for (int i = 0; i < nodes.Count() - 1; i++)
        {
            // Check If Character Is In Alphabet //
            if (!problem.nodes.Contains(nodes[i])) { return false; }

            if (problem.nodes.Contains(nodes[i]))
            {
                foundEdge = false;
                foreach (var edge in problem.edges)
                {
                    if (!edge.From.Equals(currentNode)) continue;
                    else if (edge.From.Equals(nodes[i]) && !edge.To.Equals(nodes[i + 1])) continue;
                    else if (!edge.From.Equals(nodes[i]) && !edge.To.Equals(nodes[i + 1])) continue;
                    else { currentNode = nodes[i + 1]; foundEdge = true; }
                }
            }

            // If No Edge DFA Returns False //
            if (!foundEdge) return false;
        }

        // Return True If Last State Is Accept State //
        if (problem.acceptStates.Contains(currentNode)) return true;
        else return false;
    }
}