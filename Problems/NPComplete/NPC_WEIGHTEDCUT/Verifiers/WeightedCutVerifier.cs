using System.Collections;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using Microsoft.AspNetCore.Localization;
using SPADE;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;

class WeightedCutVerifier : IVerifier<WEIGHTEDCUT> {

    public const string CertificateGrammar = "{S | S subset E, S is exactly the set of edges crossing some partition of N into two sets, sum of edge weights in S = K}";
    public const string CertificateExample = "{({2,1},5)}";

    // --- Fields ---
    public string verifierName { get; } = "Default Weighted Cut Verifier";
    public string verifierDefinition { get; } = "Checks that the certificate edges all exist in G with the stated weights, are distinct, are exactly the edges crossing some partition of the vertices into two sets, and have total weight exactly K.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public WeightedCutVerifier() { }

    public bool verify(WEIGHTEDCUT problem, string certificate) {
        if (certificate == "{}") {
            return false;
        }

        List<(string, string)> certEdges = new List<(string, string)>();
        long total = 0;
        try {
            UtilCollection edgeList = new(certificate);
            foreach (UtilCollection i in edgeList) {
                List<UtilCollection> cast = i[0].ToList();
                if (cast.Count != 2 || !int.TryParse(i[1].ToString(), out int weight)) {
                    return false;
                }
                string source = cast[0].ToString();
                string destination = cast[1].ToString();
                // The edge must exist with exactly this weight.
                if (!problem.edges.Contains((source, destination, weight)) && !problem.edges.Contains((destination, source, weight))) {
                    return false;
                }
                certEdges.Add((source, destination));
                total += weight;
            }
        } catch {
            return false;
        }

        List<(string, string)> graphEdges = problem.edges.Select(e => (e.source, e.destination)).ToList();
        if (!GraphCertificateChecks.EdgesExistAndDistinct(graphEdges, certEdges)) {
            return false;
        }
        // The certificate must be exactly the set of edges crossing some partition (S, V\S).
        if (!GraphCertificateChecks.IsExactCut(problem.nodes, graphEdges, certEdges)) {
            return false;
        }
        return total == problem.K;
    }
}
