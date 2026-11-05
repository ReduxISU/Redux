using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
using API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;
using API.Problems.NPComplete.NPC_PARTITION.Visualizations;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
using API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;
using SPADE;

namespace redux_tests;

#pragma warning disable CS1591

/// <summary>
/// Partition, Subset Sum and Knapsack are defined on a multiset: a number (or item) written twice counts twice (#628).
/// SPADE 0.1.2 reads their sets with one copy of each element, so Redux reads the elements again from the text
/// (<see cref="RepeatedElements"/>). These tests pin that every repeat is kept, and that the solvers, verifiers, pictures
/// and reductions treat two equal numbers as two numbers.
/// </summary>
public class RepeatedNumbers_Tests {
    // Partition's default instance before #938 changed it: it has two 12s (#628).
    private const string OldPartitionDefault = "{1,7,12,15,33,12,11,5,6,9,21,18}";


    // -------------------------------------------------------------------------
    // RepeatedElements
    // -------------------------------------------------------------------------

    private static List<string> Read(string grammar, string key, string text) {
        StringParser parser = new(grammar);
        parser.parse(text);
        return RepeatedElements.KeepRepeats(text, parser[key]).Select(e => e.ToString()).ToList();
    }

    [Theory]
    [InlineData("{3,3,2,2}", new[] { "3", "3", "2", "2" })]
    [InlineData("{1,7,12,15,33,12,11,5,6,9,21,18}", new[] { "1", "7", "12", "15", "33", "12", "11", "5", "6", "9", "21", "18" })]
    [InlineData("{ -1,-1 }", new[] { " -1", "-1 " })]          // whitespace is kept as SPADE keeps it
    [InlineData("{3, 2, 3, 2}", new[] { "3", " 2", " 3", " 2" })]
    [InlineData("{1,,2,}", new[] { "1", "2" })]               // empty pieces are skipped, as SPADE skips them
    [InlineData("{}", new string[0])]
    public void KeepRepeats_Reads_Every_Copy_In_Order(string instance, string[] expected) {
        Assert.Equal(expected, Read(PARTITION.InstanceGrammar, "N", instance));
    }

    [Fact]
    public void KeepRepeats_Reads_Repeated_Pairs() {
        Assert.Equal(new[] { "(1,2)", "(3,4)", "(1,2)" },
            Read(KNAPSACK.InstanceGrammar, "i", "({(1,2),(3,4),(1,2)},5,5)"));
    }

    [Fact]
    public void KeepRepeats_Falls_Back_To_Spade_When_The_Text_Does_Not_Match() {
        // The text read must match SPADE's elements; when it cannot, SPADE's own (repeat-free) reading is kept.
        UtilCollection parsed = new("{1,2}");
        Assert.Equal(new[] { "1", "2" }, RepeatedElements.KeepRepeats("{1,3}", parsed).Select(e => e.ToString()));
        Assert.Equal(new[] { "1", "2" }, RepeatedElements.KeepRepeats("no braces", parsed).Select(e => e.ToString()));
    }

    // -------------------------------------------------------------------------
    // Partition
    // -------------------------------------------------------------------------

    [Fact]
    public void Partition_Default_Instance_Keeps_Both_12s() {
        PARTITION problem = new PARTITION(OldPartitionDefault);
        Assert.Equal(12, problem.S.Count);
        Assert.Equal(2, problem.S.Count(x => x == "12"));
        Assert.Equal(150, problem.S.Sum(int.Parse));
    }

    [Fact]
    public void Partition_Instance_Format_No_Longer_Says_Repeats_Collapse() {
        Assert.DoesNotContain("collapse", new PARTITION().instanceFormat);
    }

    [Fact]
    public void Partition_Old_Certificate_Example_Using_One_12_Is_No_Longer_Valid() {
        // It leaves out the second 12 of the old default instance.
        Assert.False(new PartitionVerifier().verify(new PARTITION(OldPartitionDefault), "(33,21,15),(1,7,12,11,5,6,9,18)"));
    }

