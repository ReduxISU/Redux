namespace API.Interfaces.Steps;

/// <summary>Small helpers for the plain-sentence captions of construction steps (<see cref="BuildDelta"/>).</summary>
static class BuildCaptions {
    /// <summary>"a, b, c", or "a, b, c, d, e, f and 4 more" past <paramref name="max"/> names, so captions stay one readable sentence.</summary>
    public static string Names(IReadOnlyCollection<string> names, int max = 6) =>
        names.Count <= max
            ? string.Join(", ", names)
            : string.Join(", ", names.Take(max)) + $" and {names.Count - max} more";

    /// <summary>"1 edge", "3 edges".</summary>
    public static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

    /// <summary>The id of an edge between two nodes, "a-b" (the convention of the graph pictures).</summary>
    public static string Edge(string a, string b) => a + "-" + b;
}
