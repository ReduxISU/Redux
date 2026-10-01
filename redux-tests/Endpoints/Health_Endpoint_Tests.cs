using System.Net;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Endpoint tests for GET /health (issue #621). The endpoint itself is a
// minimal-API route mapped directly on `app` in Program.cs, not a controller —
// it exists purely as a liveness probe, so these tests only check that it
// answers fast and cheaply, not anything about problem/reflection data.
public class Health_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Health_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Returns200() {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsNonEmptyBody() {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }
}
