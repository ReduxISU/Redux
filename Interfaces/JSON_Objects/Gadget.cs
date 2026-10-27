using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.JSON_Objects;

class Gadget : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "gadget";

    // Legacy trio read by Redux_GUI (it checks color === "ElementHighlight"); keep these three
    // byte-for-byte stable. New clients should read gadgetKind / sourceIds / targetIds instead.
    public string color { get; set; }
    public List<string> reductionFromIds { get; set; }
    public List<string> reductionToIds { get; set; }

    /// <summary>What this gadget is, from the shared <see cref="GadgetKind"/> list. (Named gadgetKind because <see cref="kind"/> is the payload discriminator.)</summary>
    public GadgetKind gadgetKind { get; set; }

    /// <summary>
    /// Stable ids of the source (A) pieces, the ids A's own picture payload uses, so a Reduction
    /// View can match a gadget to a drawn element without remapping. Graph problems: node name
    /// (edges "a-b"). SAT / 3SAT formulas: clause i is "c{i}", its j-th literal "c{i}-{j}",
    /// variable v is "v:{name}". No "s:"/"t:" prefix: the Reduction View payload carries source
    /// and target separately, so the side is implicit.
    /// </summary>
    public List<string> sourceIds { get; set; }

    /// <summary>Stable ids of the target (B) pieces; same convention as <see cref="sourceIds"/>.</summary>
    public List<string> targetIds { get; set; }

    /// <summary>Optional plain-language caption (e.g. "K = 3" on a bound); omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? note { get; set; }

    /// <summary>Legacy constructor: the kind is derived from the color and the legacy ids double as the stable ids.</summary>
    public Gadget(string col, List<string> from, List<string> to) {
        color = col;
        reductionFromIds = from;
        reductionToIds = to;
        gadgetKind = col == "ClauseHighlight" ? GadgetKind.Group : GadgetKind.Element;
        // Copies, not aliases: a reduction may edit the legacy lists after construction.
        sourceIds = new List<string>(from);
        targetIds = new List<string>(to);
    }

    /// <param name="gadgetKind">What the gadget is.</param>
    /// <param name="sourceIds">Stable ids of the source pieces.</param>
    /// <param name="targetIds">Stable ids of the target pieces.</param>
    /// <param name="legacyFromIds">What Redux_GUI reads as reductionFromIds; defaults to <paramref name="sourceIds"/>.</param>
    /// <param name="legacyToIds">What Redux_GUI reads as reductionToIds; defaults to <paramref name="targetIds"/>.</param>
    /// <param name="note">Optional plain-language caption.</param>
    public Gadget(GadgetKind gadgetKind, List<string> sourceIds, List<string> targetIds,
                  List<string>? legacyFromIds = null, List<string>? legacyToIds = null, string? note = null) {
        this.gadgetKind = gadgetKind;
        this.sourceIds = sourceIds;
        this.targetIds = targetIds;
        // Group is the only kind the legacy GUI draws differently; everything else looks like an element.
        color = gadgetKind == GadgetKind.Group ? "ClauseHighlight" : "ElementHighlight";
        reductionFromIds = legacyFromIds ?? new List<string>(sourceIds);
        reductionToIds = legacyToIds ?? new List<string>(targetIds);
        this.note = note;
    }
}
