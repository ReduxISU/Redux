using API.Interfaces;
using API.Tools.UtilCollection;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;

class HittingSetVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Hitting Set Verifier";
    private string _verifierDefinition = "This is a verifier for Hitting Set";
    private string _source = " ";
    private string[] _contributors = {"Russell Phillips"};


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
       public string[] contributors{
        get{
            return _contributors;
        }
    }

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public HittingSetVerifier() {
        
    }

    public IEnumerable<List<int>> possibleSolutions(int len)
    {
        for (int i = 0; i < Math.Pow(2, len); i++) 
        {
            List<int> solution = new List<int>(); 
            for (int solBin = i + (int)Math.Pow(2, len); solBin != 1; solBin >>= 1)
            {
                if ((solBin & 1) == 0)
                    solution.Add(0);
                else
                    solution.Add(1);
            }
            yield return solution;
        }
    }

    private List<string> parseCertificate(string certificate)
    {
        throw new NotImplementedException();
    }

    public bool verify(HITTINGSET problem, string certificate)
    {
        UtilCollection choosenSet = new UtilCollection(certificate);
        foreach (UtilCollection set in problem.subSets)
        {
            if (set.Intersect(choosenSet).Count() != 1)
                return false;
        }
        return true;
    }
}