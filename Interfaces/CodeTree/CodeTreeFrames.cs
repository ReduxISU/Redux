using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.CodeTree;

/// <summary>
/// What a Lossless Data Compression instance hands the Code Tree picture: the text, as written. The base derives its
/// characters (UTF-16 code units, as the solver counts them) and how often each appears. No ids, no states.
/// </summary>
sealed record CodeTreeSpec(string Text);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>
/// A character of the text: its id (<c>c1</c>, in character order), its UTF-16 code (as the certificate writes it: 97 for
/// <c>a</c>; a number, because half of a surrogate pair cannot be sent as a JSON string on its own), a label that is safe to
/// draw (<c>␣</c> for a space, <c>↵</c> for a line break, <c>⇥</c> for a tab, <c>U+XXXX</c> for any other control or lone
/// surrogate), and how often it appears.
/// </summary>
sealed record CodeTreeSymbolDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("count")] int Count);

/// <summary>The static part of a Code Tree picture, sent once per response: the text and its characters, the tree's leaves.</summary>
sealed record CodeTreePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("symbols")] IReadOnlyList<CodeTreeSymbolDto> Symbols);

/// <summary>A merged node of the tree: its id (<c>n1</c>, the first merge), its weight, and its children: left for bit 0,
/// right for bit 1.</summary>
sealed record CodeTreeNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("weight")] int Weight,
    [property: JsonPropertyName("left")] string Left,
    [property: JsonPropertyName("right")] string Right);

