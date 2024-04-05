using System.Diagnostics;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;

class GraphColoringVerifier : IVerifier
{



    #region Fields
    private string _verifierName = "Graph Coloring Verifier";
    private string _verifierDefinition = "This is a verifier for Graph Coloring.";
    private string _source = "";
    private string[] _contributors = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private string _certificate = "";

    private Dictionary<string, string> _coloring = new Dictionary<string, string>();
    private int _k;

    #endregion

    #region Properties
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

    public string complexity
    {
        get
        {
            return _complexity;
        }

        set
        {
            _complexity = value;
        }
    }

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }

    public Dictionary<string, string> coloring
    {
        get
        {
            return _coloring;
        }

        set
        {
            _coloring = value;
        }
    }

    public int k
    {
        get
        {
            return _k;
        }

        set
        {
            _k = value;
        }
    }



    #endregion

    #region Constructors
    public GraphColoringVerifier()
    {

    }
    #endregion


    #region Methods
    private List<string> parseCertificate(string certificate)
    {

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    public bool verify(GRAPHCOLORING problem, string certificate)
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
                    if ((problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)) && !i.Equals(j))
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

    
    #endregion
}