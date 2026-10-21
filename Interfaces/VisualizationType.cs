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
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualizationType>))]
public enum VisualizationType {
    /// <summary>No renderer is claimed. The GUI shows an explicit "not renderable" state.</summary>
    Unimplemented = 0,
    /// <summary>Force-directed D3 graph.</summary>
    GraphD3,
    /// <summary>TikZ/LaTeX-rendered graph.</summary>
    GraphLaTeX,
    /// <summary>D3 set/family-of-sets view.</summary>
    SetD3,
    /// <summary>CNF clause view.</summary>
    BooleanSatisfiability,
    /// <summary>D3-drawn quantum circuit.</summary>
    QuantumCircuitD3,
    /// <summary>QASM circuit rendered by Q.js.</summary>
    QuantumCircuitQjs,
    /// <summary>Pump scheduling timeline.</summary>
    PumpSchedule,
    /// <summary>Step-table view backed by API_TableJSON.</summary>
    DynamicTable,
    /// <summary>
    /// The shared Graph picture: a static payload plus one frame of named states per solver step. Reported only
    /// in the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; visualizations keep reporting their legacy
    /// type (<see cref="GraphD3"/>, <see cref="GraphLaTeX"/>) in /info so old frontends are unaffected.
    /// </summary>
    Graph,
    /// <summary>
    /// The Bipartite Graph picture (Set Cover, Exact Cover, Exact Hitting Set): elements in one column, sets in the other, an
    /// edge per membership, a static payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is
    /// reported only as the <c>type</c> of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps
    /// the legacy <see cref="SetD3"/>.
    /// </summary>
    BipartiteGraph,
    /// <summary>
    /// The Nested Set picture (Set Cover, Exact Cover, Exact Hitting Set), a second view next to the Bipartite Graph: every
    /// element a dot and every set a region around its own elements, or a list of the sets when the regions would not read
    /// (#926). The Bipartite Graph's spec, ids and states without its edges; a static payload plus one frame of named states per
    /// solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="SetD3"/>.
    /// </summary>
    NestedSet,
}
