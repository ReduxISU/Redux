using SPADE;

namespace API.Interfaces.JSON_Objects;

class API_SET : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "set";

    public API_UtilCollection data { get; }
    public API_SET(UtilCollection uc) {
        data = new(uc);
    }

    /// <summary>
    /// The set of <paramref name="elements"/> (as the instance writes them, in order) with every repeat drawn (#628): the
    /// elements of a multiset such as Partition's numbers. An element keeps the id the SPADE set gives it (<c>r-12</c>); a
    /// second or later copy also gets its position (<c>r-12-5</c> for the copy at position 5), so every copy has its own id
    /// and no id changes when elements are added after it. The list holds one entry per element, in the given order.
    /// </summary>
    public static API_SET KeepingRepeats(IReadOnlyList<string> elements) {
        API_SET set = new API_SET(new UtilCollection("{" + string.Join(",", elements) + "}"));
        List<API_UtilCollection>? list = set.data.list;
        if (list == null) return set;
        var first = new Dictionary<string, API_UtilCollection>();
        foreach (API_UtilCollection e in list) first.TryAdd(e.id.Substring(set.data.id.Length + 1), e);
        var seen = new HashSet<string>();
        var copies = new List<API_UtilCollection>();
        for (int i = 0; i < elements.Count; i++) {
            if (!first.TryGetValue(elements[i], out API_UtilCollection? e)) return set;
            copies.Add(seen.Add(elements[i]) ? e : new API_UtilCollection(new UtilCollection(elements[i]), $"{e.id}-{i}"));
        }
        list.Clear();
        list.AddRange(copies);
        return set;
    }
}

