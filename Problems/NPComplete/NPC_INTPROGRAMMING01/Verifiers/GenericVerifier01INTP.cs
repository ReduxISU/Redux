using API.Interfaces;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

class GenericVerifier01INTP : IVerifier<INTPROGRAMMING01> {

    // --- Fields ---
    public string verifierName { get; } = "0-1 Integer Programming Verifier";
    public string verifierDefinition { get; } = "This is a verifier for 0-1 Integer Programming";
    public string source { get; } = " ";

    private string _certificate = "";
    public string[] contributors { get; } = { "Author Unknown" };

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public GenericVerifier01INTP() {

    }
    public List<int> parseCertificate(string certificate) {
        List<int> c = new List<int>();
        string[] stringVector = certificate.Replace("(", "").Replace(")", "").Split(" ");
        for (int i = 0; i < stringVector.Length; i++) {
            c.Add(int.Parse(stringVector[i]));
        }
        return c;
    }

    //Takes an instance of the 0-1 integer programming problem and a certificate, and verifies if that certificate is a solution
    //c should be in the form of a vector of 1's and 0's separated by spaces. such as "(1 0 1 1 0)"
    public bool verify(INTPROGRAMMING01 problem, string certificate) {
        List<int> cert = parseCertificate(certificate);

        //checks that the certificate is the correct size
        if (cert.Count != problem.C[0].Count) { return false; }

        //compute C*certificate, or Cx
        List<int> solution = new List<int>();
        foreach (var row in problem.C) {
            int value = 0;
            for (int i = 0; i < row.Count; i++) {
                value += row[i] * cert[i];
            }
            solution.Add(value);
        }

        //checks that C*solution <= d
        for (int i = 0; i < problem.d.Count; i++) {
            if (!(solution[i] <= problem.d[i])) { return false; }
        }

        return true;
    }
}