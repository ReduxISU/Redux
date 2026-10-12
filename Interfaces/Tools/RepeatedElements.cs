using SPADE;

namespace API.Interfaces;

/// <summary>
/// Reads the numbers (or items) of a set written in an instance or certificate string with every repeat kept, in the order
/// they were written (#628).
/// <para>
/// Partition, Subset Sum and Knapsack are defined on a multiset, but SPADE 0.1.2 has no multisets: a grammar such as
/// <c>{N | N subset int}</c> keeps one copy of each element, so <c>{3,3,2,2}</c> would be read as <c>{3,2}</c>. Their
/// constructors still let SPADE check the instance (shape, integers, error messages) exactly as before, then call
/// <see cref="KeepRepeats"/> to read the elements again from the text, one per occurrence. Each element returned is
/// SPADE's own element object for that text, so only the number of copies changes, never how an element reads.
/// </para>
/// <para>
/// TODO(#628): once SPADE releases <c>is multiset</c> (Jetison333/SPADE#17), the grammar changes listed in #628 replace this
/// Redux-side read (<c>N is multiset</c> for Partition, <c>S is multiset</c> for Subset Sum and its certificate <c>K</c>,
/// <c>i is multiset</c> for Knapsack), and this class can go.
/// </para>
/// </summary>
internal static class RepeatedElements {

    /// <summary>
    /// The elements of the first set in braces in <paramref name="text"/>, one per occurrence and in written order, as the
    /// elements of <paramref name="parsedSet"/> (the same set as SPADE read it, repeats dropped). Elements are split at the
    /// set's top-level commas; an empty piece (<c>{1,,2}</c>) is skipped, as SPADE skips it. If the text cannot be matched
    /// element for element with what SPADE read, SPADE's elements are returned as they are, so nothing SPADE accepts is
    /// ever refused or read differently here.
    /// </summary>
    public static List<UtilCollection> KeepRepeats(string text, UtilCollection parsedSet) {
        return Match(FirstSetElements(text), parsedSet);
    }

    /// <summary>
    /// The elements of the collection <paramref name="text"/> is (an answer such as <c>{7,7}</c> or <c>{(1,2),(1,2)}</c>),
    /// read as <c>new UtilCollection(text)</c> reads it but with every repeat kept, in written order. Throws what
    /// <c>new UtilCollection(text)</c> throws, so text it refuses is still refused.
    /// </summary>
    public static List<UtilCollection> Parse(string text) => Match(CollectionElements(text), new UtilCollection(text));

    // The elements of parsedSet, one per written text, when the written texts match its elements; else its elements as they are.
    private static List<UtilCollection> Match(List<string>? written, UtilCollection parsedSet) {
        // A copy: UtilCollection.ToList() hands back the collection's own list.
        List<UtilCollection> distinct = new List<UtilCollection>(parsedSet.ToList());
        if (written == null) return distinct;

        // SPADE keeps the first copy of each element text, so the distinct texts written must be exactly its elements.
        var byText = new Dictionary<string, UtilCollection>();
        foreach (UtilCollection element in distinct) {
            if (!byText.TryAdd(element.ToString(), element)) return distinct;
        }
        if (written.Distinct().Count() != byText.Count || written.Any(t => !byText.ContainsKey(t))) return distinct;
        return written.Select(t => byText[t]).ToList();
    }

    /// <summary>
    /// The texts of the elements of the first <c>{...}</c> in <paramref name="text"/>, split at its top-level commas, with
    /// empty pieces skipped and whitespace kept. Null if there is no balanced set in braces.
    /// </summary>
    public static List<string>? FirstSetElements(string text) {
        int open = text.IndexOf('{');
        if (open < 0) return null;
        int close = MatchingClose(text, open);
        if (close < 0) return null;
        return SplitTopLevel(text.Substring(open + 1, close - open - 1));
    }

    /// <summary>
    /// The texts of the elements of <paramref name="text"/> when, apart from surrounding whitespace, it is one collection in
    /// (), {} or [] (<c>(1,2,2)</c> gives 1, 2, 2), split at its top-level commas with empty pieces skipped and whitespace
    /// kept. Null if it is not one balanced collection.
    /// </summary>
    public static List<string>? CollectionElements(string text) {
        string t = text.Trim();
        if (t.Length < 2 || t[0] is not ('(' or '{' or '[')) return null;
        if (MatchingClose(t, 0) != t.Length - 1) return null;
        return SplitTopLevel(t.Substring(1, t.Length - 2));
    }

    /// <summary>
    /// <paramref name="inner"/> split at its commas that are not inside (), {} or []; empty pieces are skipped and
    /// whitespace is kept. Null if the brackets do not balance.
    /// </summary>
    public static List<string>? SplitTopLevel(string inner) {
        var pieces = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < inner.Length; i++) {
            char c = inner[i];
            if (c is '(' or '{' or '[') {
                depth++;
            } else if (c is ')' or '}' or ']') {
                if (--depth < 0) return null;
            } else if (c == ',' && depth == 0) {
                if (i > start) pieces.Add(inner.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (depth != 0) return null;
        if (inner.Length > start) pieces.Add(inner.Substring(start));
        return pieces;
    }

    // The index of the bracket that closes the one at open, or -1.
    private static int MatchingClose(string text, int open) {
        int depth = 0;
        for (int i = open; i < text.Length; i++) {
            char c = text[i];
            if (c is '(' or '{' or '[') {
                depth++;
            } else if (c is ')' or '}' or ']') {
                if (--depth == 0) return i;
                if (depth < 0) return -1;
            }
        }
        return -1;
    }
}
