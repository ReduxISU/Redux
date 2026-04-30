using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SETPACKING.Verifiers;

class SetPackingVerifier : IVerifier<SETPACKING>
{
    // --- Fields ---
    public string verifierName { get; } = "Set Packing Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Set Packing";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };

    private string _certificate = "";

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }

    // --- Constructor ---
    public SetPackingVerifier()
    {
    }

    private List<string> parseCertificate(string certificate)
    {
        List<string> selectedSets = GraphParser.parseNodeListWithStringFunctions(certificate);
        return selectedSets;
    }

    public bool verify(SETPACKING problem, string certificate)
    {
        _certificate = certificate;

        List<string> selectedSets = parseCertificate(certificate);

        // Check K value
        if (selectedSets.Count != problem.K)
        {
            return false;
        }

        // Check that every selected set actually exists
        foreach (string setName in selectedSets)
        {
            if (!problem.sets.ContainsKey(setName))
            {
                return false;
            }
        }

        // Check pairwise disjointness
        for (int i = 0; i < selectedSets.Count; i++)
        {
            for (int j = i + 1; j < selectedSets.Count; j++)
            {
                string left = selectedSets[i];
                string right = selectedSets[j];

                if (problem.sets[left].Intersect(problem.sets[right]).Any())
                {
                    return false;
                }
            }
        }

        return true;
    }
} 