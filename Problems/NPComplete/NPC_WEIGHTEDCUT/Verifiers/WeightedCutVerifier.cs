using System.Collections;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using Microsoft.AspNetCore.Localization;
using SPADE;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;

class WeightedCutVerifier : IVerifier<WEIGHTEDCUT> {

    // --- Fields ---
    public string verifierName {get;} = "Weighted Cut Verifier";
    public string verifierDefinition {get;} = "This is a verifier for the Weighted Cut problem";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Andrija Sevaljevic"};


    private string _certificate =  "";

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public WeightedCutVerifier() { }
        
    public bool verify(WEIGHTEDCUT problem, string certificate){

        if(certificate == "{}") {
            return false;
        }

        UtilCollection edgeList = new(certificate);
        int counter = 0;
        foreach(UtilCollection i in edgeList){
            List<UtilCollection> cast = i[0].ToList();
            string source = cast[0].ToString();
            string destination = cast[1].ToString();
            int weight = Int32.Parse(i[1].ToString());
            if ((problem.edges.Contains((source,destination,weight)) || problem.edges.Contains((destination,source,weight))) && !cast[0].Equals(cast[1])) { //Checks if edge exists, then adds to cut
                counter += weight;
            }
            
        }
        if (counter != problem.K) {
            return false;
        }
        return true;
    }
}