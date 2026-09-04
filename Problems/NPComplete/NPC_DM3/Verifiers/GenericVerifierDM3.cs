using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DM3.Verifiers;

class GenericVerifierDM3 : IVerifier<DM3> {

    // --- Fields ---
    public string verifierName { get; } = "3-Dimensional Matching Verifier";
    public string verifierDefinition { get; } = "This verifier checks that a given certificate is the correct size, and contains all elements of X, Y and Z";
    public string source { get; } = "";
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
        List<string> variableList = certificate.Replace("{{", "{").Replace("}}", "}").Replace("{", "").Replace("}", "").Split(',').ToList();
        if (variableList.Count % 3 != 0) variableList.Clear();
        return variableList;

    }

    // Take in a problem and a possible solution and evaluate it. Expected userInput follows the format ({Matching in solution}{Matching in solution}{Matching in solution}...)
    // EXAMPLE: "{x1,y2,z4}{x2,y1,z1}{x2,y1,z2}{x2,y2,z1}"
    // ONLY true literal names should be included in the user input seperated by commas
    public bool verify(DM3 Problem, string certificate) {
        List<string> problemVariables = ParseCertificate(certificate);
        List<string> firstSet = new List<string>();
        List<string> secondSet = new List<string>();
        List<string> thirdSet = new List<string>();

        if (!problemVariables.Any()) return false;
        if (problemVariables.Distinct().Count() != problemVariables.Count()) return false;

        for (int i = 0; i < problemVariables.Count(); i = i + 3) {
            if (firstSet.Contains(problemVariables[i]) || secondSet.Contains(problemVariables[i + 1]) || thirdSet.Contains(problemVariables[i + 2])) return false;
            firstSet.Add(problemVariables[i]);
            secondSet.Add(problemVariables[i + 1]);
            thirdSet.Add(problemVariables[i + 2]);
        }

        return firstSet.All(item => Problem.X.Contains(item)) && secondSet.All(item => Problem.Y.Contains(item)) && thirdSet.All(item => Problem.Z.Contains(item));
    }
}

