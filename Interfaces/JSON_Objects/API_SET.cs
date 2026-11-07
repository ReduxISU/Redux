using API.Interfaces.Graphs;
using API.Interfaces.Packing;
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

    /// <summary>
    /// Renames the elements of a list such as <see cref="KeepingRepeats"/> draws to the Packing picture's item ids, by position
    /// (<c>I1</c>, <c>I2</c>, ...; what is inside an element follows it: <c>I1-0</c>), the ids the gadgets of reductions into
    /// and out of a numbers problem name. Call it after colouring by the old ids.
    /// </summary>
    public API_SET UseItemIds() {
        List<API_UtilCollection> items = data.list ?? [];
        for (int i = 0; i < items.Count; i++) items[i].Rename(PackingFrames.ItemId(i));
        return this;
    }

    /// <summary>
    /// Renames the pieces of a set system drawn from its instance (a tuple whose part <paramref name="elementsAt"/> is the
    /// universe and part <paramref name="setsAt"/> the collection of sets) to the set problems' piece ids (<see cref="SetPieces"/>), the ids the
    /// gadgets of reductions into and out of the problem name: an element of the universe is <c>e:{name}</c>, the set at
    /// position j of <paramref name="sets"/> is <c>S{j+1}</c>, and an element inside a set is the membership edge
    /// <c>e:{name}~S{j+1}</c>. A set is matched by its members, so the order the instance tree lists them in does not
    /// matter; two sets with the same members are one piece of the tree, and it takes the first one's id. Call it after
    /// colouring by the old ids.
    /// </summary>
    public API_SET UseBipartiteIds(IReadOnlyList<IReadOnlyList<string>> sets, int elementsAt = 0, int setsAt = 1) {
        List<API_UtilCollection> parts = data.list ?? [];
        if (parts.Count <= Math.Max(elementsAt, setsAt)) return this;
        foreach (API_UtilCollection element in parts[elementsAt].list ?? [])
            if (element.value != null) element.Rename(SetPieces.ElementId(element.value));
        var taken = new HashSet<int>();
        foreach (API_UtilCollection set in parts[setsAt].list ?? []) {
            var members = (set.list ?? []).Select(m => m.value ?? m.id).ToHashSet(StringComparer.Ordinal);
            int j = Enumerable.Range(0, sets.Count).FirstOrDefault(j => !taken.Contains(j) && members.SetEquals(sets[j]), -1);
            if (j < 0) continue;
            taken.Add(j);
            string setId = SetPieces.SetId(j);
            set.Rename(setId);
            foreach (API_UtilCollection member in set.list ?? [])
                if (member.value != null) member.Rename(SetPieces.EdgeId(SetPieces.ElementId(member.value), setId));
        }
        return this;
    }
}

