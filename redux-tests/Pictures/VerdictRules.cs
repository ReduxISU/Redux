using System.Text.Json;

namespace redux_tests;
#pragma warning disable CS1591

// How the final frame of each picture type says whether the answer is right, and what it must show for the verifier's verdict.
// The problem's default verifier is the independent judge: when it accepts the run's answer the final frame must present it as
// correct, and when it rejects it the final frame must not present a success. A picture's own Describe or builder cannot
// fool this, since the verdict never comes from the picture.
//
// Every picture type follows the general rules below; where its encoding differs, its own rule says why (never a skip):
//
//   all types      The final frame is a Done; its ok is true when the verifier accepts and not true when it rejects; when
//                  the verifier rejects, no piece is Solution.
//   most types     When the verifier accepts: no piece is Rejected, and some piece is Solution unless the answer is empty.
//   Search Tree    Failed branches stay Rejected beside the answer, so Rejected is allowed; the answer's leaf is Solution.
//   Schedule jobs  A late job is Rejected in an accepted answer (its penalty is within K): every late job is Rejected and
//                  every job on time is Solution.
//   Graph, Layered An answer that splits the nodes into groups (colourings, components) is shown by the frame's groups, not
//                  by Solution states, so groups stand in for Solution.
//   Board          Digits given by the puzzle are not Solution, so a solved board with nothing to fill shows only values.
//   Quantum        The frame's result is the solver's answer. Only an outcome measured after running the circuit is drawn on
//                  it (its gates and qubits Solution); a classical solver (applied = 0, every column Untraveled), a result
//                  worked out afterwards (Shor's factors) or a circuit with no column to run shows the result alone.
//   Bipartite      (also the factor graph and the Nested Set) every constraint node (the column the answer does not pick)
//                  is Covered.
//   Grouped Graph, Formula: every clause (or constraint) group of the payload is Covered. An assignment that sets every
//                  variable false draws its variables False, not Solution. An empty answer the builder draws as nothing at
//                  all (Subset Sum's empty set for T = 0) leaves its groups Background: ok is true and nothing is Rejected.
//   Tripartite     every element of X, Y and Z is Covered.
//   Flow           the Solution edges are exactly the frame's cut (empty when no flow reaches the sink).
internal static class VerdictRules {
    /// <summary>What is wrong with a judged run's final frame, in plain words; empty when it agrees with the verifier.</summary>
    internal static List<string> Problems(PictureRuns.PictureRun run) {
        var problems = new List<string>();
        if (run.Final is not { } final) {
            problems.Add(run.Error ?? "it drew no frames");
            return problems;
        }
        string type = run.ResponseType;
        bool accepted = run.Verdict;
        if (run.FinalEvent != "Done") problems.Add($"its final frame is a {run.FinalEvent}, not the Done that closes every run");
        if (accepted && run.FinalOk != true) problems.Add("the verifier accepts the answer, but the final frame's ok is not true");
        if (!accepted && run.FinalOk == true) problems.Add("the verifier rejects the answer, but the final frame's ok is true");
        if (!accepted && run.Count("Solution") > 0)
            problems.Add($"the verifier rejects the answer, but {Pieces(run, "Solution")} are drawn as Solution");
        if (!accepted) return problems;

        bool rejectedAllowed = type == "SearchTree" || (type == "Schedule" && final.TryGetProperty("runs", out _));
        if (!rejectedAllowed && run.Count("Rejected") > 0)
            problems.Add($"the verifier accepts the answer, but {Pieces(run, "Rejected")} are drawn as Rejected");

        bool empty = IsEmptyAnswer(run.Answer);
        bool shownOtherwise = type switch {
            "Graph" or "LayeredGraph" => NonEmpty(final, "groups"),
            "Board" => NonEmpty(final, "values"),
            // Only an outcome measured after running the circuit is drawn on it; a result worked out afterwards (Shor's factors),
            // by a classical solver, or with no column to run (a function of no input bits) is the frame's result field, checked
            // below.
            "QuantumCircuit" => !final.TryGetProperty("outcome", out var outcome) || outcome.ValueKind == JsonValueKind.Null
                                || final.GetProperty("applied").GetInt32() == 0,
            // Checked job by job below.
            "Schedule" => final.TryGetProperty("runs", out _),
            // Checked against the cut below (an empty cut, when no flow can reach the sink, has no Solution edge).
            "FlowNetwork" => true,
            // An assignment that makes every variable false (all zeros) has no true literal to draw: its variables are False.
            "Formula" => run.StatesIn("variables") is { Count: > 0 } vars && vars.Values.All(s => s == "False"),
            _ => false,
        };
        if (!empty && !shownOtherwise && run.Count("Solution") == 0)
            problems.Add("the verifier accepts a non-empty answer, but no piece of the final frame is drawn as Solution");

        switch (type) {
            case "BipartiteGraph":
            case "NestedSet": {
                    string pickSide = run.Payload!.Value.GetProperty("pickSide").GetString()!;
                    var constraints = Ids(run.Payload!.Value, pickSide == "sets" ? "elements" : "sets");
                    var nodes = run.StatesIn("nodes");
                    var uncovered = constraints.Where(c => nodes.GetValueOrDefault(c) != "Covered").ToList();
                    if (uncovered.Count > 0)
                        problems.Add($"the verifier accepts the answer, but constraint nodes {List(uncovered, nodes)} are not Covered");
                    break;
                }
            case "GroupedGraph":
            case "Formula": {
                    var groups = run.StatesIn("groups");
                    // An empty answer that the builder draws as nothing at all (Subset Sum's empty set for T = 0: the caption
                    // says it is the answer, and no piece has a state) leaves its groups Background. Not a wrong verdict, as
                    // nothing is Rejected and ok is true, so it is allowed; any group it does draw must still be Covered.
                    bool drawsNothing = empty && run.FinalStates.Count == 0;
                    var unmet = Ids(run.Payload!.Value, "groups").Where(g => groups.GetValueOrDefault(g) != "Covered")
                        .Where(g => !(drawsNothing && !groups.ContainsKey(g))).ToList();
                    if (unmet.Count > 0)
                        problems.Add($"the verifier accepts the answer, but groups {List(unmet, groups)} are not Covered");
                    break;
                }
            case "TripartiteMatching": {
                    var elements = run.StatesIn("elements");
                    var all = new[] { "x", "y", "z" }.SelectMany(side => Ids(run.Payload!.Value, side));
                    var uncovered = all.Where(e => elements.GetValueOrDefault(e) != "Covered").ToList();
                    if (uncovered.Count > 0)
                        problems.Add($"the verifier accepts the answer, but elements {List(uncovered, elements)} are not Covered");
                    break;
                }
            case "FlowNetwork": {
                    var cut = final.TryGetProperty("cut", out var c) ? c.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : [];
                    var solution = run.StatesIn("edges").Where(kv => kv.Value == "Solution").Select(kv => kv.Key).ToHashSet();
                    if (!cut.SetEquals(solution))
                        problems.Add($"the verifier accepts the answer, but the Solution edges ({string.Join(", ", solution.Order())}) are not the cut ({string.Join(", ", cut.Order())})");
                    break;
                }
            case "Schedule" when final.TryGetProperty("runs", out var runs): {
                    var jobs = run.StatesIn("jobs");
                    foreach (var r in runs.EnumerateArray()) {
                        string job = r.GetProperty("job").GetString()!;
                        string want = r.GetProperty("late").GetBoolean() ? "Rejected" : "Solution";
                        string got = jobs.GetValueOrDefault(job, "Background");
                        if (got != want) problems.Add($"job {job} is {(want == "Rejected" ? "late" : "on time")}, so it should be {want}, but it is {got}");
                    }
                    break;
                }
            case "SearchTree":
                // Solution is required above for a non-empty answer; failed branches may stay Rejected.
                break;
            case "QuantumCircuit": {
                    string result = final.TryGetProperty("result", out var r) ? r.GetString() ?? "" : "";
                    if (result != run.Answer) problems.Add($"the frame's result is {result}, but the solver's answer is {run.Answer}");
                    break;
                }
        }
        return problems;
    }

