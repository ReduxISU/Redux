using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Gadgets carry a kind from one shared list plus stable ids (#689) next to the legacy
// color / reductionFromIds / reductionToIds trio (pinned by Gadget_Legacy_Snapshot_Tests).
public class Gadget_Kinds_Tests {
    private static JsonElement Serialize(object value) {
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions() { WriteIndented = true });
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static IEnumerable<(string Name, IReduction Red)> ReductionsWithGadgets() {
        foreach (var (_, type) in ProblemProvider.Reductions.OrderBy(p => p.Key, StringComparer.Ordinal)) {
            IReduction red;
            try { red = (IReduction)Activator.CreateInstance(type)!; } catch { continue; }
            if (red.gadgets.Count > 0) yield return (type.Name, red);
        }
    }

    [Fact]
    public void SerializedGadget_HasKindDiscriminator_LegacyTrio_AndNewFields() {
        var red = new SipserReduceToCliqueStandard();
        JsonElement first = Serialize(red.gadgets)[0];

        Assert.Equal("gadget", first.GetProperty("kind").GetString());
        Assert.Equal("ElementHighlight", first.GetProperty("color").GetString());
        Assert.Equal(JsonValueKind.Array, first.GetProperty("reductionFromIds").ValueKind);
        Assert.Equal(JsonValueKind.Array, first.GetProperty("reductionToIds").ValueKind);
        Assert.Equal("element", first.GetProperty("gadgetKind").GetString());
        Assert.Equal("c0-0", first.GetProperty("sourceIds")[0].GetString());
        Assert.Equal("0-0", first.GetProperty("reductionFromIds")[0].GetString());
        Assert.False(first.TryGetProperty("note", out _));
    }

    [Fact]
    public void GadgetKinds_SerializeAsCamelCaseStrings() {
        var expected = new Dictionary<GadgetKind, string> {
            [GadgetKind.Element] = "element", [GadgetKind.Group] = "group", [GadgetKind.EdgeRule] = "edgeRule",
            [GadgetKind.Palette] = "palette", [GadgetKind.OrGadget] = "orGadget", [GadgetKind.Bound] = "bound",
        };
        Assert.Equal(Enum.GetValues<GadgetKind>().Length, expected.Count);
        foreach (var (kind, name) in expected) {
            JsonElement g = Serialize(new Gadget(kind, new() { "a" }, new()));
            Assert.Equal(name, g.GetProperty("gadgetKind").GetString());
            Assert.Equal(kind == GadgetKind.Group ? "ClauseHighlight" : "ElementHighlight", g.GetProperty("color").GetString());
        }
    }

    [Fact]
    public void Note_IsWritten_WhenSet() {
        JsonElement g = Serialize(new Gadget(GadgetKind.Bound, new() { "c0" }, new(), note: "K = 3"));
        Assert.Equal("K = 3", g.GetProperty("note").GetString());
    }

    [Fact]
    public void LegacyConstructor_DerivesKindFromColor_AndCopiesIds() {
        var from = new List<string> { "0" };
        var to = new List<string> { "0" };
        var group = new Gadget("ClauseHighlight", from, to);
        var element = new Gadget("ElementHighlight", from, to);
        var other = new Gadget("SomethingElse", from, to);
        Assert.Equal(GadgetKind.Group, group.gadgetKind);
        Assert.Equal(GadgetKind.Element, element.gadgetKind);
        Assert.Equal(GadgetKind.Element, other.gadgetKind);
        // Editing the legacy list afterwards (KarpSATToSAT3 does) must not move the stable ids.
        to.Add("1");
        Assert.Equal(new[] { "0" }, group.targetIds);
    }

    [Fact]
    public void NewConstructor_DefaultsLegacyIdsToStableIds_WithoutAliasing() {
        var g = new Gadget(GadgetKind.Element, new() { "a" }, new() { "b" });
        g.reductionToIds.Add("x");
        Assert.Equal(new[] { "a" }, g.reductionFromIds);
        Assert.Equal(new[] { "b" }, g.targetIds);
    }

    [Fact]
    public void EveryEmittedGadget_HasStableIds_UnlessItIsARuleOrBound() {
        var emitting = ReductionsWithGadgets().ToList();
        Assert.NotEmpty(emitting);
        foreach (var (name, red) in emitting)
            foreach (Gadget g in red.gadgets) {
                bool mayBeEmpty = g.gadgetKind is GadgetKind.EdgeRule or GadgetKind.Bound or GadgetKind.Palette;
                Assert.True(mayBeEmpty || g.sourceIds.Count > 0 || g.targetIds.Count > 0, $"{name}: gadget without stable ids");
                Assert.All(g.sourceIds.Concat(g.targetIds), id => Assert.False(string.IsNullOrWhiteSpace(id), name));
            }
    }

    [Fact]
    public void EveryEmittedGadget_KeepsLegacyColorConsistentWithItsKind() {
        foreach (var (name, red) in ReductionsWithGadgets())
            foreach (Gadget g in red.gadgets)
                Assert.Equal(g.gadgetKind == GadgetKind.Group ? "ClauseHighlight" : "ElementHighlight", g.color);
    }

    [Fact]
    public void ThreeSatToClique_UsesFormulaIdsForSources_AndNodeNamesForTargets() {
        var red = new SipserReduceToCliqueStandard();
        var nodeNames = new HashSet<string>();
        foreach (var n in red.reduce().graph.Nodes) nodeNames.Add(n.ToString()!);

        Assert.Contains(red.gadgets, g => g.gadgetKind == GadgetKind.Group && g.sourceIds.SequenceEqual(new[] { "c0" }));
        Assert.Contains(red.gadgets, g => g.gadgetKind == GadgetKind.Element && g.sourceIds.SequenceEqual(new[] { "c0-1" }));
        foreach (Gadget g in red.gadgets) {
            Assert.All(g.sourceIds, id => Assert.Matches(@"^c\d+(-\d+)?$", id));
            Assert.All(g.targetIds, id => Assert.Contains(id, nodeNames));
        }
    }

    [Fact]
    public void CliqueToVertexCover_StableIdsEqualLegacyIds() {
        foreach (var (name, red) in ReductionsWithGadgets().Where(r => r.Name is "sipserReductionVertexCover" or "reduceToCLIQUE" or "GraphColoringToCliqueCover"))
            foreach (Gadget g in red.gadgets) {
                Assert.Equal(g.reductionFromIds, g.sourceIds);
                Assert.Equal(g.reductionToIds, g.targetIds);
            }
    }

    [Fact]
    public void KarpSatToSat3_StableIdsFollowLegacyIds_AfterTheReductionEditsThem() {
        foreach (var (name, red) in ReductionsWithGadgets().Where(r => r.Name == "KarpSATToSAT3"))
            foreach (Gadget g in red.gadgets) {
                Assert.Equal(g.reductionFromIds.Select(i => "c" + i), g.sourceIds);
                Assert.Equal(g.reductionToIds.Select(i => "c" + i), g.targetIds);
            }
    }
}
