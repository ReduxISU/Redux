using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_TSP.Verifiers;

class TSPVerifier : IVerifier
{

    // --- Fields ---
    private string _verifierName = "Traveling Sales Person Verifier";
    private string _verifierDefinition = "This is a verifier for the Traveling Sales Person problem";
    private string _source = " ";
    private string[] _contributers = { "Andrija Sevaljevic" };


    private string _certificate = "";

    // --- Properties ---
    public string verifierName
    {
        get
        {
            return _verifierName;
        }
    }
    public string verifierDefinition
    {
        get
        {
            return _verifierDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }
    }
    public string[] contributers
    {
        get
        {
            return _contributers;
        }
    }

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public TSPVerifier()
    {

    }

   
    public bool verify(TSP problem, string certificate)
    {
        List<string> order = certificate.Replace("{","").Replace("}","").Split(',').ToList();
        int sum = 0;
    
        for (int i = 0; i < order.Count - 1; i++)
        {
            bool check1 = problem.edges.Any(tuple => tuple.source == order[i] && tuple.target == order[i+1]);
            bool check2 = problem.edges.Any(tuple => tuple.source == order[i+1] && tuple.target == order[i]);
            if (check1) {
                var matchingTuple = problem.edges.FirstOrDefault(tuple => tuple.Item1 == order[i] && tuple.Item2 == order[i+1]);
                if(matchingTuple != default) {
                    sum += matchingTuple.Item3;
                }
            } else if(check2) {
                var matchingTuple = problem.edges.FirstOrDefault(tuple => tuple.Item1 == order[i+1] && tuple.Item2 == order[i]);
                if(matchingTuple != default) {
                    sum += matchingTuple.Item3;
                }
            } else {
                return false;
            }
        }

        if(sum <= problem.K) return true;

        return false;
        
    }
}