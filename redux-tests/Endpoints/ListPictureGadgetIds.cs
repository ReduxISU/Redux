using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

/// <summary>
/// What a frontend that draws the list-format pictures sees for a reduction (Redux_Frontend's workbench, Redux_GUI): the
/// source problem's default visualization from /visualize, the target from /visualizeReduction, and the gadgets from /reduce,
/// all on the reduction's own default instance. <see cref="Ids"/> reads a picture's ids the way Redux_Frontend's
/// lib/gadgets.js frameIds does, so a test can check a gadget's ids name pieces drawn on these pictures.
/// </summary>
internal static class ListPictureGadgetIds {
    public sealed record Pictures(string Instance, List<JsonElement> Source, List<JsonElement> Target, List<JsonElement> Gadgets);

    /// <summary>Every reduction whose default instance has gadgets, by class name.</summary>
    public static IEnumerable<(string Name, IReduction Reduction)> ReductionsWithGadgets() {
        foreach (var (_, type) in ProblemProvider.Reductions.OrderBy(p => p.Key, StringComparer.Ordinal)) {
            IReduction red;
            try { red = (IReduction)Activator.CreateInstance(type)!; } catch { continue; }
            if (red.gadgets.Count > 0) yield return (type.Name, red);
        }
    }

    public static async Task<Pictures> Fetch(HttpClient client, IReduction red, string reduction) {
        string instance = red.reductionFrom.instance;
        string visualization = red.reductionFrom.defaultVisualization.GetType().Name;
        string solver = red.reductionFrom.defaultSolver.GetType().Name;

        var source = Frames(await Post(client, $"visualize?visualization={visualization}", instance));
        string solution = await Post(client, $"solve?solver={solver}", instance);
        try { solution = JsonSerializer.Deserialize<string>(solution) ?? solution; } catch (JsonException) { }
        var target = Frames(await Post(client, $"visualizeReduction?reduction={reduction}&solution={Uri.EscapeDataString(solution)}", instance));
        using var reduced = JsonDocument.Parse(await Post(client, $"reduce?reduction={reduction}", instance));
        var gadgets = reduced.RootElement.GetProperty("gadgets").EnumerateArray().Select(g => g.Clone()).ToList();
        return new Pictures(instance, source, target, gadgets);
    }

    /// <summary>
    /// The ids a picture names, as lib/gadgets.js frameIds reads them: every node's id and name, every link's id and both
    /// "a-b" orders of its ends, and every other <c>id</c> anywhere in the payload.
    /// </summary>
    public static HashSet<string> Ids(IEnumerable<JsonElement> frames) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement frame in frames) {
            if (frame.ValueKind != JsonValueKind.Object) continue;
            if (frame.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
                foreach (var node in nodes.EnumerateArray()) {
                    if (node.ValueKind != JsonValueKind.Object) continue;
                    foreach (string key in new[] { "id", "name" })
                        if (node.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) ids.Add(v.GetString()!);
                }
            if (frame.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
                foreach (var link in links.EnumerateArray()) {
                    if (link.ValueKind != JsonValueKind.Object) continue;
                    if (link.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) ids.Add(id.GetString()!);
                    string? a = End(link, "source"), b = End(link, "target");
                    if (a != null && b != null) {
                        ids.Add($"{a}-{b}");
                        ids.Add($"{b}-{a}");
                    }
                }
            var stack = new Stack<JsonElement>();
            stack.Push(frame);
            while (stack.Count > 0) {
                JsonElement value = stack.Pop();
                if (value.ValueKind == JsonValueKind.Array) {
                    foreach (var item in value.EnumerateArray()) stack.Push(item);
                } else if (value.ValueKind == JsonValueKind.Object) {
                    if (value.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) ids.Add(id.GetString()!);
                    foreach (var property in value.EnumerateObject()) stack.Push(property.Value);
                }
            }
        }
        return ids;
    }

    public static IEnumerable<string> Strings(JsonElement gadget, string list) =>
        gadget.GetProperty(list).EnumerateArray().Select(e => e.GetString()!);

    private static string? End(JsonElement link, string key) {
        if (!link.TryGetProperty(key, out var end)) return null;
        if (end.ValueKind == JsonValueKind.String) return end.GetString();
        if (end.ValueKind == JsonValueKind.Object && end.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) return id.GetString();
        return null;
    }

    // The list format is a JSON array of frames; a frame may come as an object or as a JSON string holding one.
    private static List<JsonElement> Frames(string body) {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray()
            .Select(f => f.ValueKind == JsonValueKind.String ? JsonDocument.Parse(f.GetString()!).RootElement.Clone() : f.Clone())
            .ToList();
    }

    private static async Task<string> Post(HttpClient client, string path, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/ProblemProvider/" + path, content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return body;
    }
}
