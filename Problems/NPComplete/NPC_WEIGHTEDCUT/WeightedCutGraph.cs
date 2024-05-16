using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT;

class WeightedCutGraph : WeightedUndirectedGraph
{

   /// <summary>
 /// Takes a String and creates a VertexCoverGraph from it
 /// NOTE: DEPRECATED format, ex: {{a,b,c} : {{a,b} &amp; {b,c}} : 1}
 /// </summary>
 /// <param name="cutInput"> string input</param>
  public WeightedCutGraph(string cutInput) : base (cutInput){
        
    }

    public WeightedCutGraph(string cutInput, bool decoy) : base (cutInput, decoy){
    

    }
}