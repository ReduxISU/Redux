using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using SPADE;

namespace API.Interfaces.Packing;

/// <summary>Which question a packing problem asks of its containers; it decides the containers and their lines.</summary>
enum PackingRule {
    /// <summary>Two sides, S1 and S2, that must each hold half of the total.</summary>
    Partition,
    /// <summary>One container whose items must add up to exactly the target T.</summary>
    SubsetSum,
    /// <summary>One knapsack whose items must weigh at most W and be worth at least V.</summary>
    Knapsack,
    /// <summary>K bins that each hold at most C.</summary>
    BinPacking,
}

/// <summary>One item as the instance lists it: its size (a weight for Knapsack) and, for Knapsack only, its value.</summary>
/// <param name="Size">The item's size or weight.</param>
/// <param name="Value">Its value (Knapsack), else null.</param>
/// <param name="Label">How the instance writes it (<c>4</c>, <c>(10,60)</c>).</param>
sealed record PackingItemInput(int Size, int? Value, string Label);

/// <summary>
/// What a packing problem hands the Packing picture: its items in instance order, its rule and the numbers of its rule.
/// No containers, no ids, no colors; the base derives those.
/// </summary>
/// <param name="Items">The items, in instance order. Two items with the same numbers are two items.</param>
/// <param name="Rule">The question asked.</param>
sealed record PackingSpec(IReadOnlyList<PackingItemInput> Items, PackingRule Rule) {
    /// <summary>Subset Sum's target T.</summary>
    public int? Target { get; init; }
    /// <summary>Knapsack's weight limit W, Bin Packing's capacity C.</summary>
    public int? Limit { get; init; }
    /// <summary>Knapsack's value goal V.</summary>
    public int? AtLeast { get; init; }
    /// <summary>Bin Packing's bin limit K.</summary>
    public int? Bins { get; init; }
}

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>An item: its id (<c>I1</c>, by position), its label as the instance writes it, its size and (Knapsack) its value.</summary>
sealed record PackingItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("size")] int Size) {
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Value { get; init; }
}

/// <summary>
/// A container: its id (<c>C1</c>, by position), its label (<c>S1</c>, <c>Chosen</c>, <c>Knapsack</c>, <c>Bin 1</c>) and its
/// line(s). <c>target</c>: the sizes must add up to exactly this (Partition's half, which can end in .5, and Subset Sum's T).
/// <c>limit</c>: the sizes may add up to at most this (Knapsack's W, Bin Packing's C). <c>atLeast</c>: the values must add
/// up to at least this (Knapsack's V). Each is present only when it applies.
/// </summary>
sealed record PackingContainerDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label) {
    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Target { get; init; }

