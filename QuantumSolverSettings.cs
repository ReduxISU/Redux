using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Manages configuration settings for the Quantum Solver service.
/// Provides a default base URL and allows customization through constructors.
/// </summary>
public static class QuantumSolverSettingsGlobal
{
    public static QuantumSolverSettings QuantumSolver { get; internal set; } = null!;
}

public class QuantumSolverSettings
{
    /// <summary>
    /// Gets or sets the base URL for the Quantum Solver service.
    /// </summary>
    public string BaseURL { get; set; } = "http://localhost:27100";
}