    /// <summary>An answer that names nothing: <c>{}</c>, <c>()</c>, <c>""</c> and the like.</summary>
    internal static bool IsEmptyAnswer(string answer) => answer.All(ch => "{}()[], \t\r\n".Contains(ch));

    private static bool NonEmpty(JsonElement frame, string member) =>
        frame.TryGetProperty(member, out var m) && m.ValueKind switch {
            JsonValueKind.Object => m.EnumerateObject().Any(),
            JsonValueKind.Array => m.GetArrayLength() > 0,
            _ => false,
        };

    private static List<string> Ids(JsonElement payload, string member) =>
        payload.TryGetProperty(member, out var list) ? list.EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList() : [];

    private static string Pieces(PictureRuns.PictureRun run, string state) {
        var ids = run.FinalStates.Keys.Where(k => k.EndsWith(":" + state, StringComparison.Ordinal)).Select(k => k[..k.IndexOf(':')]).ToList();
        var named = ids.SelectMany(member => run.StatesIn(member).Where(kv => kv.Value == state).Select(kv => kv.Key)).Take(6).ToList();
        int total = run.Count(state);
        return named.Count == 0 ? $"{total} pieces" : string.Join(", ", named) + (total > named.Count ? $" (and {total - named.Count} more)" : "");
    }

    private static string List(List<string> ids, Dictionary<string, string> states) =>
        string.Join(", ", ids.Take(6).Select(id => $"{id} ({states.GetValueOrDefault(id, "Background")})")) + (ids.Count > 6 ? $" and {ids.Count - 6} more" : "");
}
