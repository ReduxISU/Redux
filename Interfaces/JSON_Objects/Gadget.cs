using System.Drawing;
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.JSON_Objects;

class Gadget : API_JSON
{
    public string color { get; set; }
    public List<string> reductionFromIds { get; set; }
    public List<string> reductionToIds { get; set; }

    public Gadget(string col, List<string> from, List<string> to)
    {
        color = col;
        reductionFromIds = from;
        reductionToIds = to;
    }
}