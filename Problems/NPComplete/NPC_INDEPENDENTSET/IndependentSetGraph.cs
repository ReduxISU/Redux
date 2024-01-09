using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_INDEPENDENTSET;

class IndependentSetGraph : UnweightedUndirectedGraph
{

   /// <summary>
 /// Takes a String and creates a VertexCoverGraph from it
 /// NOTE: DEPRECATED format, ex: {{a,b,c} : {{a,b} &amp; {b,c}} : 1}
 /// </summary>
 /// <param name="independentSetInput"> string input</param>
  public IndependentSetGraph(string independentSetInput) : base (independentSetInput){
        
    }

    //Constructor for standard graph formatted string input.
     /// <summary>
     /// </summary>
     /// <param name="independentSetInput"> Undirected Graph string input
     /// ex. {{1,2,3},{{1,2},{2,3}},0}
     /// </param>
     /// <param name="decoy"></param>
    public IndependentSetGraph(string independentSetInput, bool decoy) : base (independentSetInput, decoy){
    

    }

}