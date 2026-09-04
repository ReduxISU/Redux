using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPComplete.NPC_DOMINATINGSET;
using SPADE;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Verifiers;

class DominatingSetVerifier : IVerifier<DOMINATINGSET> {
    public const string CertificateGrammar = "D subset N | every node not in D has a neighbor in D (dominating set), |D| <= K";
    public const string CertificateExample = "{1,3}";

    // --- Fields ---
    private string _verifierName = "Dominating Set Verifier";
    private string _verifierDefinition = "This is a Verifier for Dominating Set";
    private string _source =
        "Wendy Myrvold, CSC 425 Notes: Domination Algorithms, University of Victoria.";
    private string _sourceLink =
        "https://webhome.cs.uvic.ca/~wendym/courses/425/14/notes/425_03_dom_alg.pdf";
    private string[] _contributors = { "Quinton Smith" };
    private string _certificate = string.Empty;

    // --- Properties ---
    public string verifierName => _verifierName;
    public string verifierDefinition => _verifierDefinition;
    public string source => _source;
    public string sourceLink => _sourceLink;
    public string[] contributors => _contributors;
    public string certificate => _certificate;


    // --- Methods Including Constructors ---
    public DominatingSetVerifier() {

    }

    // Builds an adjacency list from the edge list
    private Dictionary<string, List<string>> BuildAdjacencyList(List<KeyValuePair<string, string>> edges) {

        var adjacencyList = new Dictionary<string, List<string>>();

        foreach (var e in edges) {
            if (!adjacencyList.ContainsKey(e.Key)) adjacencyList[e.Key] = new List<string>();
            if (!adjacencyList.ContainsKey(e.Value)) adjacencyList[e.Value] = new List<string>();

            if (!adjacencyList[e.Key].Contains(e.Value)) adjacencyList[e.Key].Add(e.Value);
            if (!adjacencyList[e.Value].Contains(e.Key)) adjacencyList[e.Value].Add(e.Key);
        }
        return adjacencyList;
    }

    // Gets all vertices from the problem and edges
    private HashSet<string> GetAllVertices(DOMINATINGSET problem, List<KeyValuePair<string, string>> edges) {

        var V = new HashSet<string>();
        foreach (var e in edges) {
            V.Add(e.Key);
            V.Add(e.Value);
        }

        foreach (var label in problem.nodes) {
            V.Add(label);
        }

        return V;
    }

    // Parses the certificate string (a set of chosen node names) via SPADE
    private List<string> ParseCertificate(string certificate) {
        UtilCollection chosen = new UtilCollection(certificate);
        chosen.assertUnordered();
        return chosen.ToList().Select(n => n.ToString()).ToList();
    }

    private bool CandidateVerticesExist(HashSet<string> allVertices, IEnumerable<string> candidate)
             => candidate.All(v => allVertices.Contains(v));

    private bool IsDominating(HashSet<string> allVertices, Dictionary<string, List<string>> adj, HashSet<string> D) {
        foreach (var u in allVertices) {
            if (D.Contains(u)) continue;
            if (!adj.TryGetValue(u, out var nbrs)) nbrs = new List<string>();
            bool dominated = nbrs.Any(D.Contains);
            if (!dominated) return false;
        }
        return true;
    }



    public bool verify(DOMINATINGSET problem, string certificate) {
        _certificate = certificate ?? string.Empty;

        HashSet<string> chosen;
        try {
            chosen = new HashSet<string>(ParseCertificate(_certificate));
        } catch {
            return false;
        }
        var adj = BuildAdjacencyList(problem.edges);
        var allV = GetAllVertices(problem, problem.edges);

        if (!CandidateVerticesExist(allV, chosen)) return false;
        if (!IsDominating(allV, adj, chosen)) return false;

        // Enforce |D| <= K (your DOMINATINGSET exposes K)
        if (chosen.Count > problem.K) return false;

        return true;
    }


}
