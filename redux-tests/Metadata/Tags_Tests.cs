using System.Net;
using System.Reflection;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for tag sets (#914, #842): ProblemType, ProblemVariant, SolverType, ReductionType and
// VisualizationType are [Flags] enums, written as a list in enum order (problemTypes, solverTypes, ...)
// next to a deprecated single field that holds the first tag. See Interfaces/Tags.cs.
public class Tags_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Tags_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    public static TheoryData<Type> TagEnums() => new(Tags.Sets.Keys);

    // ── The enums themselves ──────────────────────────────────────────────────

    // Tags.Names lists tags by value, so value order must be declared order, and each member one bit.
    [Theory]
    [MemberData(nameof(TagEnums))]
    public void TagEnum_IsFlagsWithOneBitPerMemberInDeclaredOrder(Type tagEnum) {
        Assert.True(tagEnum.IsDefined(typeof(FlagsAttribute)), $"{tagEnum.Name} must be [Flags].");

        var values = tagEnum.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => Convert.ToUInt64(f.GetValue(null)))
            .ToList();
        Assert.Equal(0UL, values[0]);
        for (int i = 1; i < values.Count; i++) {
            Assert.True(ulong.IsPow2(values[i]), $"{tagEnum.Name} member {i} is {values[i]}, not a power of two.");
            Assert.True(values[i] > values[i - 1], $"{tagEnum.Name} members must be declared in ascending order.");
        }
    }

    // ── Tags.Names / Tags.First ───────────────────────────────────────────────

    [Fact]
    public void Names_AreInEnumOrderWhateverOrderTheFlagsAreWritten() {
        Assert.Equal(new[] { "Greedy", "Heuristic" }, Tags.Names(SolverType.Greedy | SolverType.Heuristic));
        Assert.Equal(new[] { "Greedy", "Heuristic" }, Tags.Names(SolverType.Heuristic | SolverType.Greedy));
        Assert.Equal(new[] { "Approximation", "Stochastic", "Parameterized" },
            Tags.Names(SolverType.Parameterized | SolverType.Approximation | SolverType.Stochastic));
    }

    [Fact]
    public void Names_OfAnEmptySetIsTheZeroMemberAlone() {
        Assert.Equal(new[] { "Unclassified" }, Tags.Names(SolverType.Unclassified));
        Assert.Equal(new[] { "Unclassified" }, Tags.Names(ProblemVariant.Unclassified));
        Assert.Equal(new[] { "Unimplemented" }, Tags.Names(VisualizationType.Unimplemented));
    }

    [Fact]
    public void First_IsTheFirstTagInEnumOrder() {
        Assert.Equal("Greedy", Tags.First(SolverType.Approximation | SolverType.Greedy));
        Assert.Equal("Restriction", Tags.First(ReductionType.ComponentDesign | ReductionType.Restriction));
        Assert.Equal("GraphTheory", Tags.First(ProblemType.Logic | ProblemType.GraphTheory));
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    private sealed class Tagged {
        public string name { get; init; } = "";
        public SolverType solverType { get; init; }
        public ProblemVariant problemVariant { get; init; }
        public int after { get; init; }
    }

    private static readonly JsonSerializerOptions WithLists = TagListJson.Apply(new JsonSerializerOptions());

    [Fact]
    public void Json_PutsTheListRightAfterTheSingleFieldAndDropsTheVariantSingleField() {
        var json = JsonSerializer.Serialize(new Tagged {
            name = "x",
            solverType = SolverType.Stochastic | SolverType.Heuristic,
            problemVariant = ProblemVariant.Optimization | ProblemVariant.Decision,
            after = 1,
        }, WithLists);

        Assert.Equal(
            """{"name":"x","solverType":"Heuristic","solverTypes":["Heuristic","Stochastic"],"problemVariants":["Decision","Optimization"],"after":1}""",
            json);
    }

    [Fact]
    public void Json_IsByteIdenticalForTheSameSetWrittenInEitherOrder() {
        var a = JsonSerializer.Serialize(new Tagged { solverType = SolverType.Greedy | SolverType.Approximation }, WithLists);
        var b = JsonSerializer.Serialize(new Tagged { solverType = SolverType.Approximation | SolverType.Greedy }, WithLists);
        Assert.Equal(a, b);
    }

    // The converter alone (no TagListJson) writes the first tag, never JsonStringEnumConverter's "A, B".
    [Fact]
    public void Json_SingleFieldIsTheFirstTagEvenWithoutTheListModifier() {
        var json = JsonSerializer.Serialize(new Tagged { solverType = SolverType.Stochastic | SolverType.Heuristic });
        Assert.Contains("\"solverType\":\"Heuristic\"", json);
        Assert.DoesNotContain("Heuristic, Stochastic", json);
    }

    [Fact]
    public void Json_ReadsASingleNameOrACommaJoinedSet() {
        Assert.Equal(SolverType.Greedy, JsonSerializer.Deserialize<SolverType>("\"Greedy\""));
        Assert.Equal(SolverType.Greedy | SolverType.Heuristic, JsonSerializer.Deserialize<SolverType>("\"Heuristic, Greedy\""));
    }

    // ── Live endpoints ────────────────────────────────────────────────────────

    private static readonly string[] SingleFields = { "problemType", "solverType", "reductionType", "visualizationType" };

    private static readonly Dictionary<string, Type> EnumOf = new() {
        ["problemType"] = typeof(ProblemType),
        ["problemVariant"] = typeof(ProblemVariant),
        ["solverType"] = typeof(SolverType),
        ["reductionType"] = typeof(ReductionType),
        ["visualizationType"] = typeof(VisualizationType),
    };

    // Every object in the tree that carries a tag: its single field (if any) is one name with no comma and
    // equals the list's first entry, and the list is non-empty, known names, strictly in enum order.
    private static void CheckTags(JsonElement element, string path, List<string> failures, ref int checkedLists) {
        if (element.ValueKind == JsonValueKind.Array) {
            int i = 0;
            foreach (var item in element.EnumerateArray()) CheckTags(item, $"{path}[{i++}]", failures, ref checkedLists);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var (single, type) in EnumOf) {
            bool hasSingle = element.TryGetProperty(single, out var singleValue);
            if (hasSingle && singleValue.ValueKind == JsonValueKind.String && singleValue.GetString()!.Contains(','))
                failures.Add($"{path}.{single} = \"{singleValue.GetString()}\" contains a comma.");
            if (single == "problemVariant" && hasSingle)
                failures.Add($"{path} has a single problemVariant field; it is written only as problemVariants.");
            if (!element.TryGetProperty(single + "s", out var list)) continue;

            checkedLists++;
            var names = list.EnumerateArray().Select(n => n.GetString()!).ToList();
            var order = Enum.GetNames(type).ToList();
            if (names.Count == 0 || names.Any(n => !order.Contains(n)))
                failures.Add($"{path}.{single}s = [{string.Join(", ", names)}] is empty or has an unknown name.");
            else if (!names.Zip(names.Skip(1)).All(p => order.IndexOf(p.First) < order.IndexOf(p.Second)))
                failures.Add($"{path}.{single}s = [{string.Join(", ", names)}] is not in enum order.");
            if (hasSingle && names.Count > 0 && singleValue.GetString() != names[0])
                failures.Add($"{path}.{single} = \"{singleValue.GetString()}\" but {single}s starts with \"{names[0]}\".");
        }

        foreach (var property in element.EnumerateObject())
            CheckTags(property.Value, $"{path}.{property.Name}", failures, ref checkedLists);
    }

    private async Task<JsonDocument> GetJson(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/Navigation/Batch/allInfo")]
    [InlineData("/Navigation/Reductions")]
    public async Task Endpoint_TagListsMatchTheirSingleFieldsAndNeverHoldACommaJoinedValue(string url) {
        using var doc = await GetJson(url);
        var failures = new List<string>();
        int checkedLists = 0;
        CheckTags(doc.RootElement, "", failures, ref checkedLists);

        Assert.True(checkedLists > 0, $"Found no tag lists in {url}.");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public async Task Info_HasEveryTagListOfAProblemAndItsDefaults() {
        using var doc = await GetJson("/ProblemProvider/info?interface=VERTEXCOVER");
        var root = doc.RootElement;
        Assert.Equal("GraphTheory", root.GetProperty("problemTypes")[0].GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("problemVariants").ValueKind);
        Assert.False(root.TryGetProperty("problemVariant", out _));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("defaultSolver").GetProperty("solverTypes").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("defaultVisualization").GetProperty("visualizationTypes").ValueKind);
    }

    // A multi-tag solver from #842: the list holds both, the single field the first in enum order.
    [Fact]
    public async Task Info_MultiTagSolverListsEveryTypeAndKeepsTheFirstAsTheSingleField() {
        using var doc = await GetJson("/ProblemProvider/info?interface=BinPackingFFD");
        var root = doc.RootElement;
        Assert.Equal(new[] { "Greedy", "Approximation" },
            root.GetProperty("solverTypes").EnumerateArray().Select(n => n.GetString()));
        Assert.Equal("Greedy", root.GetProperty("solverType").GetString());
    }

    // DummySolver leaves solverType to ISolver's default member, which System.Text.Json does not write;
    // the list is still there.
    [Fact]
    public async Task Info_ListIsWrittenForATagLeftToTheInterfaceDefault() {
        using var doc = await GetJson("/ProblemProvider/info?interface=DummySolver");
        Assert.Equal(new[] { "Unclassified" },
            doc.RootElement.GetProperty("solverTypes").EnumerateArray().Select(n => n.GetString()));
    }

    [Fact]
    public async Task Reductions_EdgesCarryReductionTypes() {
        using var doc = await GetJson("/Navigation/Reductions");
        var edge = doc.RootElement.GetProperty("VERTEXCOVER").GetProperty("SETCOVER")[0];
        Assert.Equal(edge.GetProperty("reductionType").GetString(), edge.GetProperty("reductionTypes")[0].GetString());
    }

    [Fact]
    public async Task ProblemVariants_VocabularyEndpointListsEveryMember() {
        using var doc = await GetJson("/Navigation/ProblemVariants");
        Assert.Equal(new[] { "Decision", "Optimization", "Unclassified" },
            doc.RootElement.EnumerateArray().Select(n => n.GetString()));
    }

    // The single-value catalogs behind the navigation endpoints use the same rule as the JSON.
    [Fact]
    public void Catalogs_SingleValueIsTheFirstTag() {
        Assert.Equal("Greedy", SolverTypeCatalog.SolverTypeByClassName.Value["BinPackingFFD"]);
        Assert.Equal(new[] { "Greedy", "Approximation" }, SolverTypeCatalog.SolverTypesByClassName.Value["BinPackingFFD"]);
        Assert.DoesNotContain(SolverTypeCatalog.SolverTypeByClassName.Value.Values, v => v.Contains(','));
        Assert.DoesNotContain(ReductionTypeCatalog.ReductionTypeByClassName.Value.Values, v => v.Contains(','));
        Assert.DoesNotContain(VisualizationTypeCatalog.ByClassName.Value.Values, v => v.Contains(','));
    }
}
