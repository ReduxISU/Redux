
namespace API.Interfaces.JSON_Objects;

/// <summary>
/// Marker interface for the JSON payload shapes returned by visualizations
/// (e.g. <see cref="Graphs.API_GraphJSON"/>, <see cref="API_SAT"/>, <see cref="API_SET"/>,
/// <see cref="API_QUANTUMCIRCUIT"/>, <see cref="Gadget"/>, and <see cref="API_empty"/>).
/// It carries no members; its purpose is to give these otherwise-unrelated types a
/// common base so they can be handled polymorphically — held in a single
/// <c>List&lt;API_JSON&gt;</c> and serialized through <see cref="API.Interfaces.Tools.API_JSON_Converter"/>.
/// </summary>
public interface API_JSON
{

}