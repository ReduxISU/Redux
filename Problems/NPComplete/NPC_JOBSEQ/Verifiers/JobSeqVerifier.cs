using API.Interfaces;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;

class JobSeqVerifier : IVerifier<JOBSEQ> {
    public string verifierName {get;} = "Job Sequencing Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Job Sequencing";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Russell Phillips"};


    private string _certificate =  "";

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public JobSeqVerifier() {
        
    }

    public bool verify(JOBSEQ jobseq, List<int> indices) {
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
        List<int> indices = certificate.TrimStart('(')
                                       .TrimEnd(')')
                                       .Split(',')
                                       .Select(int.Parse)
                                       .ToList();
        
        return verify(problem, indices);
    }
}