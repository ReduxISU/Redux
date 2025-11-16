//This API Node is used when we need to inject additional attributes into a node for a visualization request.
// For example, a node class naturaly only has the name attribute, but a CLique node needs a name, and clique attribute, and a vertexcover node needs a name and
// cover attribute. Rather than build custom nodes for every graph object that have attributes that are only used in visualizing, we can build nodes that are
// in the correct json format opon serialization by having generic attributes.
//Author: Alex Diviney

namespace API.Interfaces.JSON_Objects.Graphs;

class API_Node_Programmable_Small{
    private string _name;
    private string _color;
    private string _outline;
    private string _delay;
    private string _dashed;

    public API_Node_Programmable_Small(){
        this._name = "APINODE";
        this._color = "";
        this._outline = "";
        this._delay = "";
        this._dashed = "";
    }

    public API_Node_Programmable_Small(string nm, string color="", string outline="", string delay="", string dashed="") {
        _name = nm;
        _color = color;
        _outline = outline;
        _delay = delay;
        _dashed = dashed;
    }
    
    public string name{
        get
        {
            return _name;
        }
    }
    public string color{
        get{
            return _color;
        }
        set{
            _color = value;
        }
    }

    public string outline{
        get{
            return _outline;
        }
        set{
            _outline = value;
        }
    }

    public string delay{
        get{
            return _delay;
        }
        set{
            _delay = value;
        }
    }

    public string dashed
    {
        get
        {
            return _dashed;
        }
        set
        {
            _dashed = value;
        }
    }
    
    public string id
    {
        get
        {
            return name;
        }
    }
    
}