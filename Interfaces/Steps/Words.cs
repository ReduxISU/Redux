namespace API.Interfaces.Steps;

/// <summary>Small words shared by step captions and notes, so every picture says them the same way.</summary>
static class Words {
    /// <summary>"1 clause", "2 clauses": a count with its noun, plural when it is not 1 (the noun takes an s).</summary>
    public static string Count(int n, string noun) => $"{n} {(n == 1 ? noun : noun + "s")}";
}
