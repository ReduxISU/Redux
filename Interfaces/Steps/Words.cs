using System.Globalization;
using System.Numerics;

namespace API.Interfaces.Steps;

/// <summary>
/// Small words shared by step captions and notes, so every picture says them the same way. Wording that is plain English
/// and names no piece of a picture belongs here (counts, numbers, lists, braces); a picture's piece ids stay with the
/// picture.
/// </summary>
static class Words {
    /// <summary>"1 clause", "2 clauses": a count with its noun, plural when it is not 1 (the noun takes an s).</summary>
    public static string Count(int n, string noun) => $"{n} {(n == 1 ? noun : noun + "s")}";

    /// <summary>"1 try", "2 tries": a count with its noun, for a noun whose plural is not the noun with an s.</summary>
    public static string Count(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    /// <summary>A number as captions write it, with thousands separators: "1,234".</summary>
    public static string Number(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A number too big for a long (a number to factor), written as <see cref="Number(long)"/> writes one.</summary>
    public static string Number(BigInteger n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"4", "4 and 5", "3, 4 and 5"; "" for no items.</summary>
    public static string List(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    /// <summary>
    /// As <see cref="List(IReadOnlyList{string})"/>, but past <paramref name="most"/> items only the first are named and
    /// the rest counted: "a, b, c and 12 more".
    /// </summary>
    public static string List(IReadOnlyList<string> items, int most) =>
        items.Count > most ? string.Join(", ", items.Take(most)) + $" and {items.Count - most} more" : List(items);

    /// <summary>"4", "4 or 5", "3, 4 or 5"; "" for no items.</summary>
    public static string OrList(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " or " + items[^1];

    /// <summary>"{1,2,3}": items in braces, with no spaces.</summary>
    public static string Braces(IEnumerable<string> items) => "{" + string.Join(",", items) + "}";
}
