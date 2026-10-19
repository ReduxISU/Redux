using API.Interfaces;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;

class JobSeqVerifier : IVerifier<JOBSEQ> {
    public const string CertificateGrammar = "permutation of 0-based job indices | sum of penalties for jobs missing their deadline <= K";
    public const string CertificateExample = "(1,3,5,4,0,2)";

    public string verifierName { get; } = "Default Job Sequencing Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Job Sequencing";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public JobSeqVerifier() {

    }

    public bool verify(JOBSEQ jobseq, List<int> indices) {
        int n = jobseq.T.Count;
        // The certificate must order every job exactly once.
        if (jobseq.D.Count != n || jobseq.P.Count != n || indices.Count != n)
            return false;
        bool[] seen = new bool[n];
        foreach (int i in indices) {
            if (i < 0 || i >= n || seen[i])
                return false;
            seen[i] = true;
        }

        int penaltySum = 0;
        int timePassed = 0;
        foreach (int i in indices) {
            timePassed += jobseq.T[i];
            if (timePassed > jobseq.D[i]) {
                penaltySum += jobseq.P[i];
            }
        }
        return penaltySum <= jobseq.K;
    }

    public bool verify(JOBSEQ problem, string certificate) {
        List<int> indices = new();
        foreach (string part in certificate.Trim().TrimStart('(').TrimEnd(')').Split(',')) {
            if (!int.TryParse(part.Trim(), out int index))
                return false;
            indices.Add(index);
        }

        return verify(problem, indices);
    }
}
