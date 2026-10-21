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
    /// The Grouped Graph picture (the clause graph of SAT and 3SAT): literals grouped into one cluster per clause, a static
    /// payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c>
    /// of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy
    /// <see cref="BooleanSatisfiability"/>.
    /// </summary>
    GroupedGraph,
    /// <summary>
    /// The Reduction View: problem A and problem B side by side, the gadget map between them, and three phases of frames
    /// (build the target, solve it, map the answer back). Each pane is drawn with its problem's own picture. Like
    /// <see cref="Graph"/> it is reported only by its own endpoint (<c>/ProblemProvider/reductionView</c>, as <c>type</c>);
    /// /info is unaffected.
    /// </summary>
    Reduction,
    /// <summary>
    /// The Formula picture (SAT, 3SAT), a second view next to the Grouped Graph: the CNF formula as it is written, clause
    /// boxes joined by ∧ and literals by ∨, with an assignment strip of the variables (#925). The Grouped Graph's spec, ids and
    /// states, plus the state of each variable; a static payload plus one frame of named states per solver step. Like
    /// <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="BooleanSatisfiability"/>.
    /// </summary>
    Formula,
}
