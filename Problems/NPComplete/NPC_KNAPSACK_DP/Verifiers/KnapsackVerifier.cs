using API.Interfaces;
using System;
using SPADE;


namespace API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;

class KnapsackVerifier : IVerifier<KNAPSACK> {

    // --- Fields ---
    public string verifierName {get;} = "Knapsack Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Knapsack. It checks that that the weight of the chosen items do not exceed the allowed weight and that the value of the items exceed the required value";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Garret Stouffer", "Daniel Igbokwe", "Russell Phillips"};

    private string _complexity = "O(n^2)";

    private string _certificate = "{(30:120,20:100):220}";

    public string complexity
    {
        get
        {
            return _complexity;
        }
    }

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public KnapsackVerifier()
    {

    }

    public bool verify(KNAPSACK problem, string certificate)
    {
        UtilCollection solution;
        try
        {
            solution = parseCertificate(problem, certificate);
        }
        catch 
        {
            return false;
        } 
        int totalW = 0;
        int totalV = 0;

        foreach (UtilCollection solItem in solution)
        {
            totalW += solItem[0].parseInt();
            totalV += solItem[1].parseInt();
        }

        if (totalW <= problem.W && totalV >= problem.V) return true;
        else return false;
    
    }

    private UtilCollection parseCertificate(KNAPSACK problem, string certificate)
    {
        UtilCollection solution = new UtilCollection(certificate);
        foreach (UtilCollection solItem in solution)
        {
            solItem.assertPair();
            if (!problem.items.Contains(solItem)) throw new Exception();
        }
        return solution;
    }


}