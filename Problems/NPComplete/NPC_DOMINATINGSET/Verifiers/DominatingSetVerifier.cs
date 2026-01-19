using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPComplete.NPC_DOMINATINGSET;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Verifiers;

class DominatingSetVerifier : IVerifier<DOMINATINGSET>
{

    // --- Fields ---
    private string _verifierName = "Dominating Set Verifier";
    private string _verifierDefinition = "This is a Verifier for Dominating Set";
    private string _source = "Quinton Smith";
    private string[] _contributors = { "Quinton Smith" };
    private string _certificate = string.Empty;

    // --- Properties ---
    public string verifierName => _verifierName;
    public string verifierDefinition => _verifierDefinition;
    public string source => _source;
    public string[] contributors => _contributors;
    public string certificate => _certificate;


    // --- Methods Including Constructors ---
    public DominatingSetVerifier()
    {

    }

    // Builds an adjacency list from the edge list
    private Dictionary<string, List<string>> BuildAdjacencyList(List<KeyValuePair<string, string>> edges)
    {

        var adjacencyList = new Dictionary<string, List<string>>();

        foreach (var e in edges)
        {
            if (!adjacencyList.ContainsKey(e.Key)) adjacencyList[e.Key] = new List<string>();
            if (!adjacencyList.ContainsKey(e.Value)) adjacencyList[e.Value] = new List<string>();

            if (!adjacencyList[e.Key].Contains(e.Value)) adjacencyList[e.Key].Add(e.Value);
            if (!adjacencyList[e.Value].Contains(e.Key)) adjacencyList[e.Value].Add(e.Key);
        }
        return adjacencyList;
    }

    // Gets all vertices from the problem and edges
    private HashSet<string> GetAllVertices(DOMINATINGSET problem, List<KeyValuePair<string, string>> edges)
    {

        var V = new HashSet<string>();
        foreach (var e in edges)
        {
            V.Add(e.Key);
            V.Add(e.Value);
        }

        foreach (var label in problem.nodes)
        {
            V.Add(label);
        }

        return V;
    }

    // Parses the certificate string into a list of strings
    private List<string> ParseCertificate(string certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate))
        {
            return new List<string>();
        }
        certificate = certificate.Trim();

        if (certificate.StartsWith("{") && certificate.EndsWith("}"))
        {
            certificate = certificate.Substring(1, certificate.Length - 2);
        }

        if (string.IsNullOrWhiteSpace(certificate))
        {
            return new List<string>();
        }

        return certificate.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    private bool CandidateVerticesExist(HashSet<string> allVertices, IEnumerable<string> candidate)
             => candidate.All(v => allVertices.Contains(v));

    private bool IsDominating(HashSet<string> allVertices, Dictionary<string, List<string>> adj, HashSet<string> D)
    {
        foreach (var u in allVertices)
        {
            if (D.Contains(u)) continue;
            if (!adj.TryGetValue(u, out var nbrs)) nbrs = new List<string>();
            bool dominated = nbrs.Any(D.Contains);
            if (!dominated) return false;
        }
        return true;
    }
  


    public bool verify(DOMINATINGSET problem, string certificate)
    {
        _certificate = certificate ?? string.Empty;

        var chosen = new HashSet<string>(ParseCertificate(_certificate));
        var adj = BuildAdjacencyList(problem.edges);
        var allV = GetAllVertices(problem, problem.edges);

        if (!CandidateVerticesExist(allV, chosen)) return false;
        if (!IsDominating(allV, adj, chosen)) return false;

        // Enforce |D| <= K (your DOMINATINGSET exposes K)
        if (chosen.Count > problem.K) return false;

        return true;
    }


}
