using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_TSP;

class TSPNode : Node
{
 protected string _cluster;

    public TSPNode():base(){
        _cluster = "0";
    }
    public TSPNode(string name, string cluster){
        this._name = name;
        this._cluster = cluster;

    }

public string cluster{
    get{
            return _cluster;
        }
}
}