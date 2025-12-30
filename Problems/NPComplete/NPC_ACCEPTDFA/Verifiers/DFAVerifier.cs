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

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public DFAVerifier()
    {
       
    }

    public bool verify(DFA problem, string certificate)
    {
        // TODO: implement DFA Verifier for DFA
        //foreach (var edge in problem.edges)
        //{

        //}
        return true;
    }
}
