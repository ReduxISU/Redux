using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The renderer contract between this API and the GUI. Every member Redux_GUI is shown
/// (VisualizationTypeCatalog.GuiWireValues) except <see cref="Unimplemented"/> must have a matching key in
/// Redux_GUI/components/Visualization/svgs/Visualizations.js, which the Redux_GUI repo checks
/// with `npm run check:visualizations`, and those members are exactly the ones in
/// Documentation/visualization-types.json (checked by VisualizationType_Tests.ManifestMatchesEnum). A picture type added
/// for the visualization redesign stays out of both until Redux_GUI can draw it: it is listed only for clients that ask
/// with includePictures=true.
/// <para>
/// A tag set (#914): a visualization declares every member that fits, joined with <c>|</c>, in any order. The
/// values are powers of two in declared order; the API lists them in that order as <c>visualizationTypes</c>, and the
/// deprecated single <c>visualizationType</c> field holds the first of them. See <see cref="Tags"/>.
/// </para>
/// </summary>
[Flags]
[JsonConverter(typeof(TagJsonConverter<VisualizationType>))]
public enum VisualizationType {
    /// <summary>No renderer is claimed. The GUI shows an explicit "not renderable" state.</summary>
    Unimplemented = 0,
    /// <summary>Force-directed D3 graph.</summary>
    GraphD3 = 1 << 0,
    /// <summary>TikZ/LaTeX-rendered graph.</summary>
    GraphLaTeX = 1 << 1,
    /// <summary>D3 set/family-of-sets view.</summary>
    SetD3 = 1 << 2,
    /// <summary>CNF clause view.</summary>
    BooleanSatisfiability = 1 << 3,
    /// <summary>D3-drawn quantum circuit.</summary>
    QuantumCircuitD3 = 1 << 4,
    /// <summary>QASM circuit rendered by Q.js.</summary>
    QuantumCircuitQjs = 1 << 5,
    /// <summary>Pump scheduling timeline.</summary>
    PumpSchedule = 1 << 6,
    /// <summary>Step-table view backed by API_TableJSON.</summary>
    DynamicTable = 1 << 7,
    /// <summary>
    /// The shared Graph picture: a static payload plus one frame of named states per solver step. Reported only
    /// in the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; visualizations keep reporting their legacy
    /// type (<see cref="GraphD3"/>, <see cref="GraphLaTeX"/>) in /info so old frontends are unaffected.
    /// </summary>
    Graph = 1 << 8,
}
