using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A frontend drawing the list-format SAT and 3SAT pictures (/visualize, /visualizeReduction) highlights a gadget by finding its
// ids in them. The pictures give clause i the id c{i} and its j-th literal c{i}-{j}, the stable ids every gadget out of or into a
// formula uses, so those ids are found as they are. The list picture draws no variables: a variable gadget (v:{name}) is
// highlighted through its legacy ids, the variable's occurrences, which are found instead. Redux_GUI reads only the legacy ids.
public class SatListPicture_GadgetIds_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SatListPicture_GadgetIds_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static bool IsFormula(object problem) => problem is SAT or SAT3;

    public static TheoryData<string> FromFormula() {
        var data = new TheoryData<string>();
        foreach (var (name, red) in ListPictureGadgetIds.ReductionsWithGadgets().Where(r => IsFormula(r.Reduction.reductionFrom)))
            data.Add(name);
        return data;
    }

    public static TheoryData<string> ToFormula() {
        var data = new TheoryData<string>();
        foreach (var (name, red) in ListPictureGadgetIds.ReductionsWithGadgets().Where(r => IsFormula(r.Reduction.reductionTo)))
            data.Add(name);
        return data;
    }

    [Fact]
    public void BothSidesHaveCases() {
        Assert.NotEmpty(FromFormula());
        Assert.NotEmpty(ToFormula());
    }

    [Theory]
    [MemberData(nameof(FromFormula))]
    public async Task SourceIds_AreDrawnOnTheSourceFormula(string reduction) {
        var red = ListPictureGadgetIds.ReductionsWithGadgets().Single(r => r.Name == reduction).Reduction;
        var pictures = await ListPictureGadgetIds.Fetch(_client, red, reduction);
        var drawn = ListPictureGadgetIds.Ids(pictures.Source);
        foreach (var g in pictures.Gadgets) {
            foreach (string id in ListPictureGadgetIds.Strings(g, "sourceIds").Where(id => !id.StartsWith("v:", StringComparison.Ordinal)))
                Assert.True(drawn.Contains(id), $"{reduction}: source id {id} is not in the list picture ({g.GetRawText()})");
            Assert.All(ListPictureGadgetIds.Strings(g, "reductionFromIds"), id => Assert.Contains(id, drawn));
        }
    }

    [Theory]
    [MemberData(nameof(ToFormula))]
    public async Task TargetIds_AreDrawnOnTheTargetFormula(string reduction) {
        var red = ListPictureGadgetIds.ReductionsWithGadgets().Single(r => r.Name == reduction).Reduction;
        var pictures = await ListPictureGadgetIds.Fetch(_client, red, reduction);
        var drawn = ListPictureGadgetIds.Ids(pictures.Target);
        foreach (var g in pictures.Gadgets) {
            foreach (string id in ListPictureGadgetIds.Strings(g, "targetIds").Where(id => !id.StartsWith("v:", StringComparison.Ordinal)))
                Assert.True(drawn.Contains(id), $"{reduction}: target id {id} is not in the list picture ({g.GetRawText()})");
            Assert.All(ListPictureGadgetIds.Strings(g, "reductionToIds"), id => Assert.Contains(id, drawn));
        }
    }
}
