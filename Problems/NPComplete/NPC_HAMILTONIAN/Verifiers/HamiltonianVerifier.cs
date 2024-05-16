using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN.Verifiers;

class HamiltonianVerifier : IVerifier
{

    // --- Fields ---
    private string _verifierName = "Hamiltonian Verifier";
    private string _verifierDefinition = "This is a verifier for Undirected Hamiltonian Circut";
    private string _source = " ";
    private string[] _contributors = { "Andrija Sevaljevic" };


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
    public string[] contributors
    {
        get
        {
            return _contributors;
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
    public HamiltonianVerifier()
    {

    }

   
    public bool verify(HAMILTONIAN problem, string certificate)
    {
        List<string> order = certificate.Replace("{","").Replace("}","").Split(',').ToList();
        List<string> check = new List<string>(problem.nodes);
        
        for (int i = 0; i < order.Count - 1; i++)
        {
            KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(order[i], order[i + 1]);
            KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(order[i+1], order[i]);
            if (!(problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)))
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