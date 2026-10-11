using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The renderer contract between this API and the GUI. Every member except
/// <see cref="Unimplemented"/> must have a matching key in
/// Redux_GUI/components/Visualization/svgs/Visualizations.js, which the Redux_GUI repo checks
/// with `npm run check:visualizations`. Every member must also appear in
/// Documentation/visualization-types.json (checked by VisualizationType_Tests.ManifestMatchesEnum).
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
    /// The Tripartite Matching picture (3-Dimensional Matching): the elements of X, Y and Z in three columns, each triple of
    /// M a line through its three elements, the triples a solver chooses, the elements they use once or more than once, and
    /// the triples a choice rules out; a static payload plus one frame of named states per solver step. Like
    /// <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    TripartiteMatching,
    /// <summary>
    /// The Table picture: rows and columns of text cells drawn in HTML, in one of three shapes: a dynamic-programming grid
    /// (Edit Distance), a constraint matrix (0-1 Integer Linear Programming) or a trace table that gains a row per step (the
    /// second views of DFA, NFA, SSSP and SPSP). A static payload plus one frame of values and named states per solver step.
    /// Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy type (<see cref="DynamicTable"/>).
    /// </summary>
    Table,
    /// <summary>
    /// The Layered Graph picture (Topological Sort, Feedback Arc Set, Feedback Node Set, Strongly Connected Components,
    /// Single-Source and Single-Pair Shortest Path): a directed graph ranked left to right with its back edges marked; a
    /// static payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is reported only as the
    /// <c>type</c> of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    LayeredGraph,
    /// <summary>
    /// The Bipartite Graph picture (Set Cover, Exact Cover, Exact Hitting Set): elements in one column, sets in the other, an
    /// edge per membership, a static payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is
    /// reported only as the <c>type</c> of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps
    /// the legacy <see cref="SetD3"/>.
    /// </summary>
    BipartiteGraph,
    /// <summary>
    /// The Packing picture (Partition, Subset Sum, Knapsack, Bin Packing): items in a pool and containers drawn to scale with
    /// their target or limit lines, and which container each item is in; a static payload plus one frame of named states per
    /// solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="SetD3"/>.
    /// </summary>
    Packing,
}
