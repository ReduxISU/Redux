using SPADE;

namespace API.Interfaces.JSON_Objects;

class API_SET : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "set";

    public API_UtilCollection data { get; }
    public API_SET(UtilCollection uc) {
        data = new(uc);
    }
}

