using System.Diagnostics;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;

class GraphColoringVerifier : IVerifier<GRAPHCOLORING> {



    #region Fields
    public string verifierName { get; } = "Default Verifier";
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
    private List<string> parseCertificate(string certificate) {

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    public bool verify(GRAPHCOLORING problem, string certificate) {
        List<string> bandAid = new List<string>(problem.nodes);
        List<string> nodeSet = certificate.Split("},{").ToList();
        foreach (var k in nodeSet) {
            List<string> nodeList = parseCertificate(k);

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