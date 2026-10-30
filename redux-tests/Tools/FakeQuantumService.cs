using System.Net;
using System.Text;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

/// <summary>
/// Tests that replace the quantum service's process-wide settings (QuantumSolverSettingsGlobal) go in this collection,
/// which runs alone, so no other test sees the fake service.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuantumServiceCollection {
    public const string Name = "Quantum service settings";
}

/// <summary>
/// Installs a fake quantum service for the life of the scope and puts the real settings back afterwards. No network:
/// every request goes to <see cref="Handler"/>.
/// </summary>
public sealed class FakeQuantumService : IDisposable {
    private readonly QuantumSolverSettings _settings = QuantumSolverSettingsGlobal.QuantumSolver;
    private readonly IHttpClientFactory _factory = QuantumSolverSettingsGlobal.HttpClientFactory;

    public FakeHandler Handler { get; }

    private FakeQuantumService(FakeHandler handler) {
        Handler = handler;
        QuantumSolverSettingsGlobal.QuantumSolver = new QuantumSolverSettings { BaseURL = "http://fake-quantum-server" };
        QuantumSolverSettingsGlobal.HttpClientFactory = new FakeFactory(handler);
    }

    /// <summary>A service that cannot be reached: every request fails the way a refused connection does.</summary>
    public static FakeQuantumService Down() =>
        new(new FakeHandler(_ => throw new HttpRequestException("No connection could be made because the target machine actively refused it.")));

    /// <summary>A service that answers every request with <paramref name="status"/> and <paramref name="body"/>.</summary>
    public static FakeQuantumService Answering(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new FakeHandler(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    /// <summary>No service configured at all (the settings Program.cs fills at startup are missing).</summary>
    public static FakeQuantumService NotConfigured() {
        var scope = Down();
        QuantumSolverSettingsGlobal.QuantumSolver = null!;
        QuantumSolverSettingsGlobal.HttpClientFactory = null!;
        return scope;
    }

    public void Dispose() {
        QuantumSolverSettingsGlobal.QuantumSolver = _settings;
        QuantumSolverSettingsGlobal.HttpClientFactory = _factory;
    }

    public sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("http://fake-quantum-server") };
    }
}
