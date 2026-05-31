using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;

class VCVerifier : IVerifier<VERTEXCOVER> {

    // --- Fields ---
    public string verifierName {get;} = "Vertex Cover Verifier";
    public string verifierDefinition {get;} = "This is a Vertex Cover Verifier.";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Janita Aamir","Alex Diviney"};

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
    public bool verify(VERTEXCOVER problem, string certificate){
        //{{a,b,c,d,e,f,g} : {(a,b) & (a,c) & (c,d) & (c,e) & (d,f) & (e,f) & (e,g)} : 3}
        //{{a,d,e} : {(a,b) & (a,c) & (c,d) & (c,e) & (d,f) & (e,f) & (e,g)} }
        List<string> certificateNodes = getNodes(certificate);
       // List<KeyValuePair<string, string>> edges = getEdges(c);
        List<string> GNodes = problem.nodes;
        List<KeyValuePair<string, string>> Gedges = problem.edges;


        //var list = nodes.Except(GNodes);

        // bool result1 = list?.Any() != true;

        // int count = 0;
        // foreach (KeyValuePair<string, string> edge in edges)
        // {
        //     foreach (KeyValuePair<string, string> Gedge in Gedges){
        //         if (edge.Key.Equals(Gedge.Key) && edge.Value.Equals(Gedge.Value)){
        //             count += 1;
        //         }
        //         if (edge.Key.Equals(Gedge.Value) && edge.Value.Equals(Gedge.Key)){
        //             count += 1; 
        //         }
        //     }                
        // }
        // int size = Gedges.Count;
        // bool result2 = false;
        // if (size == count){
        //     result2 = true;
        // }
    
        // return (result1 == true) && (result2 == true) ? true : false;

        //Step one of the verify method. Check if the input graph contains all the nodes in the certificate. If not, reject.
        foreach(string cNode in certificateNodes){
            if(!GNodes.Contains(cNode)){
                return false; //reject
            }
        }

        //Step two of the verify method. Test whether the set of all edges incident to nodes in c equals the set of edges in G
        //A node being incident to an edge means that that edge has the node as one of its two endpoints.
        
        //To test incidence, we will ask the graph if it has any edges that don't have an endpoint contained in the certificate set.
        foreach(KeyValuePair<string,string> kvp in Gedges){
            if(!certificateNodes.Contains(kvp.Key) && !certificateNodes.Contains(kvp.Value)){ //if a kvp doesnt have a key or value found in the nodeset
                return false; //reject
            }
           
        }
        return true;

    }

    public List<string> getNodes(string nodesInput) {
       return GraphParser.parseNodeListWithStringFunctions(nodesInput);

    }

    public List<KeyValuePair<string, string>> getEdges(string Ginput) {

        List<KeyValuePair<string, string>> allGEdges = new List<KeyValuePair<string, string>>();

        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        string[] Gedges = Gsections[1].Split('&');
        
        foreach (string edge in Gedges) {
            string[] fromTo = edge.Split(',');
            string nodeFrom = fromTo[0];
            string nodeTo = fromTo[1];
            
            KeyValuePair<string,string> fullEdge = new KeyValuePair<string,string>(nodeFrom, nodeTo);
            allGEdges.Add(fullEdge);
        }

        return allGEdges;
    }

    public int getK(string Ginput) {
        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        return Int32.Parse(Gsections[2]);
    }
}