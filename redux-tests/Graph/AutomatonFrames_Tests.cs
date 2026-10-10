using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the Automaton picture: (machine, input, the states of one step, event, focus) -> payload and named
// states. No endpoint, no solver.
public class AutomatonFrames_Tests {
    // DFA's default machine: 1 -a-> 2, 1 -b-> 3, 2 -a,b-> 2, 3 -a-> 2, 3 -b-> 3; start 1, accept {2}.
    private static AutomatonSpec Dfa(string input = "a") => new(AutomatonKind.Dfa, ["1", "2", "3"], ["a", "b"],
        [new("1", "a", "2"), new("1", "b", "3"), new("2", "a", "2"), new("2", "b", "2"), new("3", "a", "2"), new("3", "b", "3")],
        "1", ["2"], input);

    // A DFA with missing moves: only 1 -a-> 2.
    private static AutomatonSpec Partial(string input = "ba") =>
        new(AutomatonKind.Dfa, ["1", "2"], ["a", "b"], [new("1", "a", "2")], "1", ["2"], input);

    // NFA's default machine: as the DFA, plus 1 -ε-> 2 and 2 -ε-> 3.
    private static AutomatonSpec Nfa(string input = "a") => new(AutomatonKind.Nfa, ["1", "2", "3"], ["a", "b"],
        [new("1", "a", "2"), new("1", "ε", "2"), new("1", "b", "3"), new("2", "a", "2"), new("2", "ε", "3"), new("2", "b", "2"),
         new("3", "a", "2"), new("3", "b", "3")],
        "1", ["2"], input);

    private static PictureState? State(AutomatonFrame f, string id) => f.Nodes.TryGetValue(id, out var s) ? s.State : null;
    private static PictureState? EdgeState(AutomatonFrame f, string id) => f.Edges.TryGetValue(id, out var s) ? s.State : null;

    // ---- payload ----

    [Fact]
    public void Payload_HasStatesLayersStartAcceptingAndTheInputTape() {
        var p = AutomatonFrames.Payload(Dfa("ab"));
        Assert.Equal("dfa", p.Kind);
        Assert.Equal("layered", p.Layout);
        Assert.Equal(["q:1", "q:2", "q:3"], p.States.Select(s => s.Id));
        Assert.Equal(["1", "2", "3"], p.States.Select(s => s.Label));
        Assert.Equal([0, 1, 1], p.States.Select(s => s.Layer));
        Assert.Equal("q:1", p.Start);
        Assert.Equal(["q:2"], p.Accepting);
        Assert.Equal(["a", "b"], p.Alphabet);
        Assert.Equal(["a", "b"], p.Input);
        Assert.Null(p.Garbage);
        Assert.Null(p.Paths);
        Assert.Null(p.PathsTruncated);
    }

    [Fact]
    public void Payload_MergesTransitionsBetweenTheSameStatesInOneDirection_AndGivesEveryArrowItsEnds() {
        var p = AutomatonFrames.Payload(Dfa());
        // 2 -a-> 2 and 2 -b-> 2 are one loop labelled "a,b"; 1 -> 2 and 3 -> 2 stay apart (different tails).
        Assert.Equal(["q:1>q:2", "q:1>q:3", "q:2>q:2", "q:3>q:2", "q:3>q:3"], p.Transitions.Select(t => t.Id));
        var loop = p.Transitions.Single(t => t.Id == "q:2>q:2");
        Assert.Equal(("q:2", "q:2", "a,b"), (loop.A, loop.B, loop.Label));
        Assert.Equal(["a", "b"], loop.Symbols);
        Assert.All(p.Transitions, t => Assert.Equal($"{t.A}>{t.B}", t.Id));
        Assert.All(p.Transitions, t => Assert.False(t.Epsilon));
    }

