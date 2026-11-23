using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A picture spec's Validate: a spec Describe could not have built from a valid instance fails with a plain error that names the
// visualization's Describe and what is wrong; a valid spec passes. Every frames picture's Describe on real instances is covered
// by VerifierAgreement_Tests (each BuildFrames validates its spec).
public class SpecValidation_Tests {
    private static readonly string[] U = ["1", "2", "3"];
    private static readonly IReadOnlyList<string>[] S = [["1", "2"], ["2", "3"]];
    private static readonly SolveRun NoRun = new("{}", [], null);

    [Fact]
    public void A_Valid_Bipartite_Spec_Passes() =>
        new BipartiteSpec(Universe: U, Sets: S, K: 2, Pick: BipartitePick.Sets, Rule: BipartiteRule.AtLeastOnce).Validate("X");

    [Fact]
    public void A_Set_Member_Outside_The_Universe_Names_The_Describe_And_The_Member() {
        // As if Describe passed the sets where the universe goes, or the wrong list of sets.
        var spec = new BipartiteSpec(Universe: ["1", "2"], Sets: S, K: null, Pick: BipartitePick.Sets, Rule: BipartiteRule.ExactlyOnce);
        var e = Assert.Throws<InvalidOperationException>(() => spec.Validate("SetCoverDefaultVisualization"));
        Assert.Contains("SetCoverDefaultVisualization.Describe", e.Message);
        Assert.Contains("set S2 holds 3, which is not in the universe", e.Message);
    }

    [Fact]
    public void A_Negative_K_Or_An_Undefined_Rule_Fails() {
        var spec = new BipartiteSpec(Universe: U, Sets: S, K: -1, Pick: BipartitePick.Sets, Rule: (BipartiteRule)7);
        var e = Assert.Throws<InvalidOperationException>(() => spec.Validate("V"));
        Assert.Contains("K is -1, below zero", e.Message);
        Assert.Contains("Rule is 7, which is not a BipartiteRule", e.Message);
    }

    [Fact]
    public void A_Null_Set_Fails() {
        var spec = new BipartiteSpec(Universe: U, Sets: [["1"], null!], K: null, Pick: BipartitePick.Sets, Rule: BipartiteRule.AtLeastOnce);
        Assert.Contains("Sets holds a null", Assert.Throws<InvalidOperationException>(() => spec.Validate()).Message);
    }

    [Fact]
    public void The_Builder_Validates_The_Spec() {
        var spec = new BipartiteSpec(Universe: ["1"], Sets: S, K: null, Pick: BipartitePick.Sets, Rule: BipartiteRule.AtLeastOnce);
        Assert.Throws<InvalidOperationException>(() => BipartiteFrames.Build(spec, NoRun, _ => [], _ => false));
    }

    [Fact]
    public void Graph_Subset_And_Group_Specs_Reject_A_Negative_K_But_Allow_Edges_To_Unlisted_Nodes() {
        KeyValuePair<string, string>[] edges = [new("1", "2"), new("2", "9")];
        new GraphSubsetSpec(Nodes: ["1", "2"], Edges: edges, K: 1, Rule: SubsetRule.Clique).Validate();
        new GraphGroupSpec(Nodes: ["1", "2"], Edges: edges, K: 2, Rule: GroupRule.Coloring).Validate();
        Assert.Contains("K is -1", Assert.Throws<InvalidOperationException>(() =>
            new GraphSubsetSpec(Nodes: ["1", "2"], Edges: edges, K: -1, Rule: SubsetRule.Clique).Validate("CliqueDefaultVisualization")).Message);
        Assert.Contains("K is -2", Assert.Throws<InvalidOperationException>(() =>
            new GraphGroupSpec(Nodes: ["1", "2"], Edges: edges, K: -2, Rule: GroupRule.Coloring).Validate()).Message);
    }

    [Fact]
    public void An_Automaton_Transition_Between_Unknown_States_Fails() {
        var spec = new AutomatonSpec(Kind: AutomatonKind.Dfa, States: ["q0", "q1"], Alphabet: ["0", "1"],
            Transitions: [new("q0", "0", "q1"), new("q1", "1", "q2")], Start: "q0", Accepting: ["q1"], Input: "01");
        var e = Assert.Throws<InvalidOperationException>(() => spec.Validate("DFAVisualization"));
        Assert.Contains("DFAVisualization.Describe", e.Message);
        Assert.Contains("q2", e.Message);
    }

    [Fact]
    public void A_Layered_Spec_With_A_Null_Edge_End_Fails() {
        var spec = new LayeredSpec(Nodes: ["a", "b"], Edges: [new("a", null!)], Rule: LayeredRule.TopologicalSort);
        Assert.Contains("null end", Assert.Throws<InvalidOperationException>(() => spec.Validate()).Message);
    }
}