    [Theory]
    [InlineData("{3,3,2,2}", "(3,2),(3,2)", true)]
    [InlineData("{3,3}", "{(3),(3)}", true)]                 // the solver's form, two equal sides
    [InlineData("{3,3}", "(3),(3)", true)]
    [InlineData("{3,3,2,2}", "(3,3),(2,2)", false)]           // uses every number, but 6 against 4
    [InlineData("{3,2,1}", "(3),(2,1)", true)]
    [InlineData("{3,2,1}", "(3,3),(2,1)", false)]             // 3 twice, S has it once
    [InlineData("{3,3,2,1}", "(3),(2,1)", false)]             // leaves out a 3
    [InlineData("{3,3,2,1}", "(3,x),(3,2,1)", false)]
    [InlineData("{3,3}", "(3)", false)]
    [InlineData("{3,3}", "{}", false)]
    public void Partition_Verifier_Counts_Every_Copy(string instance, string certificate, bool expected) {
        Assert.Equal(expected, new PartitionVerifier().verify(new PARTITION(instance), certificate));
    }

    [Theory]
    [InlineData("{3,3,2,2}", true)]
    [InlineData("{3,3}", true)]
    [InlineData("{5,5,5,5}", true)]
    [InlineData("{2,2,2}", false)]
    [InlineData("{1,7,12,15,33,12,11,5,6,9,21,18}", true)]
    public void Partition_BruteForce_Uses_Each_Copy(string instance, bool solvable) {
        PARTITION problem = new PARTITION(instance);
        string answer = new PartitionBruteForce().solve(problem);
        if (solvable) Assert.True(new PartitionVerifier().verify(problem, answer), answer);
        else Assert.Equal("{}", answer);
    }

    [Fact]
    public void Partition_Whitespace_And_Negative_Numbers_Read_As_Before() {
        Assert.Equal(new[] { " 1 ", " -2 ", "  3 " }, new PARTITION("{ 1 , -2 ,  3 }").S);
        Assert.ThrowsAny<Exception>(() => new PARTITION("{1,2,x}"));
        Assert.ThrowsAny<Exception>(() => new PARTITION("{}"));
    }

    [Fact]
    public void Partition_Picture_Gives_Each_Copy_Its_Own_Position_Id() {
        PARTITION problem = new PARTITION();
        var list = new PartitionDefaultVisualization().visualize(problem).data.list!;
        Assert.Equal(problem.S, list.Select(e => e.value));
        Assert.Equal(list.Count, list.Select(e => e.id).Distinct().Count());
        // Each copy is the Packing picture's item at its position, the id the gadgets name.
        Assert.Equal("I3", list[2].id);
        Assert.Equal("I6", list[5].id);
        Assert.Equal("I1", list[0].id);
    }

    [Fact]
    public void Partition_Solved_Picture_Colors_Each_Copy_By_Its_Side() {
        PARTITION problem = new PARTITION("{3,3,2,2}");
        var set = (API.Interfaces.JSON_Objects.API_SET)new PartitionDefaultVisualization().SolvedVisualization(problem, "{(3,2),(3,2)}");
        Assert.Equal(new[] { "Solution", "SolutionAlt", "Solution", "SolutionAlt" }, set.data.list!.Select(e => e.color));
    }

    [Fact]
    public void Partition_Solved_Picture_Of_No_Split_Draws_The_Numbers() {
        PARTITION problem = new PARTITION("{2,2,2}");
        var set = (API.Interfaces.JSON_Objects.API_SET)new PartitionDefaultVisualization().SolvedVisualization(problem, "{}");
        Assert.Equal(3, set.data.list!.Count);
    }

    // -------------------------------------------------------------------------
    // Subset Sum
    // -------------------------------------------------------------------------

    [Fact]
    public void SubsetSum_Keeps_Repeated_Numbers() {
        Assert.Equal(new[] { "7", "7", "1" }, new SUBSETSUM("({7,7,1},14)").S);
    }

    [Theory]
    [InlineData("({7,7,1},14)", "{7,7}", true)]
    [InlineData("({7,1},14)", "{7,7}", false)]
    [InlineData("({7,7,1},15)", "{7,7,1}", true)]
    [InlineData("({7,7,1},21)", "{7,7,7}", false)]
    [InlineData("({7, 7,1},14)", "{7,7}", true)]              // " 7" and "7" are the same number
    public void SubsetSum_Verifier_Counts_Every_Copy(string instance, string certificate, bool expected) {
        Assert.Equal(expected, new SubsetSumVerifier().verify(new SUBSETSUM(instance), certificate));
    }

    [Fact]
    public void SubsetSum_Solvers_Find_A_Subset_Using_A_Number_Twice() {
        SUBSETSUM problem = new SUBSETSUM("({7,7,1},14)");
        Assert.Equal("{7,7}", new SubsetSumBruteForce().solve(problem));
        Assert.Equal("{7,7}", new FastApproximation().solve(problem));
        Assert.Equal("{}", new SubsetSumBruteForce().solve(new SUBSETSUM("({7,1},14)")));
    }

