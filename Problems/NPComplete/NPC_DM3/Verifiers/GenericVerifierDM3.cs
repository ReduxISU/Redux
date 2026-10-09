using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DM3.Verifiers;

class GenericVerifierDM3 : IVerifier<DM3> {

    // --- Fields ---
    public string verifierName { get; } = "Default 3-Dimensional Matching Verifier";
    public string verifierDefinition { get; } = "This verifier checks that a certificate is a perfect matching: every triple is one of the constraints in M, no element of X, Y or Z is used twice, and every element of X, Y and Z is covered.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public GenericVerifierDM3() {

    }


    /*************************************************
    ParseCertificate(string certificate) takes the string representation of the 3-Dimensional Matching solution, and returns a 
    2-dimensional list, Each inner lists will be sets of 3 elements. 
    ***************************************************/
    private List<string> ParseCertificate(string certificate) {
        List<string> variableList = certificate.Replace("}{", "},{").Replace("{{", "{").Replace("}}", "}").Replace("{", "").Replace("}", "").Split(',').ToList();
        if (variableList.Count % 3 != 0) variableList.Clear();
        return variableList;

    }

    // Take in a problem and a possible solution and evaluate it. Expected userInput follows the format ({Matching in solution}{Matching in solution}{Matching in solution}...)
    // EXAMPLE: "{x1,y2,z4}{x2,y1,z1}{x2,y1,z2}{x2,y2,z1}"
    // ONLY true literal names should be included in the user input seperated by commas
    public bool verify(DM3 Problem, string certificate) {
        List<string> problemVariables = ParseCertificate(certificate);
        HashSet<string> firstSet = new HashSet<string>();
        HashSet<string> secondSet = new HashSet<string>();
        HashSet<string> thirdSet = new HashSet<string>();

        if (!problemVariables.Any()) return false;

        for (int i = 0; i < problemVariables.Count(); i = i + 3) {
            string x = problemVariables[i], y = problemVariables[i + 1], z = problemVariables[i + 2];
            // Every chosen triple must be one of the constraints in M.
            if (!Problem.M.Any(t => t.Count == 3 && t[0] == x && t[1] == y && t[2] == z)) return false;
            // No element may be used by two triples (the triples are pairwise disjoint).
            if (!firstSet.Add(x) || !secondSet.Add(y) || !thirdSet.Add(z)) return false;
        }

        // The triples must cover every element of X, Y and Z (a perfect matching).
        return Problem.X.All(firstSet.Contains) && Problem.Y.All(secondSet.Contains) && Problem.Z.All(thirdSet.Contains);
    }
}

