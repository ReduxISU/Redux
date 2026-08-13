using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using SPADE;

namespace API.Problems.NPComplete.NPC_EDITDISTANCE.Verifiers;

class EditDistanceVerifier : IVerifier<EDITDISTANCE> {
    public string verifierName { get; } = "Edit Distance Verifier";
    public string verifierDefinition { get; } = "Verifies that a proposed edit distance is correct for the input strings.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Edit_distance";
    public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };
    private string _certificate = string.Empty;
    public string certificate => _certificate;


    // --- Methods Including Constructors ---
    public EditDistanceVerifier() { }

    public bool verify(EDITDISTANCE problem, string certificate) {
        if (!int.TryParse(certificate.Trim(), out int claimed))
            return false;

        var solver = new EditDistanceDPSolver();
        int actual = int.Parse(solver.solve(problem));
        return claimed == actual;
    }
}
