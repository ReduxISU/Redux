/// <summary>
/// Manages configuration settings for the Quantum Solver service.
/// Provides a default base URL and allows customization through constructors.
/// </summary>
public static class QuantumSolverSettingsGlobal {
    /// <summary>Global Quantum Solver configuration instance, populated at startup.</summary>
    public static QuantumSolverSettings QuantumSolver { get; internal set; } = null!;

    /// <summary>Shared IHttpClientFactory for QuantumServerAPI, populated at startup.</summary>
    public static IHttpClientFactory HttpClientFactory { get; internal set; } = null!;
}

/// <summary>Configuration settings for the Quantum Solver service.</summary>
public class QuantumSolverSettings {
    /// <summary>
    /// Gets or sets the base URL for the Quantum Solver service.
    /// </summary>
    public string BaseURL { get; set; } = "http://localhost:27100";
}

/// <summary>Registers the HTTP client the quantum solvers use, in the web host and in a solve worker process (#936).</summary>
public static class QuantumHttpClientRegistration {
    /// <summary>The named client "quantum": the configured base URL and a 30-second timeout.</summary>
    public static IServiceCollection AddQuantumHttpClient(this IServiceCollection services) {
        services.AddHttpClient("quantum", (sp, client) => {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<QuantumSolverSettings>>().CurrentValue;
            client.BaseAddress = new Uri(settings.BaseURL);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}