using System;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_SSSP.Solvers;

namespace API.Problems.P.P_SSSP.Verifiers;

class SSSPVerifier : IVerifier<SSSP>
{
    public string verifierName { get; } = "Single Source Shortest Path Verifier";
    public string verifierDefinition { get; } = "Verifies the solution for the Single Source Shortest Path problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    private string _certificate = "";
    public string certificate => _certificate;

    public SSSPVerifier() { }

    public bool verify(SSSP problem, string solution)
    {
        return true;    //To Do
                        //Placeholder for now, will implement verifier logic 
    }
}