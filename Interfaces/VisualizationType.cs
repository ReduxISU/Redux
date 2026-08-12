using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The renderer contract between this API and the GUI. Every member except
/// <see cref="Unimplemented"/> must have a matching key in
/// Redux_GUI/components/Visualization/svgs/Visualizations.js. CI enforces this.
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
}
