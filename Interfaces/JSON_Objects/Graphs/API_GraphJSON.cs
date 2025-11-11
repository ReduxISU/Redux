using System.Collections.Generic;
using System.Collections;
using API.Interfaces.Graphs;
namespace API.Interfaces.JSON_Objects.Graphs;

class API_GraphJSON : API_JSON
{

    public List<API_Node_Programmable_Small> _nodes;
    public List<API_Link> _links;

    public API_GraphJSON()
    {
        this._nodes = new List<API_Node_Programmable_Small>();
        this._nodes.Add(new API_Node_Programmable_Small("DEFAULTNODE"));
        this._links = new List<API_Link>();
        this._links.Add(new API_Link());
    }
    public API_GraphJSON(List<Node> nodes, List<Edge> inputEdges){
        // this._nodes = nodes;
        _nodes = new List<API_Node_Programmable_Small>();
        foreach(Node n in nodes){
            API_Node_Programmable_Small newNode = new API_Node_Programmable_Small(n.name);
            _nodes.Add(newNode);
        }

        _links = new List<API_Link>();
        foreach(Edge e in inputEdges){
            API_Link newLink = new API_Link(e.source.name,e.target.name); //destructures an object with a nested node into an object with straight name reference.
            _links.Add(newLink);
        }
    }

    public API_GraphJSON(List<string> nodes, List<KeyValuePair<string, string>> inputEdges)
    {
        _nodes = new List<API_Node_Programmable_Small>();
        foreach(string n in nodes){
            API_Node_Programmable_Small newNode = new API_Node_Programmable_Small(n);
            _nodes.Add(newNode);
        }

        _links = new List<API_Link>();
        foreach (KeyValuePair<string, string> e in inputEdges)
        {
            API_Link newLink = new API_Link(e.Key, e.Value); //destructures an object with a nested node into an object with straight name reference.
            _links.Add(newLink);
        }

    }


public List<API_Node_Programmable_Small> nodes
    {
        get
        {
            return _nodes;
        }
    }
public List<API_Link> links {
    get {
        return _links;
    }
}

}