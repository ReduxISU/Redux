using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_TSP.Verifiers;

class TSPVerifier : IVerifier<TSP> {

    // --- Fields ---
    public string verifierName {get;} = "Traveling Sales Person Verifier";
    public string verifierDefinition {get;} = "This is a verifier for the Traveling Sales Person problem";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Andrija Sevaljevic" };


    private string _certificate = "";

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