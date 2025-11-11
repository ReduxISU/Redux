//This API link is needed for object destructuring by d3. Normally graphs in Redux have link attributes that point to node objects.
//However, in serializing links (edges) for the use of d3, we need to point links to purely the names of the nodes, not the nodes themselves
//Author: Alex Diviney

namespace API.Interfaces.JSON_Objects.Graphs;

class API_Link
{
    private string _source;
    private string _target;
    private string _attribute1;
    private string _attribute2;
    private string _color;
    private string _dashed;
    private string _delay;
    private string _weight;
    private bool _directed;
    public API_Link()
    {
        this._source = "DEFAULTSOURCE";
        this._target = "DEFAULTTARGET";
        this._attribute1 = "";
        this._attribute2 = "";
        this._color = "";
        this._dashed = "";
        this._delay = "";
        this._weight = "1";
        this._directed = false;

    }

    public API_Link(string s, string t, string a1 = "", string a2 = "", string color = "", string dashed = "", string delay = "", string weight = "1", bool directed = false)
    {
        _source = s;
        _target = t;
        _attribute1 = a1;
        _attribute2 = a2;
        _color = color;
        _dashed = dashed;
        _delay = delay;
        _weight = weight;
        _directed = directed;
    }

    public string source
    {
        get
        {
            return _source;
        }
    }
    public string target
    {
        get
        {
            return _target;
        }
    }
    public string attribute1
    {
        get
        {
            return _attribute1;
        }
        set
        {
            _attribute1 = value;
        }
    }
    public string attribute2
    {
        get
        {
            return _attribute2;
        }
        set
        {
            _attribute2 = value;
        }
    }

    public string color
    {
        get
        {
            return _color;
        }
        set
        {
            _color = value;
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

    public string delay
    {
        get
        {
            return _delay;
        }
        set
        {
            _delay = value;
        }
    }
    
    public string weight
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

    public bool directed
    {
        get
        {
            return _directed;
        }
        set
        {
            _directed = value;
        }
    }

    public string id
    {
        get
        {
            return source + "-" + target;
        }
    }
    
}