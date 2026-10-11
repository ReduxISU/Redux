using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;
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

    // Which kinds each reduction emits on its default instance, and how many of each. Written out by hand so
    // that a reduction switching a gadget's kind (or dropping one) fails here.
    private static readonly Dictionary<string, string> ExpectedKinds = new() {
        // The reductions out of 3SAT and SAT (#693) on their default formula.
        ["GareyJohnson"] = "Element x9, Group x3, Palette x1, OrGadget x3",
        ["GraphColoringToCliqueCover"] = "Element x9, EdgeRule x6, Bound x1",
        ["KarpIntProgStandard"] = "Element x12, OrGadget x3, Bound x3",
        ["KarpReduceGRAPHCOLORING"] = "Element x3, Palette x1, OrGadget x3, Bound x1",
        // 11 for the literals and the padding, then (#693) one per variable and one per new variable of a split.
        ["KarpSATToSAT3"] = "Element x17, Group x3",
        ["KarpVertexCoverToNodeSet"] = "Element x10, Bound x1",
        ["LawlerKarp"] = "Element x10, Bound x1",
        // #694: an edge rule per node that gets complement edges, and the bound.
        ["reduceToCLIQUE"] = "Element x7, EdgeRule x6, Bound x1",
        ["sipserReductionVertexCover"] = "Element x6, EdgeRule x3, Bound x1",
        ["SipserReduceToCliqueStandard"] = "Element x9, Group x3",
        ["SipserReduceToSAT3"] = "Element x9, Group x3",
        ["KarpVertexCoverToSetCover"] = "Element x10, Bound x1",
        ["KarpDirectedHamiltonianToUndirectedHamiltonian"] = "Element x11",
        ["KarpGraphColorToExactCover"] = "Group x21",
        ["KarpReduceSAT"] = "Element x27, Group x21",
        // #695: one element per number, and the bound (the container on both sides).
        ["FengReduction"] = "Element x4, Bound x1",
        // #695: one element per number, the two added numbers, and the two sides.
        ["SubsetSumToPartitionReduction"] = "Element x4, Palette x2, Bound x1",
    };

    private static string KindCounts(IEnumerable<Gadget> gadgets) =>
        string.Join(", ", gadgets.GroupBy(g => g.gadgetKind).OrderBy(g => g.Key).Select(g => $"{g.Key} x{g.Count()}"));

    [Fact]
    public void EveryEmittingReduction_EmitsTheKindsInTheExpectedTable() {
        var actual = ReductionsWithGadgets().ToDictionary(r => r.Name, r => KindCounts(r.Red.gadgets));
        string Table(IDictionary<string, string> t) => string.Join("\n", t.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}: {p.Value}"));
        string expected = Table(ExpectedKinds), got = Table(actual);
        Assert.True(expected == got, $"expected:\n{expected}\nactual:\n{got}");
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
    public void GraphReductions_StableIdsEqualLegacyIds() {
        foreach (var (name, red) in ReductionsWithGadgets().Where(r => r.Name is "sipserReductionVertexCover" or "reduceToCLIQUE" or "GraphColoringToCliqueCover"))
            foreach (Gadget g in red.gadgets) {
                Assert.Equal(g.reductionFromIds, g.sourceIds);
                Assert.Equal(g.reductionToIds, g.targetIds);
            }
    }

    [Fact]
    public void KarpSatToSat3_StableIdsFollowLegacyIds_AfterTheReductionEditsThem() {
        // The clause and literal gadgets; the variable gadgets #693 added name variables, which Redux_GUI does not draw, so
        // their legacy ids are the variable's occurrences instead (see KarpSatToSat3_VariableGadgets_...).
        foreach (var (name, red) in ReductionsWithGadgets().Where(r => r.Name == "KarpSATToSAT3"))
            foreach (Gadget g in red.gadgets.Where(g => g.sourceIds.All(id => id.StartsWith('c')) && g.targetIds.All(id => id.StartsWith('c')))) {
                Assert.Equal(g.reductionFromIds.Select(i => "c" + i), g.sourceIds);
                Assert.Equal(g.reductionToIds.Select(i => "c" + i), g.targetIds);
            }
    }

    // ---- what each stable id points to ----

    // Instances whose clause count differs from the literals per clause, so swapping clause and literal
    // index in an id points at a different literal (or none).
    public static IEnumerable<object[]> ThreeSatInstances() {
        yield return new object[] { "(x1 | x2 | x3) & (!x1 | !x2 | x4)" };
        yield return new object[] { "(x1 | x2 | x3) & (!x1 | x4 | !x5) & (x2 | !x3 | x5) & (!x4 | x1 | !x2)" };
        yield return new object[] { "(a | b) & (!a | c) & (b | !c) & (!b | a) & (c | a)" };
    }

    [Theory]
    [MemberData(nameof(ThreeSatInstances))]
    public void ThreeSatToClique_ElementIds_NameTheLiteralTheirNodeStandsFor(string instance) {
        var red = new SipserReduceToCliqueStandard(instance);
        var clauses = red.reductionFrom.clauses;
        var elements = red.gadgets.Where(g => g.gadgetKind == GadgetKind.Element).ToList();
        Assert.Equal(clauses.Sum(c => c.Count), elements.Count);
        foreach (Gadget g in elements) {
            var (clause, literal) = LiteralId(Assert.Single(g.sourceIds));
            string node = Assert.Single(g.targetIds);
            int u = node.LastIndexOf('_');
            Assert.Equal(clauses[clause][literal], node[..u]);
            Assert.Equal(clause.ToString(), node[(u + 1)..]);
        }
    }

    [Theory]
    [MemberData(nameof(ThreeSatInstances))]
    public void ThreeSatToClique_GroupIds_NameTheClauseTheirNodesComeFrom(string instance) {
        var red = new SipserReduceToCliqueStandard(instance);
        var groups = red.gadgets.Where(g => g.gadgetKind == GadgetKind.Group).ToList();
        Assert.Equal(red.reductionFrom.clauses.Count, groups.Count);
        foreach (Gadget g in groups) {
            int clause = ClauseId(Assert.Single(g.sourceIds));
            Assert.Equal(red.reductionFrom.clauses[clause].Select(l => l + "_" + clause), g.targetIds);
        }
    }

    // One instance per clause length: 1 and 2 are padded to 3, 3 is copied, 4 and 5 are split with fresh variables.
    [Theory]
    [InlineData("(x1) & (x2 | !x3)")]
    [InlineData("(x1 | !x2) & (x3)")]
    [InlineData("(x1 | x2 | !x3) & (!x1)")]
    [InlineData("(x1 | !x2 | x3 | x4) & (!x3 | x2)")]
    [InlineData("(x1 | !x2 | x3 | x4 | !x5) & (x2)")]
    [InlineData("(x1) & (x1 | x2) & (x1 | x2 | x3) & (x1 | x2 | x3 | !x4) & (!x1 | x2 | !x3 | x4 | x5)")]
    public void KarpSatToSat3_ElementIds_PointAtTheSameLiteralOnBothSides(string instance) {
        var red = new KarpSATToSAT3(instance);
        var sat = red.reductionFrom.clauses;
        var sat3 = red.reductionTo.clauses;
        var original = new HashSet<string>(sat.SelectMany(c => c).Select(l => l.TrimStart('!')));

        // Every SAT3 position holding an original variable is the target of exactly one element gadget, and
        // both ends name the same literal; positions holding a fresh variable belong to no element gadget.
        var targeted = new Dictionary<(int, int), int>();
        // The literal gadgets (one literal of SAT to its places in 3SAT); the variable gadgets of #693 are checked below.
        foreach (Gadget g in red.gadgets.Where(g => g.gadgetKind == GadgetKind.Element && g.sourceIds.All(id => System.Text.RegularExpressions.Regex.IsMatch(id, @"^c\d+-\d+$")))) {
            var (sc, sl) = LiteralId(Assert.Single(g.sourceIds));
            Assert.NotEmpty(g.targetIds);
            foreach (string id in g.targetIds) {
                var (tc, tl) = LiteralId(id);
                Assert.Equal(sat[sc][sl], sat3[tc][tl]);
                targeted[(tc, tl)] = targeted.GetValueOrDefault((tc, tl)) + 1;
            }
        }
        for (int c = 0; c < sat3.Count; c++) {
            Assert.Equal(3, sat3[c].Count);
            for (int l = 0; l < 3; l++) {
                int expected = original.Contains(sat3[c][l].TrimStart('!')) ? 1 : 0;
                Assert.True(expected == targeted.GetValueOrDefault((c, l)), $"c{c}-{l} ({sat3[c][l]}) is the target of {targeted.GetValueOrDefault((c, l))} element gadgets");
            }
        }

        // A clause's group covers the SAT3 clauses built from it: each holds only its literals or fresh variables.
        foreach (Gadget g in red.gadgets.Where(g => g.gadgetKind == GadgetKind.Group)) {
            int sc = ClauseId(Assert.Single(g.sourceIds));
            Assert.Contains("c" + sc, g.targetIds);
            foreach (string id in g.targetIds)
                Assert.All(sat3[ClauseId(id)], l => Assert.True(sat[sc].Contains(l) || !original.Contains(l.TrimStart('!')), $"{id} holds {l}, not from clause {sc}"));
        }
        Assert.Equal(sat.Count, red.gadgets.Count(g => g.gadgetKind == GadgetKind.Group));
    }

    private static (int Clause, int Literal) LiteralId(string id) {
        var m = System.Text.RegularExpressions.Regex.Match(id, @"^c(\d+)-(\d+)$");
        Assert.True(m.Success, $"'{id}' is not a literal id");
        return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    private static int ClauseId(string id) {
        var m = System.Text.RegularExpressions.Regex.Match(id, @"^c(\d+)$");
        Assert.True(m.Success, $"'{id}' is not a clause id");
        return int.Parse(m.Groups[1].Value);
    }
}
