using System.Text.Json.Serialization;
using SPADE;

namespace API.Interfaces.JSON_Objects;

class API_UtilCollection {
    public bool isOrdered { get; }
    public bool isValue { get; }
    public string id { get; private set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? value { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? color { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<API_UtilCollection>? list { get; }

    /// <summary>
    /// Gives this piece the id <paramref name="newId"/>, and every piece inside it the same new start in place of this
    /// piece's old id (<c>r-1-{1,2}-1</c> becomes <c>S1-1</c> when <c>r-1-{1,2}</c> becomes <c>S1</c>), so ids stay unique.
    /// </summary>
    internal void Rename(string newId) {
        string old = id;
        id = newId;
        foreach (API_UtilCollection child in list ?? [])
            child.Rename(newId + child.id[old.Length..]);
    }

    public API_UtilCollection(UtilCollection uc) : this(uc, "r") {

    }

    public API_UtilCollection(UtilCollection uc, string id) {
        isOrdered = uc.IsOrdered();
        isValue = uc.IsValue();
        this.id = id;

        if (uc.IsValue()) {
            value = uc.ToString();
            color = "Background";
        } else {
            list = new();
            if (uc.IsOrdered()) {
                for (int i = 0; i < uc.Count(); i++) {
                    list.Add(new API_UtilCollection(uc[i], id + "-" + i));
                }
            } else {
                foreach (UtilCollection u in uc) {
                    list.Add(new API_UtilCollection(u, id + "-" + u.ToString()));
                }
            }
        }
    }
}