using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Pins /reduce and /gadgets for the three reductions that record construction steps (#691), on their
// default instances and a few more, captured BEFORE the change: recording steps must not alter the
// target a reduction builds, its gadgets, or the JSON the two endpoints return for them.
// One file, one section per (reduction, instance, endpoint).
// Regenerate deliberately with REDUX_UPDATE_SNAPSHOTS=1.
public class Reduction_Output_Snapshot_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Reduction_Output_Snapshot_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string SnapshotPath =>
        Path.Combine(ProjectSourcePath.Value, "redux-tests", "Reductions", "Snapshots", "reduction_output_691.txt");

    private static string DefaultInstance(string reduction) =>
        ((API.Interfaces.IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!).reductionFrom.defaultInstance;

    // reductionDefinition is a verbatim string literal, so its line endings follow the checkout and reach
    // the JSON escaped: \r\n on a Windows checkout, \n on Linux CI. Normalize the file's own line endings
    // and the escaped ones inside JSON strings.
    private static string Normalize(string json) => json.Replace("\r\n", "\n").Replace(@"\r\n", @"\n");

    public static IEnumerable<(string Reduction, string Instance)> Cases() {
        const string Sat = "SipserReduceToCliqueStandard", Vc = "sipserReductionVertexCover", Is = "reduceToCLIQUE";
        yield return (Sat, DefaultInstance(Sat));
        yield return (Sat, "(x1 | x2 | x3) & (!x1 | !x2 | x3) & (x1 | !x3 | x2)");
        yield return (Sat, "(x1 | x2) & (!x1 | x3) & (!x2 | !x3 | y)");
        yield return (Sat, "(x1) & (!x1)");
        yield return (Sat, "(x1 | x1 | !x2) & (x2 | x3 | x4) & (!x4 | !x1 | x2) & (x3)");
        yield return (Vc, DefaultInstance(Vc));
        yield return (Vc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)");
        yield return (Vc, "(({a,b,c,d},{{b,a},{c,b},{c,d}}),2)");
        yield return (Vc, "(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        yield return (Vc, "(({a,b,c,d,e},{}),1)");
        yield return (Is, DefaultInstance(Is));
        yield return (Is, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)");
        yield return (Is, "(({a,b,c,d},{{b,a},{d,c}}),1)");
        yield return (Is, "(({a,b,c},{{a,b},{b,c},{a,c}}),2)");
        yield return (Is, "(({a,b,c,d,e},{}),3)");
    }

    private async Task<string> Post(string endpoint, string reduction, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{endpoint}?reduction={reduction}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Normalize(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReduceAndGadgets_AreUnchanged_ForTheThreeReductionsThatRecordBuildSteps() {
        var sb = new StringBuilder();
        foreach (var (reduction, instance) in Cases()) {
            foreach (string endpoint in new[] { "reduce", "gadgets" }) {
                sb.Append("### ").Append(endpoint).Append(' ').Append(reduction).Append(' ').Append(instance).Append('\n');
                sb.Append(await Post(endpoint, reduction, instance)).Append('\n');
            }
        }
        string actual = sb.ToString();
        if (Environment.GetEnvironmentVariable("REDUX_UPDATE_SNAPSHOTS") == "1") {
            File.WriteAllText(SnapshotPath, actual);
            return;
        }
        Assert.Equal(Normalize(File.ReadAllText(SnapshotPath)), actual);
    }
}
