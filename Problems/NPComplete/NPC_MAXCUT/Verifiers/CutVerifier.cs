using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_CUT.Verifiers;

class CutVerifier : IVerifier<CUT> {

    // --- Fields ---
    public string verifierName {get;} = "Cut Verifier";
    public string verifierDefinition {get;} = "This is a verifier for the Cut problem";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Andrija Sevaljevic"};


    private string _certificate =  "";

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public CutVerifier() {
        
    }
    private List<string> parseCertificate(string certificate){

        List<string> edgeList = certificate.Replace("{{", "").Replace("}}","").Replace(" ","").Split("},{").ToList();
        return edgeList;
    }
    public bool verify(CUT problem, string certificate){
        
        List<string> edgeList = parseCertificate(certificate); 
        int counter = 0;
        foreach(var i in edgeList){
            string invertedString = new string(i.ToCharArray().Reverse().ToArray());
            if (edgeList.Count(x => x == i) > 1 || edgeList.Contains(invertedString)) { //makes sure there are no duplicate edges
                return false;
            }
            List<string> currentEdge = i.Split(",").ToList();
            KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(currentEdge[0],currentEdge[1]);
            KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(currentEdge[1],currentEdge[0]);
            if ((problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)) && !currentEdge[1].Equals(currentEdge[0])) { //Checks if edge exists, then adds to cut
                counter++;
            }
            
        }
        if (counter != problem.K) {
            return false;
        }
        return true;
    }
}