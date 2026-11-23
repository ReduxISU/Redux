namespace API.Interfaces.Steps;

/// <summary>
/// The shared half of a picture spec's <c>Validate</c>: it collects what is wrong with a spec a visualization's
/// <c>Describe</c> gave, and throws one plain error naming that Describe. A spec that fails is a bug in Describe (a field
/// passed in the wrong place, a null, a K below zero), never in the instance: the checks only cover what the problem's own
/// parser already rules out, so every instance the problem accepts still draws as before.
/// </summary>
static class SpecCheck {
    /// <summary>Throws when <paramref name="problems"/> is not empty: what is wrong with the <paramref name="spec"/> Describe gave.</summary>
    public static void ThrowIfAny(string spec, string? describedBy, List<string> problems) {
        if (problems.Count == 0) return;
        throw new InvalidOperationException(
            $"{describedBy ?? "A visualization"}.Describe gave a {spec} that cannot be drawn: {string.Join("; ", problems)}. " +
            "Check which of the problem's fields Describe passes, and in which place.");
    }

    /// <summary>Adds a problem when <paramref name="list"/> is null or holds a null.</summary>
    public static void NoNulls<T>(List<string> problems, string name, IEnumerable<T?>? list) {
        if (list == null) problems.Add($"{name} is null");
        else if (list.Any(x => x is null)) problems.Add($"{name} holds a null");
    }

    /// <summary>Adds a problem when <paramref name="value"/> is not one of its enum's names.</summary>
    public static void Defined<TEnum>(List<string> problems, string name, TEnum value) where TEnum : struct, Enum {
        if (!Enum.IsDefined(value)) problems.Add($"{name} is {value}, which is not a {typeof(TEnum).Name}");
    }

    /// <summary>Adds a problem when a K is below zero.</summary>
    public static void NotNegative(List<string> problems, string name, int? value) {
        if (value < 0) problems.Add($"{name} is {value}, below zero");
    }
}
