
using System;
using System.Collections.Generic;
namespace API.Interfaces.Graphs;

class Node : ICloneable {

    //Fields
    protected string _name;

    //Constructors
    public Node() {
        _name = "DEFAULT";


    }
    public Node(string nm) {
        _name = nm;
    }

    //getters and setters
    public string name {
        get {
            return _name;
        }
        set {
            _name = value;
        }
    }



    public override string ToString() {

        return _name;
    }

    public object Clone() {
        Node clonedNode = new Node(this._name);


        return clonedNode;
    }

}