using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

namespace API.Interfaces.Steps;

/// <summary>
/// The picture type each visualization serves under <c>format=frames</c>, as /info reports it in <c>pictureType</c> (#841):
/// the <c>type</c> of the visualization's frames response for a visualization with a frames hook
/// (<see cref="IFramesVisualization"/>), and null for one that serves only the older formats (its <c>format=frames</c> is the
/// generic wrapping of the list, whose type is its legacy <see cref="IVisualization.visualizationType"/>).
/// </summary>
static class PictureTypes {
    /// <summary>The JSON name of the field.</summary>
    public const string JsonName = "pictureType";

    private static readonly ConcurrentDictionary<Type, string?> Probed = new();

    /// <summary>The picture type <paramref name="vis"/> serves under <c>format=frames</c>, or null when it has no frames hook.</summary>
    public static string? Of(IVisualization vis) => vis is IFramesVisualization frames ? frames.PictureType() : null;

    /// <summary>
    /// Reads the picture type off a frames response for the problem's default instance, once per visualization class. The
    /// run is an empty one stopped by the time limit, so no solver runs and no answer is read or verified; only the payload is
    /// built. Should that throw, an empty finished run (answer "{}") is tried; null if both fail, which the tests catch.
    /// </summary>
    public static string? Probe(IFramesVisualization vis) => Probed.GetOrAdd(vis.GetType(), _ => {
        string? instance = DefaultInstance(vis);
        if (instance == null) return null;
        var runs = new[] {
            new SolveRun(SolveRun.TimedOutAnswer, [], vis.StepShape) { TimedOut = true },
            new SolveRun("{}", [], vis.StepShape),
        };
        foreach (var run in runs) {
            try { return vis.BuildFrames(instance, run).Type; } catch { }
        }
        return null;
    });

    /// <summary>The default instance of the problem a visualization draws (its <c>IVisualization&lt;P&gt;</c>, else its solver's).</summary>
    private static string? DefaultInstance(IVisualization vis) {
        Type? problem = ProblemOf(vis.GetType(), typeof(IVisualization<>)) ?? (vis.solver is { } solver ? ProblemOf(solver.GetType(), typeof(ISolver<>)) : null);
        try {
            return problem != null && Activator.CreateInstance(problem) is IProblem p ? p.defaultInstance : null;
        } catch {
            return null;
        }
    }

    private static Type? ProblemOf(Type type, Type openGeneric) =>
        type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric)?.GetGenericArguments()[0];

    /// <summary>
    /// A type-info modifier that adds <c>pictureType</c> to every visualization a serializer writes (an
    /// <see cref="IVisualization"/>, or a property declared as one, such as a problem's <c>defaultVisualization</c>), right after
    /// <c>visualizationType</c>. Additive: every existing field keeps its name, value and order.
    /// </summary>
    public static void AddToVisualizations(JsonTypeInfo info) {
        if (info.Kind != JsonTypeInfoKind.Object || !typeof(IVisualization).IsAssignableFrom(info.Type)) return;
        if (info.Properties.Any(p => p.Name == JsonName)) return;
        var property = info.CreateJsonPropertyInfo(typeof(string), JsonName);
        property.Get = obj => obj is IVisualization vis ? Of(vis) : null;
        int after = info.Properties.ToList().FindIndex(p => p.Name == nameof(IVisualization.visualizationType));
        if (after < 0) info.Properties.Add(property);
        else info.Properties.Insert(after + 1, property);
    }

    /// <summary>A resolver for serializer options that adds <c>pictureType</c> (see <see cref="AddToVisualizations"/>).</summary>
    public static IJsonTypeInfoResolver Resolver() => new DefaultJsonTypeInfoResolver { Modifiers = { AddToVisualizations } };
}
