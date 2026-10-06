
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.JSON_Objects;

class API_empty : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "empty";
}