    [Fact]
    public void Payload_MarksAnArrowEpsilonOnlyWhenEverySymbolIsEpsilon() {
        var p = AutomatonFrames.Payload(Nfa());
        var oneTwo = p.Transitions.Single(t => t.Id == "q:1>q:2");
        Assert.Equal("a,ε", oneTwo.Label);
        Assert.False(oneTwo.Epsilon);
        var twoThree = p.Transitions.Single(t => t.Id == "q:2>q:3");
        Assert.Equal("ε", twoThree.Label);
        Assert.True(twoThree.Epsilon);
    }

    [Fact]
    public void Payload_Json_IsTheContract() {
        var spec = new AutomatonSpec(AutomatonKind.Dfa, ["1", "2"], ["a"], [new("1", "a", "2"), new("2", "a", "2")], "1", ["2"], "a");
        Assert.Equal(
            "{\"kind\":\"dfa\",\"layout\":\"layered\"," +
            "\"states\":[{\"id\":\"q:1\",\"label\":\"1\",\"layer\":0},{\"id\":\"q:2\",\"label\":\"2\",\"layer\":1}]," +
            "\"start\":\"q:1\",\"accepting\":[\"q:2\"],\"alphabet\":[\"a\"]," +
            "\"transitions\":[{\"id\":\"q:1\\u003Eq:2\",\"a\":\"q:1\",\"b\":\"q:2\",\"label\":\"a\",\"symbols\":[\"a\"],\"epsilon\":false}," +
            "{\"id\":\"q:2\\u003Eq:2\",\"a\":\"q:2\",\"b\":\"q:2\",\"label\":\"a\",\"symbols\":[\"a\"],\"epsilon\":false}]," +
            "\"input\":[\"a\"]}",
            JsonSerializer.Serialize(AutomatonFrames.Payload(spec)));
    }

    [Fact]
    public void Payload_EmptyInput_IsAnEmptyTape() {
        Assert.Empty(AutomatonFrames.Payload(Dfa("ε")).Input);
        Assert.Empty(AutomatonFrames.Payload(Dfa("")).Input);
    }

    [Fact]
    public void Payload_PutsStatesTheStartCannotReachOneLayerPastTheLast() {
        var spec = new AutomatonSpec(AutomatonKind.Dfa, ["s", "t", "lost"], ["a"], [new("s", "a", "t"), new("lost", "a", "s")], "s", ["t"], "a");
        Assert.Equal([0, 1, 2], AutomatonFrames.Payload(spec).States.Select(s => s.Layer));
    }

    // ---- ids ----

    [Theory]
    [InlineData("1", "q:1")]
    [InlineData("q0", "q:q0")]
    [InlineData("a>b", @"q:a\>b")]
    [InlineData("a:b", @"q:a\:b")]
    [InlineData(@"a\b", @"q:a\\b")]
    public void StateId_EscapesTheSeparators(string name, string id) => Assert.Equal(id, AutomatonFrames.StateId(name));

