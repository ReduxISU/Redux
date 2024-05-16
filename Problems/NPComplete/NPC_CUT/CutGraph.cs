using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_CUT;

class CutGraph : UnweightedUndirectedGraph
{

   /// <summary>
 /// Takes a String and creates a VertexCoverGraph from it
 /// NOTE: DEPRECATED format, ex: {{a,b,c} : {{a,b} &amp; {b,c}} : 1}
 /// </summary>
 /// <param name="cutInput"> string input</param>
  public CutGraph(string cutInput) : base (cutInput){
        
    }

    //Constructor for standard graph formatted string input.
     /// <summary>
     /// 
     /// </summary>
     /// <param name="cutInput"> Undirected Graph string input
     /// ex. {{1,2,3},{{1,2},{2,3}},0}
     /// </param>
     /// <param name="decoy"></param>
    public CutGraph(string cutInput, bool decoy) : base (cutInput, decoy){
    

    }


}