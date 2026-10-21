namespace API.Interfaces;

/// <summary>
/// Marks a visualization that only clients which draw the picture types (format=frames) can show, such as the Formula and
/// Nested Set views. Redux_GUI has no renderer for these yet, so the listing endpoints it reads (the per-problem
/// visualization list, allVisualizations, allVisualizationTypes, allInfo and the contributor profile) leave them out unless
/// the client opts in with <c>includePictures=true</c> (<see cref="OptInVisualizations.QueryName"/>). Everything else about
/// them is unchanged: a client that names one (/info, visualize, visualizeRun, the Reduction View) still gets it.
/// <para>
/// A marker with no members, so it adds nothing to a visualization's JSON. Mark a picture-type view by implementing it, or
/// let its picture interface extend it (as <c>IFormulaVisualization</c> does).
/// </para>
/// </summary>
interface IOptInVisualization { }

/// <summary>Which visualizations a listing shows: see <see cref="IOptInVisualization"/>.</summary>
static class OptInVisualizations {
    /// <summary>The query parameter a client passes (<c>includePictures=true</c>) to list the opt-in visualizations too.</summary>
    public const string QueryName = "includePictures";

    /// <summary>Whether <paramref name="visualization"/> is listed only on request.</summary>
    public static bool IsOptIn(Type visualization) => typeof(IOptInVisualization).IsAssignableFrom(visualization);

    /// <summary>Whether a listing shows <paramref name="visualization"/>: always, unless it is opt-in and the client did not opt in.</summary>
    public static bool Listed(Type visualization, bool includePictures) => includePictures || !IsOptIn(visualization);
}
