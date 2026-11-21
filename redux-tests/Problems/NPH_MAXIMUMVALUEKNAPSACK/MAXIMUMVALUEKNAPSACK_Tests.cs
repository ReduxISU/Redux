using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Numerics;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MAXIMUMVALUEKNAPSACK_Tests {
    [Fact]
    public void DefaultInstance_ParsesItemsAndCompleteMetadata() {
        var problem = new MAXIMUMVALUEKNAPSACK();
        Assert.Equal("Maximum-Value 0-1 Knapsack", problem.problemName);
        Assert.Equal("(((10,60),(20,100),(30,120)),50)", problem.instance);
        Assert.Equal(problem.instance, problem.defaultInstance);
        Assert.Equal(50, problem.capacity);
        Assert.Equal(280, problem.totalItemValue);
        Assert.Equal(new[] { new MaximumValueKnapsackItem(1, 10, 60), new(2, 20, 100), new(3, 30, 120) }, problem.items);
        Assert.Equal(ComplexityClass.NPHard, problem.complexityClass);
        Assert.Equal(ProblemVariant.Optimization, problem.problemVariant);
        Assert.Equal(ProblemType.MathematicalProgramming, problem.problemType);
        Assert.Contains(MAXIMUMVALUEKNAPSACK.InstanceGrammar, problem.instanceFormat);
        Assert.Contains(problem.defaultInstance, problem.instanceFormat);
        Assert.Contains("({2,3},220)", problem.certificateFormat);
        Assert.Contains("({},0)", problem.certificateFormat);
        Assert.EndsWith("NPH_MAXIMUMVALUEKNAPSACK/MAXIMUMVALUEKNAPSACK_Class.cs", problem.sourceFile.Replace('\\', '/'));
        foreach (string text in new[] { problem.formalDefinition, problem.problemDefinition, problem.inputDescription,
            problem.outputDescription, problem.source, problem.sourceLink, problem.problemLink, problem.wikiName })
            Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.NotEmpty(problem.contributors);
        Assert.Equal(typeof(MAXIMUMVALUEKNAPSACK), ProblemProvider.Problems["maximumvalueknapsack"]);
        // Plain IProblem supports an independently delivered problem definition.
        IProblem contract = problem;
        Assert.IsType<API.DummyClasses.DummySolver>(contract.defaultSolver);
        Assert.IsType<API.DummyClasses.DummyVerifier>(contract.defaultVerifier);
    }

    [Theory]
    [InlineData("((),0)", 0, 0, 0)]
    [InlineData("((),9223372036854775807)", 0, long.MaxValue, 0)]
    [InlineData("(((0,0)),0)", 1, 0, 0)]
    [InlineData("(((0,7),(0,0),(5,0)),0)", 3, 0, 7)]
    [InlineData("(((9223372036854775807,9223372036854775807)),9223372036854775807)", 1, long.MaxValue, long.MaxValue)]
    [InlineData("(((1,9223372036854775806),(2,1)),3)", 2, 3, long.MaxValue)]
    [InlineData(" ( ( ( 001 , 002 ) , ( 0 , 3 ) ) , 0004 ) \r\n", 2, 4, 5)]
    [InlineData("\t(\n(\r(0,1)\t),\n0\r) ", 1, 0, 1)]
    public void Parser_AcceptsEmptyZeroWhitespaceAndInt64Boundaries(string input, int count, long capacity, long total) {
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        Assert.Equal(input, problem.instance);
        Assert.Equal(count, problem.items.Count);
        Assert.Equal(capacity, problem.capacity);
        Assert.Equal(total, problem.totalItemValue);
        Assert.Equal(Enumerable.Range(1, count), problem.items.Select(item => item.Index));
    }

    [Fact]
    public void DuplicateOccurrences_KeepInputOrderAndIndependentIndices() {
        var problem = new MAXIMUMVALUEKNAPSACK("(((2,3),(1,9),(2,3)),4)");
        Assert.Equal(new[] { 2L, 1L, 2L }, problem.items.Select(item => item.Weight));
        Assert.Equal(new[] { 3L, 9L, 3L }, problem.items.Select(item => item.Value));
        Assert.Equal(15, problem.totalItemValue);
        Assert.True(problem.IsFeasible([1, 3]));
        Assert.Equal(6, problem.Value([1, 3]));
        Assert.Equal("({1,3},6)", problem.FormatCertificate([3, 1]));
        Assert.False(problem.IsFeasible([1, 1]));
    }

    [Fact]
    public void Items_CannotBeMutatedThroughCollectionInterfaces() {
        var problem = new MAXIMUMVALUEKNAPSACK();
        var list = Assert.IsAssignableFrom<IList<MaximumValueKnapsackItem>>(problem.items);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(new(4, 1, 1)));
        Assert.Throws<NotSupportedException>(() => list[0] = new(1, 0, 0));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        // A record copy cannot mutate the original occurrence.
        var copy = problem.items[0] with { Weight = 0 };
        Assert.Equal(0, copy.Weight);
        Assert.Equal(10, problem.items[0].Weight);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("garbage")]
    [InlineData("()")]
    [InlineData("(())")]
    [InlineData("((),0,1)")]
    [InlineData("((),)")]
    [InlineData("(,0)")]
    [InlineData("((),,0)")]
    [InlineData("((),0,)")]
    [InlineData("(0,0)")]
    [InlineData("({},0)")]
    [InlineData("({(1,2)},3)")]
    [InlineData("([(1,2)],3)")]
    [InlineData("(((1,2]),3)")]
    [InlineData("(((1,2)),3")]
    [InlineData("(((1,2)),3))")]
    [InlineData("(((1,2)),3)trailing")]
    [InlineData("(((1,2)),3)()")]
    [InlineData("(((1,2)),3) ((),0)")]
    [InlineData("(((1,2)),(3))")]
    [InlineData("(((1,2)),())")]
    [InlineData("((()),0)")]
    [InlineData("(((1)),0)")]
    [InlineData("(((1,2,3)),0)")]
    [InlineData("(((1,,2)),0)")]
    [InlineData("(((1,2,)),0)")]
    [InlineData("(((,1,2)),0)")]
    [InlineData("(((1,2),),0)")]
    [InlineData("((,(1,2)),0)")]
    [InlineData("(((1,2),,(3,4)),0)")]
    [InlineData("(((1,2)(3,4)),0)")]
    [InlineData("(((1,2)3),0)")]
    [InlineData("(((1 2,3)),0)")]
    [InlineData("(((1,2)),3 4)")]
    [InlineData("((((1),2)),0)")]
    [InlineData("(((1,(2))),0)")]
    [InlineData("((1,2),3)")]
    public void Parser_RejectsMalformedInputWithTypedErrors(string? input) {
        var error = Assert.Throws<ProblemParseException>(() => new MAXIMUMVALUEKNAPSACK(input!));
        Assert.Equal(input, error.Received);
        Assert.Equal("Maximum-Value 0-1 Knapsack", error.ProblemName);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Throws<ProblemParseException>(() => ParseGuard.CreateProblem<MAXIMUMVALUEKNAPSACK>(input!));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("+0")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("0x10")]
    [InlineData("one")]
    [InlineData("NaN")]
    [InlineData("٤")]
    [InlineData("４")]
    [InlineData("9223372036854775808")]
    [InlineData("18446744073709551615")]
    [InlineData("9999999999999999999999999999999999")]
    [InlineData("0\0")]
    [InlineData("\0")]
    [InlineData("1\02")]
    [InlineData("1\t2")]
    public void Parser_RejectsInvalidNumbersInEveryPosition(string token) {
        foreach (string input in new[] { $"((({token},0)),0)", $"(((0,{token})),0)", $"((),{token})" })
            Assert.Throws<ProblemParseException>(() => new MAXIMUMVALUEKNAPSACK(input));
    }

    [Fact]
    public void Parser_RejectsNulAtEveryPosition() {
        for (int i = 0; i <= MAXIMUMVALUEKNAPSACK.DefaultInstance.Length; i++) {
            string input = MAXIMUMVALUEKNAPSACK.DefaultInstance.Insert(i, "\0");
            Assert.Throws<ProblemParseException>(() => new MAXIMUMVALUEKNAPSACK(input));
        }
    }

    [Theory]
    [InlineData("(((0,9223372036854775807),(0,1)),0)")]
    [InlineData("(((9223372036854775807,9223372036854775807),(9223372036854775807,1)),0)")]
    public void Parser_RejectsValueSumOverflowEvenForUnusableItems(string input) {
        var error = Assert.Throws<ProblemParseException>(() => new MAXIMUMVALUEKNAPSACK(input));
        Assert.Contains("sum of item values", error.Message);
        Assert.IsType<OverflowException>(error.InnerException);
    }

    [Fact]
    public void Feasibility_DoesNotOverflowWhenTotalWeightsExceedInt64() {
        var problem = new MAXIMUMVALUEKNAPSACK("(((9223372036854775807,1),(9223372036854775807,2),(0,3)),9223372036854775807)");
        Assert.False(problem.IsFeasible([1, 2]));
        Assert.False(problem.IsFeasible([2, 1]));
        Assert.True(problem.IsFeasible([1, 3]));
        Assert.Equal(6, problem.Value([1, 2, 3]));
        Assert.Equal("({1,3},4)", problem.FormatCertificate([3, 1]));
    }

    [Fact]
    public void SharedOperations_HandleEmptyFreeItemsAndExactCapacity() {
        var problem = new MAXIMUMVALUEKNAPSACK();
        Assert.True(problem.IsFeasible([]));
        Assert.Equal(0, problem.Value([]));
        Assert.Equal("({},0)", problem.FormatCertificate([]));
        Assert.True(problem.IsFeasible([2, 3]));
        Assert.False(problem.IsFeasible([1, 2, 3]));
        Assert.Equal("({2,3},220)", problem.FormatCertificate([3, 2]));
        var free = new MAXIMUMVALUEKNAPSACK("(((0,7),(0,0),(1,5)),0)");
        Assert.True(free.IsFeasible([1, 2]));
        Assert.False(free.IsFeasible([3]));
        Assert.Equal("({1,2},7)", free.FormatCertificate([2, 1]));
        Assert.Equal("({},0)", new MAXIMUMVALUEKNAPSACK("((),0)").FormatCertificate([]));
    }

    [Fact]
    public void SharedOperations_RejectRepeatedAndOutOfRangeIndices() {
        var problem = new MAXIMUMVALUEKNAPSACK();
        foreach (int[] selected in new[] { new[] { 0 }, [-1], [4], [1, 1], [int.MinValue], [int.MaxValue] }) {
            Assert.False(problem.IsFeasible(selected));
            Assert.Throws<ArgumentException>(() => problem.Value(selected));
            Assert.Throws<ArgumentException>(() => problem.FormatCertificate(selected));
        }
        Assert.Throws<ArgumentException>(() => problem.FormatCertificate([1, 2, 3]));
    }

    [Fact]
    public void CertificateFormatting_IsDeterministicAndCultureIndependent() {
        var problem = new MAXIMUMVALUEKNAPSACK("(((0,9223372036854775806),(0,1)),0)");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal("({1,2},9223372036854775807)", problem.FormatCertificate([2, 1]));
            Assert.Equal(problem.FormatCertificate([1, 2]), problem.FormatCertificate([2, 1]));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void SharedOperations_AgreeWithIndependentBigIntegerArithmeticForEverySmallSubset() {
        var random = new Random(874);
        for (int trial = 0; trial < 40; trial++) {
            int n = trial % 9;
            long[] weights = Enumerable.Range(0, n).Select(_ => random.Next(4) == 0 ? long.MaxValue : random.NextInt64(0, 20)).ToArray();
            long[] values = Enumerable.Range(0, n).Select(_ => random.NextInt64(0, 100)).ToArray();
            long capacity = trial % 2 == 0 ? long.MaxValue : random.NextInt64(0, 40);
            string pairs = string.Join(",", weights.Select((w, i) => $"({w.ToString(CultureInfo.InvariantCulture)},{values[i].ToString(CultureInfo.InvariantCulture)})"));
            var problem = new MAXIMUMVALUEKNAPSACK($"(({pairs}),{capacity.ToString(CultureInfo.InvariantCulture)})");
            for (int mask = 0; mask < (1 << n); mask++) {
                int[] selected = Enumerable.Range(1, n).Where(i => (mask & (1 << (i - 1))) != 0).ToArray();
                BigInteger weight = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + weights[i - 1]);
                BigInteger value = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + values[i - 1]);
                Assert.Equal(weight <= capacity, problem.IsFeasible(selected));
                Assert.Equal((long)value, problem.Value(selected));
            }
        }
    }
}

