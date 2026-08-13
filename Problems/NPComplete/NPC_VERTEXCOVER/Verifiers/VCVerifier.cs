using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;

class VCVerifier : IVerifier<VERTEXCOVER> {

    // --- Fields ---
    public string verifierName { get; } = "Vertex Cover Verifier";
    public string verifierDefinition { get; } = "This is a Vertex Cover Verifier.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Janita Aamir", "Alex Diviney" };

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = null;

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public VCVerifier() {

    }

    /// <summary>
    /// This Method Verifies whether a passed in Vertexcover (problem) is covered by the set of nodes (c). 
    /// </summary>
    /// <param name="problem"></param>
    /// <param name="certificate"></param>
    /// <returns></returns>
    public bool verify(VERTEXCOVER problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate)) {
            throw new CertificateParseException(problem, certificate, "certificate is empty");
        }

        List<string> certificateNodes;
        try {
            certificateNodes = getNodes(certificate);
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }
        if (certificateNodes.Count == 0 || certificateNodes.Any(string.IsNullOrWhiteSpace)) {
            throw new CertificateParseException(problem, certificate,
                "certificate did not parse to a non-empty list of node names");
        }
        List<string> GNodes = problem.nodes;
        List<KeyValuePair<string, string>> Gedges = problem.edges;

        //Step one of the verify method. Check if the input graph contains all the nodes in the certificate. If not, reject.
        foreach (string cNode in certificateNodes) {
            if (!GNodes.Contains(cNode)) {
                return false; //reject
            }
        }

        //Step two of the verify method. Test whether the set of all edges incident to nodes in c equals the set of edges in G
        //A node being incident to an edge means that that edge has the node as one of its two endpoints.

        //To test incidence, we will ask the graph if it has any edges that don't have an endpoint contained in the certificate set.
        foreach (KeyValuePair<string, string> kvp in Gedges) {
            if (!certificateNodes.Contains(kvp.Key) && !certificateNodes.Contains(kvp.Value)) { //if a kvp doesnt have a key or value found in the nodeset
                return false; //reject
            }
        }
        return true;

    }

    public List<string> getNodes(string nodesInput) {
        return GraphParser.parseNodeListWithStringFunctions(nodesInput);
    }
}