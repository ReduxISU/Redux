using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Whitespace in a certificate must never change what /ProblemProvider/mapSolution returns. Some mappers
// misread it silently instead of throwing (sipserReductionVertexCover mapped "{2, 3, 4, 5}" to a wrong
// certificate), so a retry-on-parse-failure is not enough; see ProblemProvider.MapSolutions.
//
// Shares the non-parallel WhitespaceTolerance collection: it solves every reduction's source problem.
[Collection("WhitespaceTolerance")]
public class MapSolutionWhitespace_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public MapSolutionWhitespace_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static readonly (string Name, Func<string, string> Apply)[] Variants = {
        ("comma+space", s => s.Replace(",", ", ")),
        ("comma+LF", s => s.Replace(",", ",\n")),
        ("comma+tab", s => s.Replace(",", ",\t")),
        ("trailing LF", s => s + "\n"),
        ("leading LF", s => "\n" + s),
    };

    private async Task<(HttpStatusCode Status, string Body)> MapAsync(string reduction, string instance, string solution) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/ProblemProvider/mapSolution?reduction={reduction}&solution={Uri.EscapeDataString(solution)}",
            content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // The repro from the #626 review.
    [Fact]
    public async Task MapSolution_SpacedCliqueCertificate_MapsLikeCompactOne() {
        const string instance = "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)";

        var compact = await MapAsync("sipserReductionVertexCover", instance, "{2,3,4,5}");
        var spaced = await MapAsync("sipserReductionVertexCover", instance, "{2, 3, 4, 5}");
        var multiline = await MapAsync("sipserReductionVertexCover", instance, "{2,\n3,\n4,\n5}");

        Assert.Equal(HttpStatusCode.OK, compact.Status);
        Assert.Equal("\"{1,6}\"", compact.Body);
        Assert.Equal(compact, spaced);
        Assert.Equal(compact, multiline);
    }

    public static IEnumerable<TheoryDataRow<string>> ReductionNames() =>
        ProblemProvider.Reductions.Values
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new TheoryDataRow<string>(n));

    [Theory]
    [MemberData(nameof(ReductionNames))]
    public async Task WhitespaceVariants_DoNotChangeMapSolutionResult(string reduction) {
        // Same as the metadata catalogs: a reduction that can't be default-constructed (a test fake, or
        // SipserReduceToSAT3, whose source problem's default instance isn't Sipser-formatted) has no
        // default instance to map from.
        IReduction red;
        try {
            red = (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!;
        } catch (Exception ex) {
            Assert.Skip($"{reduction}: can't be default-constructed ({(ex.InnerException ?? ex).Message})");
            return;
        }
        IProblem from = red.reductionFrom;
        string instance = from.defaultInstance;

        Task<string> solve = Task.Run(() => from.defaultSolver.solve(instance), TestContext.Current.CancellationToken);
        Task finished = await Task.WhenAny(solve, Task.Delay(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        if (finished != solve)
            Assert.Skip($"{reduction}: the source problem's default solve timed out");
        string solution = await solve;

        var baseline = await MapAsync(reduction, instance, solution);
        if (baseline.Status != HttpStatusCode.OK)
            Assert.Skip($"{reduction}: mapping the default solution as given already fails ({(int)baseline.Status}), so there is nothing to compare against");

        var failures = new List<string>();
        foreach (var (variant, apply) in Variants) {
            var result = await MapAsync(reduction, instance, apply(solution));
            if (result != baseline)
                failures.Add($"{variant}: {(int)result.Status} {result.Body} (expected {baseline.Body})");
        }
        Assert.True(failures.Count == 0, $"{reduction} with solution '{solution}': {string.Join("; ", failures)}");
    }
}
