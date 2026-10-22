using System.Globalization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
using static API.Interfaces.Graphs.FormulaExpr;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Visualizations;

/// <summary>
/// Subset Sum's Formula picture (#929): the equation <c>Σ(1, 7, 12, 15) = 28</c>, the numbers left out of the subset being
/// tried struck through, with a 0/1 chip per number underneath. It is served only by <c>format=frames</c> (type
/// <c>Formula</c>) and is an extra, opt-in view: the problem's default stays the Packing picture
/// (<see cref="SubsetSumVisualization"/>), whose list format this entry serves unchanged.
/// <para>
/// <b>Ids.</b> The group <c>e0</c>; the equation <c>eq</c>; the sum <c>sum</c>; the i-th number of S (0-based, repeats
/// included) <c>s{i}</c>, tied to its chip <c>v:x{i+1}</c>; the target <c>T</c>.
/// </para>
/// <para>
/// <b>Frames</b> follow the solver's steps (Subset Sum Brute Force by default): a chosen number and its chip take the step's
/// state, one left out is <c>False</c> (its chip 0); the equation is <c>Covered</c> when the chosen numbers add up to T and
/// <c>Rejected</c> otherwise, with the sum as the note (<c>sum 20 ≠ 28</c>). A Done with nothing chosen (no subset works) is
/// blank apart from its caption. The focus is the chosen numbers.
/// </para>
/// </summary>
class SubsetSumFormulaVisualization : IVisualization<SUBSETSUM, API_SET>, IFormulaExprVisualization<SUBSETSUM, Placement> {
    public string visualizationName { get; } = "Subset Sum Formula";
    public string visualizationDefinition { get; } = "Subset Sum as an equation, the sum of the numbers of S equal to the target T, with the numbers left out of the subset being tried struck through and the sum of the chosen ones beside it.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new SubsetSumBruteForce();

    private readonly SubsetSumVisualization _listFormat = new();

    public API_SET visualize(SUBSETSUM problem) => _listFormat.visualize(problem);

    public API_JSON SolvedVisualization(SUBSETSUM problem, string solution) => _listFormat.SolvedVisualization(problem, solution);

    // --- Formula picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Placement);

    internal const string Group = "e0";
    internal static string Item(int i) => "s" + i.ToString(CultureInfo.InvariantCulture);
    internal static string Var(int i) => "v:x" + (i + 1).ToString(CultureInfo.InvariantCulture);

    FormulaPayload IFormulaExprVisualization<SUBSETSUM, Placement>.Describe(SUBSETSUM problem) => Payload(problem);

    Func<string, IReadOnlyList<string>>? IFramesVisualization.IdsFromDefaultPicture(string instance) =>
        PackingIds(ParseGuard.CreateProblem<SUBSETSUM>(instance));

    /// <summary>
    /// The Packing picture's ids (<see cref="SubsetSumVisualization"/>) in this picture's (#929): the item <c>I{i+1}</c> is the
    /// number <c>s{i}</c> and its chip <c>v:x{i+1}</c>, and the container <c>C1</c> (the subset, against the line T) is the target
    /// <c>T</c>. An id the Packing picture does not have is kept as it is.
    /// </summary>
    internal static Func<string, IReadOnlyList<string>> PackingIds(SUBSETSUM problem) {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { [PackingFrames.ContainerId(0)] = ["T"] };
        for (int i = 0; i < problem.S.Count; i++) map[PackingFrames.ItemId(i)] = [Item(i), Var(i)];
        return id => map.TryGetValue(id, out var ids) ? ids : [id];
    }

    internal static FormulaPayload Payload(SUBSETSUM problem) =>
        new(FormulaFrames.KindName, FormulaLayouts.Equation, "assignment", "subsetSum",
            [new FormulaGroupDto(Group, "") {
                Expr = Operator("eq", FormulaOps.Eq,
                    Operator("sum", FormulaOps.Sum, [.. problem.S.Select((a, i) => Number(Item(i), long.Parse(a, CultureInfo.InvariantCulture), Var(i)))]),
                    Number("T", problem.T)),
            }],
            null,
            problem.S.Select((_, i) => FormulaVariableDto.Integer(Var(i), "x" + (i + 1).ToString(CultureInfo.InvariantCulture), 0, 1)).ToList()) {
            Noun = "equation",
        };

    FormulaExprFrame IFormulaExprVisualization<SUBSETSUM, Placement>.Draw(SUBSETSUM problem, SolverStep<Placement> step, bool incremental, bool endsRun) {
        var pick = GraphSubsetFrames.Picked(step.Event, incremental, endsRun, step.Ok == true);
        bool blank = step.Partial.Assign.Length == 0 && step.Event == StepEvent.Done;
        return Frame(problem, blank ? null : step.Partial, pick, step.Event, step.Caption, step.Ok);
    }

    FormulaExprFrame IFormulaExprVisualization<SUBSETSUM, Placement>.Solved(SUBSETSUM problem, string answer) {
        bool ok;
        try { ok = problem.defaultVerifier.verify(problem, answer); } catch { ok = false; }
        Placement? chosen = null;
        try { chosen = PackingFrames.FromGroups(problem.S.Select(PackingFrames.Key).ToList(), PackingFrames.Groups(answer, nested: false)); } catch { }
        bool blank = !ok || chosen == null || chosen.Assign.Length == 0;
        string caption = ok ? $"The answer is {answer.Trim()}." : $"No subset adds up to {problem.T.ToString(CultureInfo.InvariantCulture)}.";
        return Frame(problem, blank ? null : chosen, PictureState.Solution, StepEvent.Done, caption, ok);
    }

    private static FormulaExprFrame Frame(SUBSETSUM problem, Placement? chosen, PictureState pick, StepEvent ev, string caption, bool? ok) {
        var draft = new FormulaFrameDraft();
        var focus = new List<string>();
        if (chosen != null) {
            long sum = 0;
            for (int i = 0; i < problem.S.Count; i++) {
                bool picked = chosen.Of(i) == 0;
                var state = picked ? pick : PictureState.False;
                draft.Node(Item(i), state).Var(Var(i), state).Value(Var(i), picked ? 1 : 0);
                if (picked) {
                    sum += long.Parse(problem.S[i], CultureInfo.InvariantCulture);
                    focus.Add(Item(i));
                }
            }
            bool holds = sum == problem.T;
            draft.Group(Group, holds ? PictureState.Covered : PictureState.Rejected,
                $"sum {sum.ToString(CultureInfo.InvariantCulture)} {(holds ? "=" : "≠")} {problem.T.ToString(CultureInfo.InvariantCulture)}");
        }
        return draft.ToFrame(ev, caption, [.. focus], ok);
    }
}
