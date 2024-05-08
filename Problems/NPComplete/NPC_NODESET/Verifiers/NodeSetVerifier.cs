using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_NODESET.Verifiers;

class NodeSetVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Node Set Verifier";
    private string _verifierDefinition = "This is a verifier for the Node Set problem";
    private string _source = "";
    private string[] _contributors = {"Andrija Sevaljevic"};


    private string _certificate =  "";

    // --- Properties ---
    public string verifierName {
        get {
            return _verifierName;
        }
    }
    public string verifierDefinition {
        get {
            return _verifierDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
       public string[] contributors{
        get{
            return _contributors;
        }
    }

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public NodeSetVerifier() {
        
    }
    
    public string toEdges(string certificate, NODESET problem) {
        List<string> nodes = certificate.Replace("{","").Replace("}","").Split(',').ToList();
        string certificateEdges = "{";
        foreach(var i in problem.edges) {
            if(nodes.Contains(i.Key) || nodes.Contains(i.Value)) {
                certificateEdges += "(" + i.Key + ',' + i.Value + "),";
            }
        }
        return certificateEdges.TrimEnd(',') + '}';
    }

    public bool verify(NODESET problem, string certificate){
        
        NodeSetGraph graph = problem.nodeSetAsGraph; 
        string userInput = toEdges(certificate, problem);

        //Checks if certificate matches k-value;
        graph.processCertificate(userInput);
        bool isACyclical = true;
        for(int i=0; i<graph.getNodeList.Count; i++){
            if(graph.isCyclical(i)){
                isACyclical = false;
            }
        }
        graph.reverseCertificate(userInput);
        //when userInput is removed from graph is it no longer Cyclical? 
        return isACyclical;
    }
}