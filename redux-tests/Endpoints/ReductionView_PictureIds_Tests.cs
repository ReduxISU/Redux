using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The ids of a picture the caller chose in the Reduction View (#929): a reduction names both sides' pieces by their default
// pictures' ids, and the view writes them in the chosen picture's (IFramesVisualization.IdsFromDefaultPicture), so every gadget
// and every highlight lands on a piece of the picture on screen. 0-1 ILP's Formula numbers its constraints r0.. where the Table
// numbers them r1.. (row r0 is x =); Subset Sum's Formula names the Packing picture's items I1.. as s0.. and its container as T.
public class ReductionView_PictureIds_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_PictureIds_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Formula3Sat = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";

    private async Task<(HttpStatusCode Status, string Body)> Post(string reduction, string instance, string query = "") {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reductionView?reduction={reduction}{query}", content,
            TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> View(string reduction, string instance, string query = "") {
        var (status, body) = await Post(reduction, instance, query);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement;
    }

    private static IReduction Build(string reduction) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!;

    private static string DefaultInstance(string reduction) =>
        reduction.Equals("SipserReduceToSAT3", StringComparison.OrdinalIgnoreCase)
            ? new SipserReduceToCliqueStandard(new SAT3(Formula3Sat)).reductionTo.instance
            : Build(reduction).reductionFrom.defaultInstance;

    // Every id a payload names, at any depth.
    private static HashSet<string> Ids(JsonElement e, HashSet<string>? ids = null) {
        ids ??= new HashSet<string>(StringComparer.Ordinal);
        if (e.ValueKind == JsonValueKind.Object) {
            foreach (var p in e.EnumerateObject()) {
                if (p.Name == "id" && p.Value.ValueKind == JsonValueKind.String) ids.Add(p.Value.GetString()!);
                else Ids(p.Value, ids);
            }
        } else if (e.ValueKind == JsonValueKind.Array) {
            foreach (var item in e.EnumerateArray()) Ids(item, ids);
        }
        return ids;
    }

    private static string[] Strings(JsonElement a) => a.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static List<string[]> GadgetIds(JsonElement view, string side) =>
        view.GetProperty("gadgets").EnumerateArray().Select(g => Strings(g.GetProperty(side + "Ids"))).ToList();

    private static List<JsonElement> Phase(JsonElement view, string phase) =>
        view.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == phase).ToList();

    // ---- Every reduction, every picture other than the default ----

    /// <summary>
    /// Each viewable reduction with each side drawn by every frames picture other than its problem's default that draws an answer
    /// (a search tree draws the solver's search, not the instance, so the reduction's pieces are not its pieces): every gadget id
    /// and every focus id of that side is an id of that picture's payload. The default pictures are pinned by the other Reduction
    /// View tests with their own id rules (a Table's cells are named by their row and column).
    /// </summary>
    [Theory]
    [MemberData(nameof(ReductionView_PictureChoice_Tests.Viewable), MemberType = typeof(ReductionView_PictureChoice_Tests))]
    public async Task EveryGadgetAndFocusId_IsAnIdOfTheChosenPicture(string reduction) {
        string instance = DefaultInstance(reduction);
        var red = Build(reduction);
        var missing = new List<string>();
        foreach (var (side, problem) in new[] { ("source", red.reductionFrom), ("target", red.reductionTo) }) {
            foreach (string name in ProblemProvider.VisualizationsOf(problem.GetType())) {
                var vis = (IVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[name.ToLower()])!;
                if (vis.GetType() == problem.defaultVisualization?.GetType()) continue;
                if (vis is not IFramesVisualization frames || vis.StepShape == null || !frames.DrawsAnswerAlone()) continue;
                var view = await View(reduction, instance, $"&{side}Visualization={name}");
                var ids = Ids(view.GetProperty(side).GetProperty("picture").GetProperty("payload"));
                foreach (var gadget in GadgetIds(view, side))
                    missing.AddRange(gadget.Where(id => !ids.Contains(id)).Select(id => $"{side} {name} gadget {id}"));
                foreach (var f in view.GetProperty("frames").EnumerateArray())
                    missing.AddRange(Strings(f.GetProperty(side + "Focus")).Where(id => !ids.Contains(id))
                        .Select(id => $"{side} {name} {f.GetProperty("phase").GetString()} focus {id}"));
            }
        }
        Assert.True(missing.Count == 0, string.Join("\n", missing.Distinct().Take(40)));
    }

    // ---- 3SAT to 0-1 ILP ----

    [Fact]
    public async Task ThreeSatToIlp_FormulaTarget_GadgetsNameTheFormulasPieces() {
        var view = await View("KarpIntProgStandard", Formula3Sat,
            "&sourceVisualization=Sat3FormulaVisualization&targetVisualization=IntProgrammingFormulaVisualization");
        Assert.Equal("Formula", view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        var targets = GadgetIds(view, "target");
        // The variable x1: its chip and every occurrence of it (rows R1 and R3; in R2, x1 and !x1 cancel out).
        Assert.Equal(new[] { "v:x1", "r0-x0", "r2-x0" }, targets[0]);
        // Clause C1 is constraint r0 (the Table's row r1), each literal its occurrence there, the bound its bound.
        Assert.Equal(new[] { "r0" }, targets[3]);
        Assert.Equal(new[] { "r0-x0" }, targets[4]);
        Assert.Equal(new[] { "r0-x1" }, targets[5]);
        Assert.Equal(new[] { "r0-x2" }, targets[6]);
        Assert.Equal(new[] { "r0-d" }, targets[7]);
        // C2 (!x1 | x3 | x1): x1 and !x1 cancel, so the row writes no x1 and those two literals stand for the whole row.
        Assert.Equal(new[] { "r1" }, targets[8]);
        Assert.Equal(new[] { "r1" }, targets[9]);
        Assert.Equal(new[] { "r1-x2" }, targets[10]);
        Assert.Equal(new[] { "r1" }, targets[11]);
        Assert.DoesNotContain(targets.SelectMany(t => t), id => System.Text.RegularExpressions.Regex.IsMatch(id, @"^r\d+c\d+$|^c\d+$"));

        // The build focuses the clause's constraint; the map reads each variable's chip.
        var build = Phase(view, "build");
        Assert.Equal(new[] { "v:x1", "v:x2", "v:x3" }, Strings(build[0].GetProperty("targetFocus")));
        Assert.Equal(new[] { "v:x1" }, Strings(build[1].GetProperty("targetFocus")));
        Assert.Equal(new[] { "r0" }, Strings(build[4].GetProperty("targetFocus")));
        var map = Phase(view, "map");
        Assert.Equal(new[] { "v:x1" }, Strings(map[0].GetProperty("targetFocus")));
        Assert.Equal(new[] { "v:x1", "c0-0", "c1-0", "c1-2", "c2-2" }, Strings(map[0].GetProperty("sourceFocus")));
    }

    [Fact]
    public async Task ThreeSatToIlp_TableTarget_KeepsTheTablesIds() {
        var view = await View("KarpIntProgStandard", Formula3Sat);
        Assert.Equal("Table", view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        var targets = GadgetIds(view, "target");
        Assert.Equal(new[] { "c0", "r0c0" }, targets[0]);
        Assert.Equal(new[] { "r1" }, targets[3]);
        Assert.Equal(new[] { "r1c0" }, targets[4]);
        Assert.Equal(new[] { "r1c4" }, targets[7]);
        Assert.Equal(new[] { "r0c0" }, Strings(Phase(view, "map")[0].GetProperty("targetFocus")));
    }

    // ---- Subset Sum's Formula ----

    [Fact]
    public async Task ExactCoverToSubsetSum_FormulaTarget_NamesTheNumbersAndT() {
        string instance = DefaultInstance("KarpExactCoverToSubsetSum");
        var view = await View("KarpExactCoverToSubsetSum", instance, "&targetVisualization=SubsetSumFormulaVisualization");
        var targets = GadgetIds(view, "target");
        Assert.Equal(new[] { "s0", "v:x1" }, targets[0]);
        Assert.Equal(new[] { "T" }, targets[^1]);
    }

    [Fact]
    public async Task SubsetSumToPartition_FormulaSource_NamesTheNumbersAndT() {
        string instance = DefaultInstance("SubsetSumToPartitionReduction");
        var view = await View("SubsetSumToPartitionReduction", instance, "&sourceVisualization=SubsetSumFormulaVisualization");
        var sources = GadgetIds(view, "source").SelectMany(s => s).ToHashSet();
        Assert.Contains("s0", sources);
        Assert.Contains("T", sources);
        Assert.DoesNotContain("I1", sources);
        Assert.DoesNotContain("C1", sources);
    }
}
