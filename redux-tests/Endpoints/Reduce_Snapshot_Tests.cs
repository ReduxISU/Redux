using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Pins /reduce and /info for the reductions that gain a backward map (#690), captured BEFORE the
// change: adding mapSolutionBack to IReduction must not leak into either response. /reduce serializes
// through the IReduction interface, so a new interface property (even a default one) would show up here.
//   reduce.<reduction>.json / .instance.txt   /reduce response for the source problem's default instance
//   info.<reduction>.json                     /info response
// Regenerate deliberately with REDUX_UPDATE_SNAPSHOTS=1.
public class Reduce_Snapshot_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Reduce_Snapshot_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static bool Update => Environment.GetEnvironmentVariable("REDUX_UPDATE_SNAPSHOTS") == "1";

    private static string SourcePath(string file) =>
        Path.Combine(ProjectSourcePath.Value, "redux-tests", "Endpoints", "Snapshots", file);

    // reductionDefinition is a verbatim string literal, so its line endings follow the source file's
    // checkout (CRLF on Windows, LF on Linux CI) and show up escaped in the JSON. Compare without them.
    private static string Normalize(string json) => json.Replace("\r\n", "\n").Replace("\\r\\n", "\\n");

    private static string ReadSnapshot(string file) =>
        Normalize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)));

    private static string DefaultInstanceOf(string reduction) {
        var type = ProblemProvider.Reductions[reduction.ToLower()];
        return ((API.Interfaces.IReduction)Activator.CreateInstance(type)!).reductionFrom.defaultInstance;
    }

    [Theory]
    [InlineData("SipserReduceToCliqueStandard")]
    [InlineData("sipserReductionVertexCover")]
    [InlineData("reduceToCLIQUE")]
    public async Task Reduce_DefaultInstance_MatchesPreChangeSnapshot(string reduction) {
        string instance = DefaultInstanceOf(reduction);
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reduce?reduction={reduction}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        string name = reduction.ToLower();
        if (Update) {
            File.WriteAllText(SourcePath($"reduce.{name}.instance.txt"), instance);
            File.WriteAllText(SourcePath($"reduce.{name}.json"), body);
            return;
        }
        Assert.Equal(instance, ReadSnapshot($"reduce.{name}.instance.txt"));
        Assert.Equal(ReadSnapshot($"reduce.{name}.json"), Normalize(body));
    }

    [Theory]
    [InlineData("SipserReduceToCliqueStandard")]
    [InlineData("sipserReductionVertexCover")]
    [InlineData("reduceToCLIQUE")]
    public async Task Info_OfReduction_MatchesPreChangeSnapshot(string reduction) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={reduction}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        string name = reduction.ToLower();
        if (Update) {
            File.WriteAllText(SourcePath($"info.{name}.json"), body);
            return;
        }
        Assert.Equal(ReadSnapshot($"info.{name}.json"), Normalize(body));
    }
}