    [JsonPropertyName("limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; init; }

    [JsonPropertyName("atLeast")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AtLeast { get; init; }
}

/// <summary>
/// The static part of a Packing picture, sent once per response. <c>total</c> is the sum of every item's size.
/// <c>maxBins</c> (Bin Packing only) is the instance's K; <c>containers</c> holds one bin per item at most, since no
/// packing fills more bins than it has items.
/// </summary>
sealed record PackingPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("items")] IReadOnlyList<PackingItemDto> Items,
    [property: JsonPropertyName("containers")] IReadOnlyList<PackingContainerDto> Containers,
    [property: JsonPropertyName("total")] int Total) {
    [JsonPropertyName("maxBins")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxBins { get; init; }
}

/// <summary>What a container holds in one frame: the sum of its items' sizes and, for Knapsack, of their values.</summary>
sealed record PackingTotal([property: JsonPropertyName("size")] int Size) {
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Value { get; init; }
}

/// <summary>
/// One picture of the packing. <c>items</c> and <c>containers</c> list only elements whose state is not
/// <see cref="PictureState.Background"/>; an id that is absent is Background. <c>assign</c> has one entry per item: the
/// 0-based index of its container in the payload, or -1 when it is left out (the frontend draws group colors for
/// Partition's sides and Bin Packing's bins from it: an annotation, not a state). <c>totals</c> has one entry per container.
/// </summary>
sealed record PackingFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("items")] IReadOnlyDictionary<string, StateRef> Items,
    [property: JsonPropertyName("containers")] IReadOnlyDictionary<string, StateRef> Containers,
    [property: JsonPropertyName("assign")] IReadOnlyList<int> Assign,
    [property: JsonPropertyName("totals")] IReadOnlyList<PackingTotal> Totals) {
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Packing picture (Partition, Subset Sum, Knapsack, Bin Packing) from (items, rule, a step's
/// <see cref="Placement"/>, step event, focus). Nothing here knows about a particular problem or solver.
/// <para>
/// <b>Ids.</b> Item <c>i</c> (0-based, instance order) is <c>I{i+1}</c>, container <c>c</c> is <c>C{c+1}</c>. Ids are
/// positions, never numbers or names, so two items of the same size are two items, no id needs escaping and no item id
/// equals a container id.
/// </para>
/// <para>
/// <b>Containers</b> come from the rule: Partition has S1 and S2, each with the target half the total; Subset Sum one
/// container, Chosen, with the target T; Knapsack one knapsack with the limit W (on sizes, the weights) and the goal V (on
/// values); Bin Packing one bin per item at most (and at most K), each with the limit C.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Covered &gt; Blocked &gt; Background</c>,
/// with <c>Untraveled</c> only where nothing else applies; see <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>An item in a container is <c>ElementHighlight</c> while a whole candidate is only being tried (Try, Reject of a
/// run that is not <see cref="SolveRun.Incremental"/>) and <c>Solution</c> otherwise: in an Accept, after a Backtrack, in
/// every step of an incremental run (what it has committed) and in Done (<see cref="GraphSubsetFrames.Picked"/>).</item>
/// <item>An item left out of a correct answer (a Done frame with ok = true) is <c>Untraveled</c>: not part of the
/// answer.</item>
/// <item>A container over its limit is <c>Rejected</c> in every frame. A container whose constraint is met is
/// <c>Covered</c>: a target hit exactly; a knapsack within W and worth at least V; a bin that holds something and is within
/// C. A target missed or a value short of V is <c>Rejected</c> only when a whole placement is judged (Done, or a Try or
/// Reject that is not part of an answer still being built) and something is placed: while an answer is built, and for an
/// empty placement (no answer), nothing has failed yet.</item>
/// <item>The focus is <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed (an item no bin has
/// room for), so <c>Rejected</c> (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>A run stopped by its time limit ends with a Done frame, ok = false, every element Background and nothing placed
/// (<see cref="StoppedFrame"/>).</item>
/// </list>
/// </summary>
static class PackingFrames {
    public const string Kind = "packing";

    // ---- Ids ----

    /// <summary><c>I{i+1}</c> for the item at 0-based position <paramref name="index"/>.</summary>
    public static string ItemId(int index) => $"I{index + 1}";

    /// <summary><c>C{c+1}</c> for the container at 0-based position <paramref name="index"/>.</summary>
    public static string ContainerId(int index) => $"C{index + 1}";

    public static string RuleName(PackingRule rule) => rule switch {
        PackingRule.Partition => "partition",
        PackingRule.SubsetSum => "subsetSum",
        PackingRule.Knapsack => "knapsack",
        PackingRule.BinPacking => "binPacking",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    // ---- The containers a spec has ----

    private sealed record Container(string Label, double? Target, int? Limit, int? AtLeast);

    private static List<Container> Containers(PackingSpec spec) {
        int total = spec.Items.Sum(i => i.Size);
        switch (spec.Rule) {
            case PackingRule.Partition:
                return [new("S1", total / 2.0, null, null), new("S2", total / 2.0, null, null)];
            case PackingRule.SubsetSum:
                return [new("Chosen", spec.Target ?? 0, null, null)];
            case PackingRule.Knapsack:
                return [new("Knapsack", null, spec.Limit ?? 0, spec.AtLeast ?? 0)];
            case PackingRule.BinPacking:
                int bins = Math.Min(Math.Max(spec.Bins ?? 0, 0), spec.Items.Count);
                return Enumerable.Range(0, bins).Select(b => new Container($"Bin {b + 1}", null, spec.Limit ?? 0, null)).ToList();
            default:
                throw new ArgumentOutOfRangeException(nameof(spec));
        }
    }

    // ---- The payload ----

    public static PackingPayload Payload(PackingSpec spec) =>
        new(Kind, RuleName(spec.Rule),
            spec.Items.Select((item, i) => new PackingItemDto(ItemId(i), item.Label, item.Size) { Value = item.Value }).ToList(),
            Containers(spec).Select((c, i) => new PackingContainerDto(ContainerId(i), c.Label) { Target = c.Target, Limit = c.Limit, AtLeast = c.AtLeast }).ToList(),
            spec.Items.Sum(i => i.Size)) {
            MaxBins = spec.Rule == PackingRule.BinPacking ? spec.Bins ?? 0 : null,
        };

    // ---- Frames ----

    // The placement as one container index per item: -1 for an item left out or put in a container the picture lacks.
    private static int[] AssignOf(PackingSpec spec, int containers, Placement placement) =>
        Enumerable.Range(0, spec.Items.Count).Select(i => placement.Of(i) is int c && c < containers ? c : -1).ToArray();

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>).
    /// </summary>
    public static PackingFrame Frame(PackingSpec spec, Placement partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false) {
        var containers = Containers(spec);
        int[] assign = AssignOf(spec, containers.Count, partial);
        var totals = containers.Select((_, c) => {
            var inside = spec.Items.Where((_, i) => assign[i] == c).ToList();
            return new PackingTotal(inside.Sum(i => i.Size)) { Value = spec.Rule == PackingRule.Knapsack ? inside.Sum(i => i.Value ?? 0) : null };
        }).ToList();

        var items = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var boxes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        // An incremental run's Try and Reject are part of an answer still being built (a Reject there turns down one piece, as a
        // backward map does when it leaves an item out), so nothing has missed yet, as the rule above says.
        bool inProgress = ev is StepEvent.Accept or StepEvent.Backtrack || (incremental && ev is StepEvent.Try or StepEvent.Reject);
        bool placedAny = assign.Any(c => c >= 0);

        var picked = GraphSubsetFrames.Picked(ev, incremental);
        for (int i = 0; i < assign.Length; i++) {
            if (assign[i] >= 0) GraphSubsetFrames.Raise(items, ItemId(i), picked);
            else if (ev == StepEvent.Done && ok == true) GraphSubsetFrames.Raise(items, ItemId(i), PictureState.Untraveled);
        }

        for (int c = 0; c < containers.Count; c++) {
            var box = containers[c];
            int size = totals[c].Size, value = totals[c].Value ?? 0;
            bool holds = assign.Any(a => a == c);
            PictureState state;
            if (box.Limit is int limit && size > limit) state = PictureState.Rejected;
            else if (box.Target is double target) state = size == target ? PictureState.Covered : Missed();
            else if (box.AtLeast is int atLeast) state = value >= atLeast ? PictureState.Covered : Missed();
            else state = holds ? PictureState.Covered : PictureState.Background;
            GraphSubsetFrames.Raise(boxes, ContainerId(c), state);
        }
        PictureState Missed() => !inProgress && placedAny ? PictureState.Rejected : PictureState.Background;

        var itemIds = new HashSet<string>(Enumerable.Range(0, spec.Items.Count).Select(ItemId), StringComparer.Ordinal);
        var boxIds = new HashSet<string>(Enumerable.Range(0, containers.Count).Select(ContainerId), StringComparer.Ordinal);
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (itemIds.Contains(id)) GraphSubsetFrames.Raise(items, id, mark);
            else if (boxIds.Contains(id)) GraphSubsetFrames.Raise(boxes, id, mark);
        }

        return new PackingFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(items), GraphSubsetFrames.ToRefs(boxes), assign, totals) { Ok = ok };
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// in which every element is Background and nothing is placed. The solver had not finished, so nothing is an answer or
    /// judged.
    /// </summary>
    public static PackingFrame StoppedFrame(PackingSpec spec, string caption) {
        int containers = Containers(spec).Count;
        return new PackingFrame(StepEvent.Done, caption, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), Enumerable.Repeat(-1, spec.Items.Count).ToArray(),
            Enumerable.Range(0, containers).Select(_ => new PackingTotal(0) { Value = spec.Rule == PackingRule.Knapsack ? 0 : null }).ToList()) { Ok = false };
    }

    // ---- Reading answers (only for a run whose steps cannot be drawn) ----

    /// <summary>
    /// A placement from groups of item keys (one group per container, in container order): each key goes to the first item
    /// with that key not used yet, so repeated numbers fill repeated items in order. <paramref name="keys"/> is each item's
    /// key, in instance order. A key no item has left is skipped.
    /// </summary>
    public static Placement FromGroups(IReadOnlyList<string> keys, IEnumerable<IEnumerable<string>> groups) {
        var assign = Enumerable.Repeat(-1, keys.Count).ToArray();
        int c = 0;
        foreach (var group in groups) {
            foreach (var key in group) {
                int i = Enumerable.Range(0, keys.Count).FirstOrDefault(i => assign[i] == -1 && keys[i] == key, -1);
                if (i >= 0) assign[i] = c;
            }
            c++;
        }
        return new Placement(assign);
    }

    /// <summary>
    /// The groups of an answer written as a collection of collections (<c>{(1,2),(3)}</c>, <c>((4,6),(7,3))</c>), each member
    /// as SPADE writes it; or, when <paramref name="nested"/> is false, the one group of a flat collection (<c>{1,12,15}</c>,
    /// <c>{(20,100),(30,120)}</c>). Anything unreadable is no groups.
    /// </summary>
    public static List<List<string>> Groups(string answer, bool nested) {
        try {
            var top = new UtilCollection(answer);
            if (top.IsValue()) return [];
            if (!nested) return [top.ToList().Select(Key).ToList()];
            return top.ToList().Select(g => g.IsValue() ? new List<string> { Key(g) } : g.ToList().Select(Key).ToList()).ToList();
        } catch {
            return [];
        }
    }

    /// <summary>An item's key for <see cref="FromGroups"/>: SPADE's writing of it with spaces removed (<c>4</c>, <c>(10,60)</c>).</summary>
    public static string Key(UtilCollection item) => item.ToString().Replace(" ", "");

    /// <summary>An item's key for <see cref="FromGroups"/> from its label.</summary>
    public static string Key(string label) => label.Replace(" ", "");

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(PackingSpec spec, SolveRun run, Func<string, Placement> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<Placement>? last = null;
        foreach (var step in run.StepsFor(typeof(Placement)).OfType<SolverStep<Placement>>()) {
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(spec, step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental));
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(spec, GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(spec, ok ? parseAnswer(run.Answer) : new Placement([]), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.Packing), Payload(spec), frames);
    }

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{a,b,c}" for captions.</summary>
    public static string Braces(IEnumerable<string> parts) => "{" + string.Join(",", parts) + "}";

    /// <summary>"a + b + c = s" for captions; "0" for nothing.</summary>
    public static string SumText(IReadOnlyList<int> numbers) =>
        numbers.Count == 0 ? "0" : numbers.Count == 1 ? Num(numbers[0])
        : string.Join(" + ", numbers.Select(Num)) + " = " + Num(numbers.Sum());

    /// <summary>A number as captions write it.</summary>
    public static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>Half of <paramref name="total"/> as captions write it: 75, or 74.5.</summary>
    public static string Half(int total) => (total / 2.0).ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A visualization for a packing problem drawn as the Packing picture. The visualization declares only what the problem is
/// (<see cref="Describe"/>) and how to read an answer as a <see cref="Placement"/> (<see cref="ParseAnswer"/>); the frames
/// hook then comes for free. Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(Placement)</c>.
/// </summary>
interface IPackingVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    PackingSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a placement (used only when the run's steps cannot be drawn).</summary>
    Placement ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return PackingFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
