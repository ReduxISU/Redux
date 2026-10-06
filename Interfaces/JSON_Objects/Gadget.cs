using System.Drawing;
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.JSON_Objects;

class Gadget : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "gadget";

    public string color { get; set; }
    public List<string> reductionFromIds { get; set; }
    public List<string> reductionToIds { get; set; }

    public Gadget(string col, List<string> from, List<string> to) {
        color = col;
        reductionFromIds = from;
        reductionToIds = to;
    }
}