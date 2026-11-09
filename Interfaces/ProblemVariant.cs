using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// Whether a problem asks a yes/no question or for a best answer (#616). A tag set like
/// <see cref="ProblemType"/> (see <see cref="Tags"/>): a problem declares every variant its class supports.
/// Declared, not derived: read the class, its verifier and its solver, then declare it. The API writes it
/// only as the list <c>problemVariants</c>; there is no single <c>problemVariant</c> field.
/// </summary>
[Flags]
[JsonConverter(typeof(TagJsonConverter<ProblemVariant>))]
public enum ProblemVariant {
    /// <summary>No variant has been declared for this problem yet.</summary>
    Unclassified = 0,
    /// <summary>
    /// Asks a yes/no question, usually against a bound the instance carries ("is there a vertex cover of
    /// size at most k?"), and the verifier checks a certificate against that bound.
    /// </summary>
    Decision = 1 << 0,
    /// <summary>Asks for the best value or object ("find the smallest vertex cover"), with no bound in the instance.</summary>
    Optimization = 1 << 1,
}
