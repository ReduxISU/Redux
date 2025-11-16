using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Manages configuration settings for the Quantum Solver service.
/// Provides a default base URL and allows customization through constructors.
/// </summary>
public class QuantumSolverSettings
{
    private static string defaultBaseURL = "http://towel.aws.cose.isu.edu:8080";

    /// <summary>
    /// Gets or sets the base URL for the Quantum Solver service.
    /// </summary>
    public static string BaseURL { get; set; } = defaultBaseURL;

    /// <summary>
    /// Initializes a new instance with the default base URL.
    /// </summary>
    public QuantumSolverSettings() : this(defaultBaseURL) {
    }
    
    /// <summary>
    /// Initializes a new instance with a custom base URL.
    /// </summary>
    /// <param name="input">The base URL to use for the Quantum Solver service.</param>
    public QuantumSolverSettings(string input) {
        QuantumSolverSettings.BaseURL = input;
    }
}