    // -------------------------------------------------------------------------
    // Knapsack
    // -------------------------------------------------------------------------

    [Fact]
    public void Knapsack_Keeps_Identical_Items() {
        KNAPSACK problem = new KNAPSACK("({(1,2),(1,2)},2,4)");
        Assert.Equal(2, problem.items.Count());
    }

    [Theory]
    [InlineData("({(1,2),(1,2)},2,4)", "{(1,2),(1,2)}", true)]
    [InlineData("({(1,2),(3,4)},2,4)", "{(1,2),(1,2)}", false)]  // (1,2) twice, the instance has it once
    [InlineData("({(1,2),(1,2)},1,2)", "{(1,2)}", true)]
    [InlineData("({(1,2),(1,2)},1,4)", "{(1,2),(1,2)}", false)]  // over the weight limit
    public void Knapsack_Verifier_Counts_Every_Copy(string instance, string certificate, bool expected) {
        Assert.Equal(expected, new KnapsackVerifier().verify(new KNAPSACK(instance), certificate));
    }

    [Fact]
    public void Knapsack_Solvers_Take_Both_Identical_Items() {
        KNAPSACK problem = new KNAPSACK("({(1,2),(1,2)},2,4)");
        Assert.Equal("{(1,2),(1,2)}", new KnapsackBruteForce().solve(problem));
        Assert.Equal("{(1,2),(1,2)}", new KnapsackDP().solve(problem));
    }

    [Fact]
    public void Knapsack_Default_Instance_Solves_As_Before() {
        KNAPSACK problem = new KNAPSACK();
        Assert.Equal("{(20,100),(30,120)}", new KnapsackBruteForce().solve(problem));
        Assert.Equal("{(30,120),(20,100)}", new KnapsackDP().solve(problem));
    }

    // -------------------------------------------------------------------------
    // Reductions
    // -------------------------------------------------------------------------

    [Fact]
    public void Feng_Makes_One_Item_Per_Copy() {
        FengReduction reduction = new FengReduction("({7,7,1},14)");
        Assert.Equal("({(7,7),(7,7),(1,1)},14,14)", reduction.reductionTo.instance);
        Assert.Equal(3, reduction.reductionTo.items.Count());
        Assert.Equal(3, new KNAPSACK(reduction.reductionTo.instance).items.Count());
        Assert.Equal("{(7,7),(7,7)}", new KnapsackBruteForce().solve(reduction.reductionTo));
    }

    [Theory]
    [InlineData("({3,4},3)", true)]          // adds T + 1 = 4, a number of S, and sum - T + 1 = 5
    [InlineData("({3,4,5,6},9)", true)]      // T = sum / 2: adds 10 twice
    [InlineData("({3,4},5)", false)]         // adds 6 and 3, a number of S
    [InlineData("({2,4},3)", false)]         // T = sum / 2: adds 4 twice, a number of S
    [InlineData("({7,7,1},14)", true)]
    [InlineData("({7,1},14)", false)]
    public void SubsetSumToPartition_Stays_Sound_When_Numbers_Repeat(string instance, bool expected) {
        SUBSETSUM from = new SUBSETSUM(instance);
        PARTITION to = new SubsetSumToPartitionReduction(from).reductionTo;
        // The target as the endpoint reads it: from its own text.
        PARTITION reread = new PARTITION(to.instance);
        Assert.Equal(to.S, reread.S);
        Assert.Equal(from.S.Count + 2, reread.S.Count);

        bool fromAnswer = from.defaultVerifier.verify(from, new SubsetSumBruteForce().solve(from));
        bool toAnswer = reread.defaultVerifier.verify(reread, new PartitionBruteForce().solve(reread));
        Assert.Equal(expected, fromAnswer);
        Assert.Equal(expected, toAnswer);
    }

    [Fact]
    public void PartitionToWeightedCut_Makes_One_Node_Per_Copy() {
        WEIGHTEDCUTReduction reduction = new WEIGHTEDCUTReduction("{3,3,2,2}");
        Assert.Equal(4, reduction.reductionTo.nodes.Count);
        Assert.Equal(6, reduction.reductionTo.edges.Count);
        Assert.Contains(reduction.reductionTo.edges, e => e.source == "1" && e.destination == "2" && e.weight == 9);
    }
}
