using System;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SHORTESTPATH;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Verifiers;

public class ShortestPathVerifier : IVerifier<SHORTESTPATH> 
{
    public string verifierName { get; } = "Shortest Path Verifier";
    public string verifierDefinition { get; } = "Verifies shortest path solutions";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    private string _certificate = "";
    public string certificate { get => _certificate; }

    public bool verify(SHORTESTPATH problem, string certificate)
    {
        // TODO: Implement verification logic
        return true;
    }
}
