using System.Text.RegularExpressions;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A frontend drawing the list-format set pictures (/visualize, /visualizeReduction) highlights a gadget by finding its ids in
// them. A set system's picture (Exact Cover, Hitting Set, Set Cover) names its pieces with the Bipartite Graph picture's ids
// (element e:{x}, set S{j}); a numbers problem's picture (Subset Sum, Partition, Knapsack) names its numbers with the Packing
// picture's item ids (I{k}). Those are the ids the gadgets use, so they are found as they are. The one exception is a Packing
// container (C{k}: Subset Sum's target, Partition's two sides, the knapsack), which the list picture does not draw.
public class SetListPicture_GadgetIds_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SetListPicture_GadgetIds_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static bool IsSetPicture(object problem) =>
        problem is EXACTCOVER or HITTINGSET or SETCOVER or SUBSETSUM or PARTITION or KNAPSACK;

    private static bool Container(string id) => Regex.IsMatch(id, @"^C\d+$");

    public static TheoryData<string> FromSetPicture() {
        var data = new TheoryData<string>();
        foreach (var (name, red) in ListPictureGadgetIds.ReductionsWithGadgets().Where(r => IsSetPicture(r.Reduction.reductionFrom)))
            data.Add(name);
        return data;
    }

    public static TheoryData<string> ToSetPicture() {
        var data = new TheoryData<string>();
        foreach (var (name, red) in ListPictureGadgetIds.ReductionsWithGadgets().Where(r => IsSetPicture(r.Reduction.reductionTo)))
            data.Add(name);
        return data;
    }

    [Fact]
    public void BothSidesHaveCases() {
        Assert.NotEmpty(FromSetPicture());
        Assert.NotEmpty(ToSetPicture());
    }

    [Theory]
    [MemberData(nameof(FromSetPicture))]
    public async Task SourceIds_AreDrawnOnTheSourceSetPicture(string reduction) {
        var red = ListPictureGadgetIds.ReductionsWithGadgets().Single(r => r.Name == reduction).Reduction;
        var pictures = await ListPictureGadgetIds.Fetch(_client, red, reduction);
        var drawn = ListPictureGadgetIds.Ids(pictures.Source);
        foreach (var g in pictures.Gadgets)
            foreach (string list in new[] { "sourceIds", "reductionFromIds" })
                foreach (string id in ListPictureGadgetIds.Strings(g, list).Where(id => !Container(id)))
                    Assert.True(drawn.Contains(id), $"{reduction}: {list} {id} is not in the list picture ({g.GetRawText()})");
    }

    [Theory]
    [MemberData(nameof(ToSetPicture))]
    public async Task TargetIds_AreDrawnOnTheTargetSetPicture(string reduction) {
        var red = ListPictureGadgetIds.ReductionsWithGadgets().Single(r => r.Name == reduction).Reduction;
        var pictures = await ListPictureGadgetIds.Fetch(_client, red, reduction);
        var drawn = ListPictureGadgetIds.Ids(pictures.Target);
        foreach (var g in pictures.Gadgets)
            foreach (string list in new[] { "targetIds", "reductionToIds" })
                foreach (string id in ListPictureGadgetIds.Strings(g, list).Where(id => !Container(id)))
                    Assert.True(drawn.Contains(id), $"{reduction}: {list} {id} is not in the list picture ({g.GetRawText()})");
    }

    // Every piece keeps an id of its own after the renaming, repeats included.
    [Theory]
    [InlineData("exactcoverdefaultvisualization", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})")]
    [InlineData("hittingsetdefaultvisualization", "({1,2,3,4},{{1,3},{2,3,4},{1,4}})")]
    [InlineData("setcoverdefaultvisualization", "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)")]
    [InlineData("subsetsumvisualization", "({7,7,1,12},8)")]
    [InlineData("partitiondefaultvisualization", "{1,5,5,11}")]
    [InlineData("knapsackvisualization", "({(1,2),(1,2),(3,4)},4,6)")]
    public async Task EveryPiece_HasItsOwnId(string visualization, string instance) {
        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(instance), System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?visualization={visualization}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        foreach (var frame in doc.RootElement.EnumerateArray()) {
            using var picture = System.Text.Json.JsonDocument.Parse(frame.ValueKind == System.Text.Json.JsonValueKind.String ? frame.GetString()! : frame.GetRawText());
            var ids = new List<string>();
            void Walk(System.Text.Json.JsonElement piece) {
                ids.Add(piece.GetProperty("id").GetString()!);
                if (piece.TryGetProperty("list", out var list))
                    foreach (var child in list.EnumerateArray()) Walk(child);
            }
            Walk(picture.RootElement.GetProperty("data"));
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }
    }
}
