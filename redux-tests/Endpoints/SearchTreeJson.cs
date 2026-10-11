using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The checks every Search Tree endpoint test makes on the real format=frames JSON, whatever the problem: the payload, the
// fields of every frame, a well-formed tree in each (unique ids, parents first, the shared states, the focus drawn, totals
// that add up), and the answer's green path.
public static class SearchTreeJson {
    public static readonly HashSet<string> Vocabulary = ["Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked"];
    public static readonly HashSet<string> Kinds = ["queued", "explored", "pruned", "failed", "solution", "skipped", "more", "cut", "hidden"];

    public static string Id(JsonElement node) => node.GetProperty("id").GetString()!;
    public static string Kind(JsonElement node) => node.GetProperty("kind").GetString()!;
    public static string State(JsonElement node) => node.GetProperty("state").GetString()!;
    public static string? Parent(JsonElement node) => node.GetProperty("parent").ValueKind == JsonValueKind.Null ? null : node.GetProperty("parent").GetString();

    /// <summary>Checks the response's shape and payload and every frame; returns the frames, the last being the only Done.</summary>
    public static List<JsonElement> Check(string body, string problem, string instance) {
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("SearchTree", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "problem", "instance", "maxLeaves"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("searchTree", payload.GetProperty("kind").GetString());
        Assert.Equal(problem, payload.GetProperty("problem").GetString());
        Assert.Equal(instance, payload.GetProperty("instance").GetString());
        Assert.Equal(12, payload.GetProperty("maxLeaves").GetInt32());

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        foreach (var f in frames) CheckFrame(f);
        return frames;
    }

    public static void CheckFrame(JsonElement f) {
        bool isDone = f.GetProperty("event").GetString() == "Done";
        Assert.Equal(["event", "caption", "focus", "nodes", "total", "cut", .. isDone ? new[] { "ok" } : []], f.EnumerateObject().Select(p => p.Name));
        Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
        var nodes = f.GetProperty("nodes").EnumerateArray().ToList();
        var ids = new HashSet<string>();
        foreach (var n in nodes) {
            string id = Id(n);
            Assert.True(ids.Add(id), $"{id} twice");
            Assert.Matches(@"^n\d+(\.more|\.cut)?$", id);
            if (Parent(n) is { } parent) Assert.Contains(parent, ids);   // parents come first
            else Assert.Equal("n0", id);
            Assert.Contains(Kind(n), Kinds);
            Assert.Contains(State(n), Vocabulary);
            if (Kind(n) is "more" or "cut") {
                Assert.Equal(Parent(n) + "." + Kind(n), id);
                Assert.True(n.GetProperty("hidden").GetInt32() > 1);
            } else {
                Assert.False(n.TryGetProperty("hidden", out _));
            }
        }
        // Past 12 leaves something must hold the tree open: a queued node, or the path to the focus or the answer.
        int leaves = nodes.Count(n => !nodes.Any(k => Parent(k) == Id(n)));
        if (leaves > 12)
            Assert.Contains(nodes, n => Kind(n) == "queued" || (Kind(n) == "cut") || State(n) is "ElementHighlight" or "Solution");
        var focus = f.GetProperty("focus").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.All(focus, id => Assert.Contains(id, ids));
        if (isDone) Assert.Empty(focus);
        Assert.True(focus.Count <= 1);
        Assert.Equal(nodes.Count(n => Kind(n) is not ("more" or "cut")) + nodes.Sum(n => n.TryGetProperty("hidden", out var h) ? h.GetInt32() : 0),
            f.GetProperty("total").GetInt32());
    }

    /// <summary>The Solution nodes of a frame, checked to be one chain from the root down to a solution node (or none).</summary>
    public static List<JsonElement> GreenPath(JsonElement frame) {
        var green = frame.GetProperty("nodes").EnumerateArray().Where(n => State(n) is "Solution").ToList();
        if (green.Count == 0) return green;
        Assert.Equal("n0", Id(green[0]));
        for (int i = 1; i < green.Count; i++) Assert.Equal(Id(green[i - 1]), Parent(green[i]));
        return green;
    }
}
