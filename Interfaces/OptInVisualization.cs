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

/// <summary>
/// The <see cref="VisualizationType"/> a visualization reported before the visualization redesign, for a visualization whose
/// type changed with it (Convex Hull's, Lossless Data Compression's and Sudoku's list pictures were <c>Unimplemented</c>).
/// Clients that have not opted in with <c>includePictures=true</c> are told this type, so Redux_GUI keeps seeing what it saw;
/// clients that opt in are told the visualization's own <see cref="IVisualization.visualizationType"/>. An attribute, so it adds
/// nothing to a visualization's JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
sealed class LegacyVisualizationTypeAttribute(VisualizationType type) : Attribute {
    /// <summary>The type reported to clients that have not opted in.</summary>
    public VisualizationType Type { get; } = type;
}

/// <summary>
/// What a client that has not opted in (<c>includePictures=true</c>) is shown of the visualization redesign: nothing. Every
/// response such a client gets is what the API answered before the redesign, which is what Redux_GUI was built against.
/// <list type="bullet">
/// <item>An <see cref="IOptInVisualization"/> is left out of the listings (see <see cref="OptInVisualizations.Listed"/>).</item>
/// <item>A problem whose default visualization is opt-in shows the placeholder (<see cref="DummyClasses.DummyVisualization"/>)
/// instead, wherever the problem is serialized (/info, allInfo, problemInstance, reduce) and wherever its default is drawn
/// (visualizeReduction).</item>
/// <item>A visualization with a <see cref="LegacyVisualizationTypeAttribute"/> reports that type.</item>
/// </list>
/// </summary>
static class GuiView {
    /// <summary>
    /// The visualization a client is shown in place of <paramref name="visualization"/>: itself, unless it is opt-in and the
    /// client did not opt in, in which case the placeholder that stood there before the redesign.
    /// </summary>
    public static IVisualization Shown(IVisualization visualization, bool includePictures) =>
        includePictures || !OptInVisualizations.IsOptIn(visualization.GetType()) ? visualization : new DummyClasses.DummyVisualization();

    /// <summary>The type a client is told <paramref name="visualization"/> has (see <see cref="LegacyVisualizationTypeAttribute"/>).</summary>
    public static VisualizationType TypeOf(IVisualization visualization, bool includePictures) =>
        includePictures ? visualization.visualizationType : LegacyType(visualization.GetType()) ?? visualization.visualizationType;

    /// <summary>The legacy type declared on <paramref name="visualization"/>, or null when it has none.</summary>
    public static VisualizationType? LegacyType(Type visualization) =>
        (visualization.GetCustomAttributes(typeof(LegacyVisualizationTypeAttribute), false).FirstOrDefault() as LegacyVisualizationTypeAttribute)?.Type;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Text.Json.JsonSerializerOptions, System.Text.Json.JsonSerializerOptions> ForGui = new();

    /// <summary>
    /// <paramref name="options"/> as is for a client that opted in; for one that did not, a copy that also applies
    /// <see cref="Modify"/>. The copy is made once per options object.
    /// </summary>
    public static System.Text.Json.JsonSerializerOptions Options(System.Text.Json.JsonSerializerOptions options, bool includePictures) =>
        includePictures ? options : ForGui.GetValue(options, o => new System.Text.Json.JsonSerializerOptions(o) {
            TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.WithAddedModifier(
                o.TypeInfoResolver ?? new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(), Modify),
        });

    /// <summary>
    /// A type-info modifier for the responses of clients that have not opted in: every visualization reports its legacy type,
    /// and every property that holds a visualization (a problem's <c>defaultVisualization</c>, a reduction's
    /// <c>visualization</c>) holds the placeholder in place of an opt-in one.
    /// </summary>
    public static void Modify(System.Text.Json.Serialization.Metadata.JsonTypeInfo info) {
        if (info.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object) return;
        var properties = info.Properties;
        for (int i = 0; i < properties.Count; i++) {
            var property = properties[i];
            var get = property.Get;
            if (get == null) continue;
            if (typeof(IVisualization).IsAssignableFrom(info.Type) && property.Name == nameof(IVisualization.visualizationType)
                && property.PropertyType == typeof(VisualizationType)) {
                property.Get = obj => obj is IVisualization vis ? TypeOf(vis, false) : get(obj);
            } else if (typeof(IVisualization).IsAssignableFrom(property.PropertyType)) {
                if (property.PropertyType.IsAssignableFrom(typeof(DummyClasses.DummyVisualization))) {
                    property.Get = obj => get(obj) is IVisualization vis ? Shown(vis, false) : get(obj);
                } else if (OptInVisualizations.IsOptIn(property.PropertyType)) {
                    // Declared as the opt-in class itself, which cannot hold the placeholder: written as the placeholder instead.
                    var shown = info.CreateJsonPropertyInfo(typeof(DummyClasses.DummyVisualization), property.Name);
                    shown.Get = obj => get(obj) is null ? null : new DummyClasses.DummyVisualization();
                    properties[i] = shown;
                }
            }
        }
    }
}
