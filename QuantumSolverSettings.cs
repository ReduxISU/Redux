/// <summary>
/// Manages configuration settings for the Quantum Solver service.
/// Provides a default base URL and allows customization through constructors.
/// </summary>
public static class QuantumSolverSettingsGlobal {
    private static QuantumSolverSettings _quantumSolver = null!;
    private static IHttpClientFactory _httpClientFactory = null!;

    // A quantum server set for one async flow only (a test's fake server), read before the process-wide values. See #809.
    private static readonly AsyncLocal<(QuantumSolverSettings Settings, IHttpClientFactory Factory)?> _scoped = new();

    /// <summary>Global Quantum Solver configuration instance, populated at startup.</summary>
    public static QuantumSolverSettings QuantumSolver {
        get => _scoped.Value?.Settings ?? _quantumSolver;
        internal set => _quantumSolver = value;
    }

    /// <summary>Shared IHttpClientFactory for QuantumServerAPI, populated at startup.</summary>
    public static IHttpClientFactory HttpClientFactory {
        get => _scoped.Value?.Factory ?? _httpClientFactory;
        internal set => _httpClientFactory = value;
    }

    /// <summary>
    /// Uses <paramref name="settings"/> and <paramref name="factory"/> for the current async flow only, until the result is
    /// disposed. A test that fakes the quantum server uses this instead of the setters: the process-wide values are written
    /// by every app that starts (Program.cs), and tests run in parallel, so a fake written there could be replaced by an
    /// app starting meanwhile, or be read by another test.
    /// </summary>
    internal static IDisposable UseForThisFlow(QuantumSolverSettings settings, IHttpClientFactory factory) {
        var previous = _scoped.Value;
        _scoped.Value = (settings, factory);
        return new Restore(previous);
    }

    private sealed class Restore((QuantumSolverSettings Settings, IHttpClientFactory Factory)? previous) : IDisposable {
        public void Dispose() => _scoped.Value = previous;
    }
}

/// <summary>Configuration settings for the Quantum Solver service.</summary>
public class QuantumSolverSettings {
    /// <summary>
    /// Gets or sets the base URL for the Quantum Solver service.
    /// </summary>
    public string BaseURL { get; set; } = "http://localhost:27100";
}