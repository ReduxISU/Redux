using System.Net;
using System.Text.Json;
using Xunit;

namespace Redux.Integration;
#pragma warning disable CS1591

// The seed for this tier and the template for the next test: plain HTTP against the running
// container, nothing imported from the API. `rbs integration-test` builds the image, starts it,
// waits until the readiness route in rbs.toml answers, and sets RBS_BASE_URL before running this.
public class ApiTests {
    private static readonly HttpClient Client = new() {
        BaseAddress = new Uri(BaseUrl()),
        Timeout = TimeSpan.FromSeconds(60),
    };

    private static string BaseUrl() {
        var url = Environment.GetEnvironmentVariable("RBS_BASE_URL");
        if (string.IsNullOrWhiteSpace(url)) {
            throw new InvalidOperationException(
                "RBS_BASE_URL is unset; run this suite via `rbs integration-test`");
        }
        return url.TrimEnd('/') + "/";
    }

    [Fact]
    public async Task Health_Answers() {
        var response = await Client.GetAsync("health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task Problem_Catalogue_Lists_SAT3() {
        // The route rbs waited on for readiness; a populated list proves the reflection scan ran.
        var response = await Client.GetAsync(
            "Navigation/Batch/allProblems", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problems = JsonSerializer.Deserialize<string[]>(json);
        Assert.NotNull(problems);
        Assert.Contains("SAT3", problems);
    }
}
