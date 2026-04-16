using API.Interfaces;
using System.Linq;
using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_SETPACKING.Verifiers;

public class SetPackingVerifier : IVerifier<API.Problems.NPComplete.NPC_SETPACKING.SETPACKING>
{
    public string verifierName { get; } = "Set Packing Verifier";
    public string verifierDefinition { get; } = "Checks no overlap";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };

    private string _certificate = "";

    public string certificate => _certificate;

    private List<string> parse(string cert)
    {
        return cert.Trim('{', '}')
                   .Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(x => x.Trim())
                   .ToList();
    }

    public bool verify(SETPACKING problem, string certificate)
    {
        _certificate = certificate;

        var chosen = parse(certificate);

        if (chosen.Count != problem.K)
            return false;

        for (int i = 0; i < chosen.Count; i++)
        {
            for (int j = i + 1; j < chosen.Count; j++)
            {
                var s1 = problem.sets[chosen[i]];
                var s2 = problem.sets[chosen[j]];

                if (s1.Intersect(s2).Any())
                    return false;
            }
        }

        return true;
    }
}