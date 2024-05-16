using API.Interfaces;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_ARCSET.Verifiers;


class ArcSetVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Arc Set Verifier";
    private string _verifierDefinition =  @"This Verifier takes in an arcset problem and a list of edges to remove from that problem. It removes those edges and then checks if the problem is still an instance of ARCSET
                                            ie. Does this input graph no longer have cycles after these input edges are removed? Returns true or false ";
    
    private string _source = "This verifier is essentially common knowledge, as it utilizes a widely recognized algorithm in computer science: The Depth First Search.";

    private string _certificate = "{(2,4)}"; //The certificate should be in the form of a set of directed edges
    private string[] _contributors = {"Alex Diviney","Caleb Eardley"};

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



    public ArcSetVerifier(){

    }

    /**
    * This method should take in an arcset problem and a list of edges to remove from that problem. It removes those edges and then checks if the problem is still an instance of ARCSET
    * ie. Does this input graph continue to have cycles after these input edges are removed? 
    **/
    public Boolean verify(ARCSET problem, string userInput){

        ArcsetGraph graph = problem.directedGraph; 

        //Checks if certificate matches k-value;
        if(graph.cerfitficateLength(userInput) > graph.K){
            return false;
        }
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