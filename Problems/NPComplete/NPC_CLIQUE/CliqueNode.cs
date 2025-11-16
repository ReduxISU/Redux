using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_CLIQUE;

class CliqueNode : Node
{
 protected string _cluster;

    public CliqueNode():base(){
        _cluster = "0";
    }
    public CliqueNode(string name, string cluster){
        this._name = name;
        this._cluster = cluster;

    }

public string cluster{
    get{
            return _cluster;
        }
}
}