using System.Net;
using System.Text;
using System.Text.Json;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The CNF Formula picture of SAT and 3SAT (#925) must not change when the Formula becomes payload-driven (#929): every
// response a frontend can ask for today is compared byte for byte with a snapshot captured before #929 (Snapshots/
// formula.*.json). The generalization only adds optional fields, which a CNF picture never writes.
public class FormulaCnfCompat_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public FormulaCnfCompat_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    internal static string SnapshotPath(string file) => Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    private async Task<string> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        string body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return body;
    }

    // The default instances the snapshots were taken on, before #957 changed SAT's and 3SAT's defaults: the capture
    // is of the response before #929, so the instance it was taken on stays fixed here.
    private const string Sat = "(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)";
    private const string Sat3 = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";

    // snapshot file, endpoint, query, instance
    public static TheoryData<string, string, string, string> Cases => new() {
        { "formula.sat.frames.json", "visualize", "visualization=SatFormulaVisualization&format=frames", Sat },
        { "formula.sat3.frames.json", "visualize", "visualization=Sat3FormulaVisualization&format=frames", Sat3 },
        { "formula.sat3.unsat.frames.json", "visualize", "visualization=Sat3FormulaVisualization&format=frames", "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)" },
        { "formula.sat3.reductionview.json", "reductionView", "reduction=SipserReduceToCliqueStandard&sourceVisualization=Sat3FormulaVisualization", Sat3 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheCnfFormulaResponse_IsByteForByteTheOneBefore929(string snapshot, string path, string query, string instance) {
        string body = await Post(path, query, instance);
        string expected = File.ReadAllText(SnapshotPath(snapshot)).Replace("\r\n", "\n");
        Assert.Equal(expected, body);
    }
}
