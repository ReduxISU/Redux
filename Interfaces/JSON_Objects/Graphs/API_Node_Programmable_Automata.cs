//This API Node is used when we need to inject additional attributes into a node for a visualization request for Automatas.
//Modified by Andrija Sevaljevic to include initial and accept state information for DFA visualizations -- 4/16/2026

namespace API.Interfaces.JSON_Objects.Graphs;

class API_Node_Programmable_Automata : API_Node_Programmable_Small
{
    private string _initial;
    private string _accept_state;

    public API_Node_Programmable_Automata() : base()
    {
        _initial = "";
        _accept_state = "";
    }

    public API_Node_Programmable_Automata(string nm, string color = "", string outline = "", string delay = "", string dashed = "", string initial = "", string accept_state = "", string additional = "")
        : base(nm, color, outline, delay, dashed, additional)
    {
        _initial = initial;
        _accept_state = accept_state;
    }

    public string initial
    {
        get => _initial;
        set => _initial = value;
    }

    public string accept_state
    {
        get => _accept_state;
        set => _accept_state = value;
    }
}