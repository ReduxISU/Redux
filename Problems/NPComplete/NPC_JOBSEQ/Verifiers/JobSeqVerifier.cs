using API.Interfaces;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;

class JobSeqVerifier : IVerifier {
    private string _verifierName = "Generic Verifier";
    private string _verifierDefinition = "This is a verifier for Job Sequencing";
    private string _source = " ";
    private string[] _contributers = {"Russell Phillips"};


    private string _certificate =  "";

    // --- Properties ---
    public string verifierName {
        get {
            return _verifierName;
        }
    }
    public string verifierDefinition {
        get {
            return _verifierDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
       public string[] contributers{
        get{
            return _contributers;
        }
    }

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

    public bool verify(JOBSEQ jobseq, string certificate) {
        List<int> indices = certificate.TrimStart('(')
                                       .TrimEnd(')')
                                       .Split(',')
                                       .Select(int.Parse)
                                       .ToList();
        
        return verify(jobseq, indices);
    }
}