/// <summary>
/// One picture of the tree.
/// <list type="bullet">
/// <item><c>merged</c>: the merged nodes built so far, in the order they were built.</item>
/// <item><c>roots</c>: the trees not merged yet, lightest first, in the order the solver picks them (weight, then the tree
/// holding the earliest character, then the oldest).</item>
/// <item><c>codes</c>: each character's code, by its id, once the tree is read; empty until then.</item>
/// <item><c>bits</c>: the length of the encoded text, once the codes are known.</item>
/// <item><c>nodes</c>: the state of each leaf and merged node whose state is not <see cref="PictureState.Background"/>.</item>
/// <item><c>edges</c>: the state of each edge (<c>{node}.0</c>, <c>{node}.1</c>) whose state is not Background.</item>
/// </list>
/// </summary>
sealed record CodeTreeFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("merged")] IReadOnlyList<CodeTreeNodeDto> Merged,
    [property: JsonPropertyName("roots")] IReadOnlyList<string> Roots,
    [property: JsonPropertyName("codes")] IReadOnlyDictionary<string, string> Codes,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("bits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Bits { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Code Tree picture (Lossless Data Compression) from (spec, a step's <see cref="HuffmanForest"/>, step event,
/// focus). Nothing here knows about a particular solver.
/// <para>
/// <b>Ids.</b> The i-th distinct character in character order (from 1) is the leaf <c>c{i}</c>; the k-th merge makes the node
/// <c>n{k}</c>; a node's edges are <c>{node}.0</c> (left) and <c>{node}.1</c> (right). Ids are positions, never characters,
/// so no id needs escaping, whatever the text holds.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>; see
/// <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>A merged node, its children and its two edges are <c>Committed</c> in every step of an incremental run (what the
/// solver has committed so far) and in an Accept the run goes on from (#886), <c>Solution</c> only in the confirmed answer
/// (a correct Done, or an Accept that ends the run), and <c>ElementHighlight</c> while a whole candidate is only tried
/// (<see cref="GraphSubsetFrames.Picked"/>). Leaves no merge has used yet are <c>Background</c>.</item>
/// <item>Once the codes are known, every leaf that has one takes the same state (a one-character text has no merge).</item>
/// <item>The focus (the two trees being merged and their new parent) is <c>ElementHighlight</c>, with the edges between focused
/// nodes (the parent's two edges); in a Reject step of an incremental run it is <c>Rejected</c> (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>In a Done frame with ok = false everything drawn is <c>Rejected</c> (the same rule).</item>
/// <item>A run stopped by its time limit ends with a blank Done frame, ok = false (<see cref="StoppedFrame"/>).</item>
/// </list>
/// Covered, Blocked and Untraveled are not used.
/// </summary>
static class CodeTreeFrames {
    public const string Kind = "codeTree";

    // ---- Ids ----

    /// <summary><c>c{i+1}</c> for the i-th distinct character, in character order.</summary>
    public static string LeafId(int index) => $"c{index + 1}";

    /// <summary><c>n{k}</c> for the k-th merge (from 1).</summary>
    public static string NodeId(int merge) => $"n{merge}";

    /// <summary>The id of a node's edge to its left (bit 0) or right (bit 1) child.</summary>
    public static string EdgeId(string node, char bit) => $"{node}.{bit}";

    /// <summary>The text's distinct characters in character order, with how often each appears.</summary>
    public static List<(char Symbol, int Count)> Symbols(CodeTreeSpec spec) =>
        spec.Text.GroupBy(c => c).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();

    /// <summary>A character as it can be drawn and captioned: <c>␣</c>, <c>↵</c>, <c>⇥</c>, or <c>U+XXXX</c> for a control or a
    /// lone surrogate; the character itself otherwise.</summary>
    public static string Label(char c) => c switch {
        ' ' => "␣",
        '\n' => "↵",
        '\t' => "⇥",
        _ when char.IsControl(c) || char.IsSurrogate(c) => $"U+{(int)c:X4}",
        _ => c.ToString(),
    };

    // ---- The payload ----

    public static CodeTreePayload Payload(CodeTreeSpec spec) =>
        new(Kind, spec.Text, Symbols(spec).Select((s, i) => new CodeTreeSymbolDto(LeafId(i), s.Symbol, Label(s.Symbol), s.Count)).ToList());

    // ---- Frames ----

    /// <summary>
    /// The trees not merged yet after <paramref name="merges"/>, in the order the solver picks them: lightest first, then the
    /// tree holding the earliest character, then the oldest (leaves before merged nodes, merged nodes in the order made).
    /// </summary>
    public static List<string> Roots(CodeTreeSpec spec, IReadOnlyList<HuffmanMerge> merges) {
        var symbols = Symbols(spec);
        var weight = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowest = new Dictionary<string, int>(StringComparer.Ordinal);
        var serial = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < symbols.Count; i++) {
            weight[LeafId(i)] = symbols[i].Count;
            lowest[LeafId(i)] = i;
            serial[LeafId(i)] = i;
        }
        var roots = new HashSet<string>(weight.Keys, StringComparer.Ordinal);
        foreach (var m in merges) {
            if (!weight.ContainsKey(m.Left) || !weight.ContainsKey(m.Right) || weight.ContainsKey(m.Node)) continue;
            weight[m.Node] = m.Weight;
            lowest[m.Node] = Math.Min(lowest[m.Left], lowest[m.Right]);
            serial[m.Node] = serial.Count;
            roots.Remove(m.Left);
            roots.Remove(m.Right);
            roots.Add(m.Node);
        }
        return roots.OrderBy(r => weight[r]).ThenBy(r => lowest[r]).ThenBy(r => serial[r]).ToList();
    }

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static CodeTreeFrame Frame(CodeTreeSpec spec, HuffmanForest partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var symbols = Symbols(spec);
        var leaves = new HashSet<string>(Enumerable.Range(0, symbols.Count).Select(LeafId), StringComparer.Ordinal);
        // Merges whose children are trees of this text, each used once.
        var merged = new List<CodeTreeNodeDto>();
        var known = new HashSet<string>(leaves, StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in partial.Merges) {
            if (!known.Contains(m.Left) || !known.Contains(m.Right) || m.Left == m.Right || known.Contains(m.Node)
                || used.Contains(m.Left) || used.Contains(m.Right)) continue;
            merged.Add(new CodeTreeNodeDto(m.Node, m.Weight, m.Left, m.Right));
            known.Add(m.Node);
            used.Add(m.Left);
            used.Add(m.Right);
        }
        var drawn = merged.Select(m => new HuffmanMerge(m.Id, m.Left, m.Right, m.Weight)).ToList();
        var codes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, code) in partial.Codes)
            if (leaves.Contains(id)) codes[id] = code;

        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        foreach (var m in merged) {
            foreach (var id in new[] { m.Id, m.Left, m.Right }) GraphSubsetFrames.Raise(nodes, id, picked);
            GraphSubsetFrames.Raise(edges, EdgeId(m.Id, '0'), picked);
            GraphSubsetFrames.Raise(edges, EdgeId(m.Id, '1'), picked);
        }
        foreach (var id in codes.Keys) GraphSubsetFrames.Raise(nodes, id, picked);

        // The focus, and the edges between focused nodes (a new parent and the two trees it joins).
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        var focused = new HashSet<string>(focus.Where(known.Contains), StringComparer.Ordinal);
        foreach (var id in focused) GraphSubsetFrames.Raise(nodes, id, mark);
        foreach (var m in merged.Where(m => focused.Contains(m.Id))) {
            if (focused.Contains(m.Left)) GraphSubsetFrames.Raise(edges, EdgeId(m.Id, '0'), mark);
            if (focused.Contains(m.Right)) GraphSubsetFrames.Raise(edges, EdgeId(m.Id, '1'), mark);
        }

        var counts = symbols.Select((s, i) => (Id: LeafId(i), s.Count)).ToDictionary(p => p.Id, p => p.Count, StringComparer.Ordinal);
        long? bits = codes.Count > 0 && codes.Count == symbols.Count ? codes.Sum(c => (long)c.Value.Length * counts[c.Key]) : null;
        return new CodeTreeFrame(ev, caption, focus, merged, Roots(spec, drawn), codes,
            GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edges)) { Bits = bits, Ok = ok };
    }

    /// <summary>The blank Done frame, ok = false, that closes a run stopped by its time limit: no tree, no codes.</summary>
    public static CodeTreeFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], [], [], new SortedDictionary<string, string>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    /// <summary>
    /// A Lossless Data Compression certificate (<c>({(97,0),(98,10),(99,11)},01011)</c>: each character's code by its character
    /// code, then the encoded bits) as codes by leaf id, with no merges (the certificate has none). Anything that does not
    /// give a code to characters of this text is no codes.
    /// </summary>
    public static HuffmanForest ParseCodes(CodeTreeSpec spec, string answer) {
        var none = new HuffmanForest([], []);
        string s = answer.Trim();
        int close = s.LastIndexOf('}');
        if (!s.StartsWith("({", StringComparison.Ordinal) || close < 0) return none;
        var ids = Symbols(spec).Select((x, i) => (x.Symbol, Id: LeafId(i))).ToDictionary(p => p.Symbol, p => p.Id);
        var codes = new List<KeyValuePair<string, string>>();
        string table = s[2..close];
        if (table.Length == 0) return none;
        foreach (var entry in table.Split("),(")) {
            var parts = entry.Trim('(', ')').Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int code)
                || code > char.MaxValue || !ids.TryGetValue((char)code, out var id)) return none;
            codes.Add(new(id, parts[1]));
        }
        return new HuffmanForest([], codes);
    }

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last step
    /// already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its answer
    /// string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(CodeTreeSpec spec, SolveRun run, Func<string, HuffmanForest> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<HuffmanForest>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<HuffmanForest>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(spec, ok ? parseAnswer(run.Answer) : new HuffmanForest([], []), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.CodeTree), Payload(spec), frames);
    }

    // ---- Captions ----

    /// <summary>A number as captions write it, with thousands separators (1,234).</summary>
    public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
}

/// <summary>
/// A visualization for a problem drawn as the Code Tree picture. The visualization declares only what the instance is
/// (<see cref="Describe"/>) and how to read an answer as codes (<see cref="ParseAnswer"/>); the frames hook then comes for
/// free. Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(HuffmanForest)</c>.
/// </summary>
interface ICodeTreeVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    CodeTreeSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as codes (used only when the run's steps cannot be drawn).</summary>
    HuffmanForest ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return CodeTreeFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