    [Fact]
    public void Ids_StayUnique_WhenStateNamesHoldTheSeparators() {
        // Without escaping, a>b -> c and a -> b>c would both be "q:a>q:b>q:c"-like; and "garbage" is a fine state name.
        string[] names = ["a", "b>c", "a>b", "c", "x:y", @"x\", "garbage", "Garbage"];
        var transitions = new List<AutomatonTransition> { new("a>b", "a", "c"), new("a", "a", "b>c"), new("x:y", "a", @"x\"), new(@"x\", "a", "x:y") };
        var spec = new AutomatonSpec(AutomatonKind.Dfa, names, ["a"], transitions, "a", ["c"], "a");
        var p = AutomatonFrames.Payload(spec);
        var ids = p.States.Select(s => s.Id).Concat(p.Transitions.Select(t => t.Id)).Append(p.Garbage!.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(names, p.States.Select(s => s.Label));
        Assert.Contains(p.Transitions, t => t.A == @"q:a\>b" && t.B == "q:c");
        Assert.Contains(p.Transitions, t => t.A == "q:a" && t.B == @"q:b\>c");
        // A state already called Garbage pushes the Garbage state's label to Garbage'.
        Assert.Equal(("garbage", "Garbage'"), (p.Garbage.Id, p.Garbage.Label));
    }

    // ---- Garbage ----

    [Fact]
    public void Garbage_IsThereOnlyForADfaWithAMissingMove_AndListsTheMissingMoves() {
        Assert.Null(AutomatonFrames.Payload(Dfa()).Garbage);
        Assert.Null(AutomatonFrames.Payload(new AutomatonSpec(AutomatonKind.Nfa, ["1", "2"], ["a", "b"], [new("1", "a", "2")], "1", ["2"], "b")).Garbage);
        var g = AutomatonFrames.Payload(Partial()).Garbage;
        Assert.NotNull(g);
        Assert.Equal(("garbage", "Garbage"), (g.Id, g.Label));
        Assert.Equal([("q:1", "b"), ("q:2", "a"), ("q:2", "b")], g.Missing.Select(x => (x.State, x.Symbol)));
        // No arrow leads into Garbage: it is drawn apart.
        Assert.DoesNotContain(AutomatonFrames.Payload(Partial()).Transitions, t => t.B == "garbage");
    }

    [Fact]
    public void Frame_EmptyStatesInADfaWithGarbage_IsGarbage_AndADoneThatFellInIsRejected() {
        var fall = AutomatonFrames.Frame(Partial(), [], 0, StepEvent.Reject, [AutomatonFrames.GarbageId], "falls");
        Assert.Equal(["garbage"], fall.Active);
        Assert.Equal(PictureState.Rejected, State(fall, "garbage"));
        var done = AutomatonFrames.Frame(Partial(), [], 0, StepEvent.Done, [], "rejected", ok: false);
        Assert.Equal(["garbage"], done.Active);
        Assert.Equal(PictureState.Rejected, State(done, "garbage"));
        // It never left 1 on "ba", so 2 is Untraveled; 1 was reached.
        Assert.Equal(PictureState.Untraveled, State(done, "q:2"));
        Assert.Null(State(done, "q:1"));
    }

    [Fact]
    public void Frame_EmptyStatesInAnNfa_IsNothingActive() {
        var done = AutomatonFrames.Frame(Nfa("b"), [], 1, StepEvent.Done, [], "rejected", ok: false);
        Assert.Empty(done.Active);
        Assert.DoesNotContain(done.Nodes.Values, s => s.State == PictureState.Rejected);
    }

    // ---- states ----

    [Fact]
    public void Frame_TryHighlightsTheActiveStatesAndTheArrowTaken() {
        var f = AutomatonFrames.Frame(Dfa("a"), ["2"], 1, StepEvent.Try, ["q:1>q:2"], "Read 'a': move from 1 to 2.");
        Assert.Equal(["q:2"], f.Active);
        Assert.Equal(1, f.Position);
        Assert.Equal(PictureState.ElementHighlight, State(f, "q:2"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "q:1>q:2"));
        Assert.Null(State(f, "q:1"));
        Assert.Single(f.Nodes);
        Assert.Single(f.Edges);
        Assert.Null(f.Ok);
    }

    [Fact]
    public void Frame_SolutionOnlyInAcceptAndDone() {
        // An accept state that is merely active in a Try is not yet an answer.
        Assert.Equal(PictureState.ElementHighlight, State(AutomatonFrames.Frame(Dfa(), ["2"], 1, StepEvent.Try, [], "x"), "q:2"));
        // Accept: the accept state is Solution, also when it is the focus.
        var accept = AutomatonFrames.Frame(Nfa(), ["2"], 1, StepEvent.Accept, ["q:2"], "accepts");
        Assert.Equal(PictureState.Solution, State(accept, "q:2"));
        // Done, accepted: the active accept states are Solution, other active states are not.
        var done = AutomatonFrames.Frame(Nfa(), ["2", "3"], 1, StepEvent.Done, [], "accepted", ok: true);
        Assert.Equal(PictureState.Solution, State(done, "q:2"));
        Assert.Null(State(done, "q:3"));
        Assert.True(done.Ok);
    }

    [Fact]
    public void Frame_RejectMarksTheFocusRejected_AndADoneThatRejectsMarksTheActiveStates() {
        var reject = AutomatonFrames.Frame(Dfa("b"), ["3"], 1, StepEvent.Reject, ["q:3"], "3 does not accept");
        Assert.Equal(PictureState.Rejected, State(reject, "q:3"));
        var done = AutomatonFrames.Frame(Dfa("b"), ["3"], 1, StepEvent.Done, [], "rejected", ok: false);
        Assert.Equal(PictureState.Rejected, State(done, "q:3"));
        // On "b" the run is 1, 3: state 2 was never reached.
        Assert.Equal(PictureState.Untraveled, State(done, "q:2"));
        Assert.Null(State(done, "q:1"));
    }

    [Fact]
    public void Frame_RejectedOutranksHighlightOnTheSameState() {
        var f = AutomatonFrames.Frame(Nfa(), ["3"], 1, StepEvent.Reject, ["q:3"], "dead end");
        Assert.Equal(PictureState.Rejected, State(f, "q:3"));
    }

    [Fact]
    public void Frame_UntraveledOnlyInDone() {
        var f = AutomatonFrames.Frame(Dfa("b"), ["1"], 0, StepEvent.Try, ["q:1"], "Start in 1.");
        Assert.DoesNotContain(f.Nodes.Values, s => s.State == PictureState.Untraveled);
    }

    [Fact]
    public void Frame_UnknownFocusIds_AreIgnored() {
        var f = AutomatonFrames.Frame(Dfa(), ["1"], 0, StepEvent.Try, ["1", "q:9", "garbage"], "x");
        Assert.Equal(["q:1"], f.Nodes.Keys);
    }

    [Fact]
    public void Frame_Json_HasOnlyTheContractFields() {
        var f = AutomatonFrames.Frame(Dfa(), ["2"], 1, StepEvent.Try, ["q:1>q:2"], "Read 'a'.");
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Read \\u0027a\\u0027.\",\"focus\":[\"q:1\\u003Eq:2\"],\"position\":1,\"active\":[\"q:2\"]," +
            "\"nodes\":{\"q:2\":{\"state\":\"ElementHighlight\"}},\"edges\":{\"q:1\\u003Eq:2\":{\"state\":\"ElementHighlight\"}}}",
            JsonSerializer.Serialize(f));
        var done = AutomatonFrames.Frame(Dfa(), ["2"], 1, StepEvent.Done, [], "Accepted.", ok: false);
        Assert.EndsWith(",\"ok\":false}", JsonSerializer.Serialize(done));
    }

    // ---- NFA paths ----

    [Fact]
    public void Paths_ListEveryRunOfTheNfa_AcceptingFirst_WithTheirOwnFrames() {
        var p = AutomatonFrames.Payload(Nfa("a"));
        Assert.False(p.PathsTruncated);
        var paths = p.Paths!;
        Assert.Equal(paths.Select((_, i) => $"p{i + 1}"), paths.Select(x => x.Id));
        // Accepting runs come first, then the rest.
        Assert.Equal(paths.OrderByDescending(x => x.Accepted).Select(x => x.Id), paths.Select(x => x.Id));
        Assert.All(paths, x => Assert.Equal(x.Accepted ? "accepts" : x.Label.Contains("dies") ? "dies" : "rejects", x.Outcome));
        // ε moves are tried before symbol moves, as NFASolver tries them, so 1 ⇢ 2 ⇢ 3 → 2 comes first.
        Assert.Equal("Path 1: 1 ⇢ 2 ⇢ 3 → 2, accepts", paths[0].Label);

        var first = paths.Single(x => x.Label.EndsWith(": 1 → 2, accepts"));
        Assert.Equal(["Try", "Try", "Done"], first.Frames.Select(f => f.Event.ToString()));
        Assert.Equal("Start in 1.", first.Frames[0].Caption);
        Assert.Equal(["q:1>q:2"], first.Frames[1].Focus);
        Assert.Equal(PictureState.ElementHighlight, EdgeState(first.Frames[1], "q:1>q:2"));
        var done = first.Frames[^1];
        Assert.True(done.Ok);
        Assert.Equal(PictureState.Solution, State(done, "q:2"));
        // This path never visits 3.
        Assert.Equal(PictureState.Untraveled, State(done, "q:3"));
    }

    [Fact]
    public void Paths_TellAnEpsilonMoveApart_AndARunThatDies() {
        var spec = new AutomatonSpec(AutomatonKind.Nfa, ["1", "2", "3"], ["a", "b"], [new("1", "ε", "2"), new("2", "a", "3"), new("1", "b", "3")], "1", ["3"], "a");
        var paths = AutomatonFrames.Payload(spec).Paths!;
        var accepted = Assert.Single(paths, x => x.Accepted);
        Assert.Equal("Path 1: 1 ⇢ 2 → 3, accepts", accepted.Label);
        Assert.Equal("Take an ε-move from 1 to 2.", accepted.Frames[1].Caption);
        Assert.Equal(0, accepted.Frames[1].Position);
        Assert.Equal(1, accepted.Frames[2].Position);

        var dead = new AutomatonSpec(AutomatonKind.Nfa, ["1", "2"], ["a", "b"], [new("1", "a", "2")], "1", ["2"], "ba");
        var died = Assert.Single(AutomatonFrames.Payload(dead).Paths!);
        Assert.Equal(("dies", "Path 1: 1, dies at symbol 1"), (died.Outcome, died.Label));
        Assert.Equal("No 'b' move from 1, so this path dies. Rejected.", died.Frames[^1].Caption);
        Assert.Equal(PictureState.Rejected, State(died.Frames[^1], "q:1"));
        Assert.False(died.Frames[^1].Ok);
    }

    [Fact]
    public void Paths_AreCappedForABranchyMachine() {
        // Two choices per symbol over 40 symbols: 2^40 runs.
        var spec = new AutomatonSpec(AutomatonKind.Nfa, ["1", "2", "3"], ["a"],
            [new("1", "a", "1"), new("1", "a", "2"), new("2", "a", "1"), new("2", "a", "2")], "1", ["3"], new string('a', 40));
        var p = AutomatonFrames.Payload(spec);
        Assert.Equal(AutomatonFrames.PathLimit, p.Paths!.Count);
        Assert.True(p.PathsTruncated);
    }

    [Fact]
    public void Paths_FollowAnEpsilonCycleOnce() {
        var spec = new AutomatonSpec(AutomatonKind.Nfa, ["1", "2"], ["a"], [new("1", "ε", "2"), new("2", "ε", "1")], "1", ["2"], "ε");
        var paths = AutomatonFrames.Payload(spec).Paths!;
        Assert.Contains(paths, x => x.Label == "Path 1: 1 ⇢ 2, accepts");
        Assert.All(paths, x => Assert.True(x.Frames.Count <= 4));
    }

    // ---- the whole response ----

    private static SolverStep<ActiveStates> Step(string[] states, int pos, StepEvent ev, string[] focus, string caption, bool? ok = null) =>
        new(new ActiveStates(states, pos), ev, focus, caption) { Ok = ok };

    [Fact]
    public void Build_DrawsOneFramePerStep_AndHasTheAutomatonType() {
        object[] steps = [
            Step(["1"], 0, StepEvent.Try, ["q:1"], "Start in 1."),
            Step(["2"], 1, StepEvent.Try, ["q:1>q:2"], "Read 'a': move from 1 to 2."),
            Step(["2"], 1, StepEvent.Done, [], "Accepted.", true),
        ];
        var response = AutomatonFrames.Build(Dfa(), new SolveRun("The sequence of states to accept is: 1, 2", steps, typeof(ActiveStates)));
        Assert.Equal("Automaton", response.Type);
        Assert.IsType<AutomatonPayload>(response.Payload);
        Assert.Equal(3, response.Frames.Count);
        var done = Assert.IsType<AutomatonFrame>(response.Frames[^1]);
        Assert.Equal(PictureState.Solution, State(done, "q:2"));
        Assert.Equal(PictureState.Untraveled, State(done, "q:3"));
    }

    [Fact]
    public void Build_WithoutADoneStep_EndsWithTheMachinesOwnVerdict() {
        var accepted = AutomatonFrames.Build(Dfa("a"), new SolveRun("x", [], null));
        var done = Assert.IsType<AutomatonFrame>(Assert.Single(accepted.Frames));
        Assert.Equal((StepEvent.Done, true, 1), (done.Event, done.Ok, done.Position));
        var fell = Assert.IsType<AutomatonFrame>(Assert.Single(AutomatonFrames.Build(Partial("ba"), new SolveRun("x", [], null)).Frames));
        Assert.Equal((false, "garbage"), (fell.Ok, fell.Active.Single()));
        Assert.Equal("The machine falls into Garbage, so it rejects the input.", fell.Caption);
        // A symbol outside the alphabet is no move into Garbage: the machine stops where it is.
        var stuck = Assert.IsType<AutomatonFrame>(Assert.Single(AutomatonFrames.Build(Partial("ac"), new SolveRun("x", [], null)).Frames));
        Assert.Equal((false, "q:2", 1), (stuck.Ok, stuck.Active.Single(), stuck.Position));
        Assert.Equal("The machine rejects the input.", stuck.Caption);
    }

    [Fact]
    public void Build_StoppedRun_EndsWithABlankNotOkDone() {
        object[] steps = [
            Step(["1"], 0, StepEvent.Try, ["q:1"], "Start in 1."),
            Step(["2"], 1, StepEvent.Try, ["q:1>q:2"], "Read 'a'."),
            Step(["2"], 1, StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.", false),
        ];
        var response = AutomatonFrames.Build(Nfa(), new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(ActiveStates)) { TimedOut = true });
        var done = Assert.IsType<AutomatonFrame>(response.Frames[^1]);
        Assert.Equal(3, response.Frames.Count);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Empty(done.Active);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);
        Assert.Equal(1, done.Position);
        Assert.Equal("Stopped after 0.05 s and 2 steps; the solver had not finished.", done.Caption);
    }

    [Fact]
    public void Build_StoppedRun_FromTheRecorder_HasNothingJudgedInItsDoneFrame() {
        // A recorder whose time limit has already run out stops on the first recorded step.
        var run = SolveRun.Record<ActiveStates>(true, TimeSpan.Zero, rec => {
            rec.Try(() => new ActiveStates(["1"], 0), () => "Start in 1.", "q:1");
            rec.Try(() => new ActiveStates(["2"], 1), () => "Read 'a'.", "q:1>q:2");
            return "unreachable";
        });
        Assert.True(run.TimedOut);
        var response = AutomatonFrames.Build(Nfa(), run);
        var done = Assert.IsType<AutomatonFrame>(response.Frames[^1]);
        Assert.False(done.Ok);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Active);
        Assert.DoesNotContain(response.Frames.Cast<AutomatonFrame>(), f => f.Nodes.Values.Any(s => s.State == PictureState.Solution));
    }

    [Fact]
    public void Build_StoppedRunWithoutSteps_StillEndsWithAStoppedFrame() {
        var response = AutomatonFrames.Build(Nfa(), new SolveRun(SolveRun.TimedOutAnswer, [], typeof(ActiveStates)) { TimedOut = true });
        var done = Assert.IsType<AutomatonFrame>(Assert.Single(response.Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, done.Caption);
        Assert.False(done.Ok);
    }
}
