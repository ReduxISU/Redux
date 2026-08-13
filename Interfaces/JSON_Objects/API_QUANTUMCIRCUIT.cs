namespace API.Interfaces.JSON_Objects;

using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Supported output formats for quantum circuit visualizations.
/// </summary>
public enum QuantumCircuitFormat {
    /// <summary>
    /// Raw OpenQASM string representation.
    /// </summary>
    QASM,

    /// <summary>
    /// Structured JSON payload for D3-based visualization.
    /// </summary>
    D3
}

/// <summary>
/// Common response object for quantum circuit visualizations.
/// A visualization may provide either a QASM string or a structured D3 payload.
/// </summary>
public sealed class API_QUANTUMCIRCUIT : API_JSON {
    /// <summary>
    /// The solution to the quantum problem
    /// </summary>
    public string? solution { get; set; }

    /// <summary>
    /// Indicates which circuit representation is populated.
    /// </summary>
    public QuantumCircuitFormat format { get; set; }

    /// <summary>
    /// Raw OpenQASM circuit string.
    /// Used by QASM-based visualizations.
    /// </summary>
    public string? qasm { get; set; }

    /// <summary>
    /// Structured circuit payload for D3-based visualizations.
    /// </summary>
    public JsonElement? d3 { get; set; }

    /// <summary>
    /// Optional metadata associated with the visualization.
    /// </summary>
    public Dictionary<string, object?>? metadata { get; set; }
}
