namespace API.Interfaces.Steps;

/// <summary>
/// Counts passes over a solver's algorithm, so tests can check that the visualize endpoint solves once (#734). A
/// solver calls <see cref="Hit"/> at the start of its typed Solve and of any other method that walks the algorithm
/// again. Counting happens only between <see cref="Start"/> and <see cref="Stop"/>, and only in the async flow that
/// called <see cref="Start"/>; otherwise <see cref="Hit"/> does nothing.
/// </summary>
static class SolveProbe {
    private static readonly AsyncLocal<Dictionary<Type, int>?> Counts = new();

    /// <summary>Starts counting in the current async flow and returns the counts by solver type.</summary>
    public static Dictionary<Type, int> Start() {
        var counts = new Dictionary<Type, int>();
        Counts.Value = counts;
        return counts;
    }

    /// <summary>Stops counting in the current async flow.</summary>
    public static void Stop() => Counts.Value = null;

    /// <summary>Counts one pass of <paramref name="solver"/>'s algorithm, if counting is on.</summary>
    public static void Hit(Type solver) {
        var counts = Counts.Value;
        if (counts == null) return;
        lock (counts) counts[solver] = counts.GetValueOrDefault(solver) + 1;
    }
}
