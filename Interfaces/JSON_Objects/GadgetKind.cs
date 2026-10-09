using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Interfaces.JSON_Objects;

/// <summary>
/// The one shared list of what a gadget can be, so every reduction describes its construction in
/// the same words and a Reduction View can style each kind once. Serialized as camelCase strings
/// ("element", "edgeRule", "orGadget", ...). Legacy clients never see this: they read
/// <see cref="Gadget.color"/>, which each kind maps onto (see the <see cref="Gadget"/> constructors).
/// </summary>
[JsonConverter(typeof(GadgetKindConverter))]
public enum GadgetKind {
    /// <summary>One piece of A becomes one piece of B (legacy color <c>ElementHighlight</c>).</summary>
    Element,
    /// <summary>One part of A becomes a group of pieces in B (legacy color <c>ClauseHighlight</c>).</summary>
    Group,
    /// <summary>A rule that decides which pairs of B get an edge.</summary>
    EdgeRule,
    /// <summary>Fixed helper pieces every instance of B gets.</summary>
    Palette,
    /// <summary>A small structure that only works if a clause has a true literal.</summary>
    OrGadget,
    /// <summary>How B's number K is computed from A.</summary>
    Bound,
}

// JsonStringEnumConverter<T> takes its naming policy through the constructor, which the
// [JsonConverter] attribute cannot supply, hence this one-line subclass.
/// <summary>Writes <see cref="GadgetKind"/> as a camelCase string.</summary>
public sealed class GadgetKindConverter : JsonStringEnumConverter<GadgetKind> {
    /// <summary>Creates the converter used by the <see cref="GadgetKind"/> attribute.</summary>
    public GadgetKindConverter() : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}
