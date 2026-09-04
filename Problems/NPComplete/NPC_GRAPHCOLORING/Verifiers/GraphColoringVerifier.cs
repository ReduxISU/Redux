using System.Diagnostics;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;

class GraphColoringVerifier : IVerifier<GRAPHCOLORING> {
    public const string CertificateGrammar = "{c1,...,cK} | color classes partition N, no edge in E has both endpoints in the same class";
    public const string CertificateExample = "{{a},{b,d,f,h},{c,e,g,i}}";



    #region Fields
    public string verifierName { get; } = "Graph Coloring Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Graph Coloring.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private string _certificate = "";

    private Dictionary<string, string> _coloring = new Dictionary<string, string>();
    private int _k;

    #endregion

    #region Properties
    public string complexity {
        get {
            return _complexity;
        }

        set {
            _complexity = value;
        }
    }

    public string certificate {
        get {
            return _certificate;
        }
    }

    public Dictionary<string, string> coloring {
        get {
            return _coloring;
        }

        set {
            _coloring = value;
        }
    }

    public int k {
        get {
            return _k;
        }

        set {
            _k = value;
        }
    }



    #endregion

    #region Constructors
    public GraphColoringVerifier() {

    }
    #endregion


    #region Methods
    private List<List<string>> ParseCertificate(string certificate) {
        UtilCollection colorClasses = new UtilCollection(certificate);
        colorClasses.assertUnordered();
        return colorClasses.ToList().Select(cls => {
            cls.assertUnordered();
            return cls.ToList().Select(n => n.ToString()).ToList();
        }).ToList();
    }

    public bool verify(GRAPHCOLORING problem, string certificate) {
        List<List<string>> nodeSet;
        try {
            nodeSet = ParseCertificate(certificate);
        } catch {
            return false;
        }
        List<string> bandAid = new List<string>(problem.nodes);
        foreach (var nodeList in nodeSet) {
            foreach (var i in nodeList) {
                if (!bandAid.Contains(i)) {
                    return false;
                }

                bandAid.Remove(i);

                foreach (var j in nodeList) {
                    KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                    KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                    if ((problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)) && !i.Equals(j)) {
                        return false;
                    }
                }
            }
        }

        if (bandAid.Any()) {
            return false;
        }
        return true;
    }


    #endregion
}