public class MaximumValueKnapsackDefinition_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    [Fact]
    public async Task MetadataAndNavigation_ExposeOptimizationProblem() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("NPHard", info.GetProperty("complexityClass").GetString());
        Assert.Contains(info.GetProperty("problemVariants").EnumerateArray(), v => v.GetString() == "Optimization");
        Assert.Contains(info.GetProperty("problemTypes").EnumerateArray(), v => v.GetString() == "MathematicalProgramming");
        Assert.Equal(MAXIMUMVALUEKNAPSACK.DefaultInstance, info.GetProperty("defaultInstance").GetString());
        using var navigation = await client.GetAsync("/Navigation/NPHard_ProblemsRefactor", TestContext.Current.CancellationToken);
        navigation.EnsureSuccessStatusCode();
        var names = await navigation.Content.ReadFromJsonAsync<string[]>(TestContext.Current.CancellationToken);
        Assert.Contains("MAXIMUMVALUEKNAPSACK", names!);
    }

    [Theory]
    [InlineData("(((2,3),(2,3)),4)")]
    [InlineData("((),0)")]
    [InlineData("(((0,9223372036854775807)),0)")]
    public async Task ProblemInstance_ParsesValidInputs(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/problemInstance?problem=MAXIMUMVALUEKNAPSACK", input,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(input, json.GetProperty("instance").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK(input).items.Count, json.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("(((1,2)),0)trailing")]
    [InlineData("(((0,0\0)),0)")]
    [InlineData("(((0,9223372036854775807),(0,1)),0)")]
    public async Task ProblemInstance_ReportsStructuredParseErrors(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/problemInstance?problem=MAXIMUMVALUEKNAPSACK", input,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(input, json.GetProperty("received").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().instanceFormat, json.GetProperty("expected").GetString());
    }
}
