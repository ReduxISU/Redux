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

    public bool verify(string problem, string solution)
    {
        // TODO: Implement verification logic
        return true;
    }

    public string certificate(SHORTESTPATH problem, string solution)
    {
        // TODO: Implement certificate generation
        return solution;
    }
}
