using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER.Verifiers;

class CliqueCoverVerifier : IVerifier
{

    // --- Fields ---
    private string _verifierName = "Clique Cover Verifier";
    private string _verifierDefinition = "This is a verifier for the NP-Complete Clique Cover problem";
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
    public CliqueCoverVerifier()
    {

    }

    private List<string> parseCertificate(string certificate)
    {

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    public bool verify(CLIQUECOVER problem, string certificate)
    {
        List<string> bandAid = new List<string>(problem.nodes);
        List<string> nodeSet = certificate.Split("},{").ToList();
        foreach (var k in nodeSet)
        {
            List<string> nodeList = parseCertificate(k);
    
            foreach (var i in nodeList)
            {
                if (!bandAid.Contains(i)) {
                    return false;
                }

                bandAid.Remove(i);

                foreach (var j in nodeList)
                {
                    KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                    KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                    if (!(problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2) || i.Equals(j)) || i == "")
                    {
                        return false;
                    }
                }
            }
        }

        if(bandAid.Any()) {
            return false;
        }
        return true;
    }
}