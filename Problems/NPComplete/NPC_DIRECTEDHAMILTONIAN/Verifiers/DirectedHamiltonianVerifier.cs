using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Verifiers;

class DirectedHamiltonianVerifier : IVerifier<DIRECTEDHAMILTONIAN>
{

    // --- Fields ---
    public string verifierName {get;} = "Directed Hamiltonian Path Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Directed Hamiltonian Path";
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
    public DirectedHamiltonianVerifier()
    {

    }

   
    public bool verify(DIRECTEDHAMILTONIAN problem, string certificate)
    {
        List<string> order = certificate.Replace("{","").Replace("}","").Split(',').ToList();
        List<string> check = new List<string>(problem.nodes);
        
        for (int i = 0; i < order.Count - 1; i++)
        {
            KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(order[i], order[i + 1]);
            KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(order[i+1], order[i]);
            if (!problem.edges.Contains(pairCheck1))
            {
                return false;
            }
            if(check.Contains(order[i])) {
                check.Remove(order[i]);
            } else {
                return false;
            }
        }

        if(check.Any()) {
            return false;
        }
        
        return true;
        
    }
}