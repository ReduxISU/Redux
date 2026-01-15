//Edge.cs
//This class is used for the creation of Edge objects that can then be used to create a graph. It is composed of two Nodes.


using System;
using System.Collections.Generic;
namespace API.Interfaces.Graphs;
class WeightedEdge : IComparable<WeightedEdge>{

    //Fields
    private Node _source;
    private Node _target;
    private int _weight;

    // Added Fields For DFA Case -- Michael Trosper 1/13/2026 //
    private string _from;
    private string _to;
    private string _value;

    public WeightedEdge()
    {
        _source = new Node();
        _target = new Node();
    }

    // Added Constructor Accepting String Weight -- Michael Trosper 1/13/2026 //
    public WeightedEdge(string from, string to, string weight)
    {
        _from = from;
        _to = to;
        _value = weight;
    }

    public WeightedEdge(Node n1, Node n2, int weight) {
    _source = n1;
    _target = n2;
    _weight = weight;
}

    public Node source
    {
        get
        {
            return _source;
        }
        set
        {
            _source = value;
        }
    }

    public Node target
    {
        get
        {
            return _target;
        }
        set
        {
            _target = value;
        }
    }

    public int weight
    {
        get
        {
            return _weight;
        }
        set
        {
            _weight = value;
        }
    }

    // Added DFA Field Getters and Setters -- Michael Trosper 1/13/2026 //
    public string from
    {
        get
        {
            return _from;
        }
        set
        {
            _from = value;
        }
    }

    public string to
    {
        get
        {
            return _to;
        }
        set
        {
            _to = value;
        }
    }

    public string value
    {
        get
        {
            return _value;
        }
        set
        {
            _value = value;
        }
    }

    public override string ToString(){
return source.name+","+_target.name;
}

public string undirectedString(){
    return "{"+source.name+","+_target.name+"}";
}
public string directedString(){
    return "("+source.name+","+_target.name+")";
}
public int CompareTo(WeightedEdge? other)
    {
        if (other is null) {
            // Convention: non-null > null
            return 1;
        }

        int compare =  this.toKVP().Key.CompareTo(other.toKVP().Key);
        if(compare == 0){
            compare = this.toKVP().Value.CompareTo(other.toKVP().Value);
        }
        return compare;
    }
public KeyValuePair<string,string> toKVP(){
    KeyValuePair<string,string> asKVP = new KeyValuePair<string, string>(source.name,target.name);
    return asKVP;
}

}