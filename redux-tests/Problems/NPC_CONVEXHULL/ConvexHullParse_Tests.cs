using System.Diagnostics;
using System.Globalization;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using SPADE;
using Xunit;

namespace redux_tests;

#pragma warning disable CS1591

// #815: parsing a Convex Hull instance went through SPADE, which takes time quadratic in the instance's length (seconds for
// 10,000 points). The plain {(x,y),...} shape is now read in one pass; everything else still goes through SPADE. These
// tests hold the new reader to exactly what the old one gave.
public class ConvexHullParse_Tests {

    // The parser as it was before #815, verbatim (with #806's finite check): what the new one must agree with.
    private static List<(double x, double y)> OldParse(string s) {
        UtilCollection collection = new UtilCollection(s.Replace(" ", ""));
        collection.assertUnordered();
        return collection.ToList().Select(point => {
            point.assertPair();
            double x = double.Parse(point[0].ToString(), CultureInfo.InvariantCulture);
            double y = double.Parse(point[1].ToString(), CultureInfo.InvariantCulture);
            if (!double.IsFinite(x) || !double.IsFinite(y))
                throw new API.Interfaces.ProblemParseException("CONVEXHULL", s, $"point ({point[0]},{point[1]}) must have finite numbers for x and y");
            return (x, y);
        }).ToList();
    }

    // The points, or the exception, as text; doubles by their bits, so -0 and 0 differ.
    private static string Outcome(Func<List<(double x, double y)>> parse) {
        try {
            return string.Join(";", parse().Select(p => $"{BitConverter.DoubleToInt64Bits(p.x)},{BitConverter.DoubleToInt64Bits(p.y)}"));
        } catch (Exception e) {
            return $"{e.GetType().FullName}: {e.Message}";
        }
    }

    private static void AssertSameAsOld(string instance) =>
        Assert.Equal(Outcome(() => OldParse(instance)), Outcome(() => new CONVEXHULL(instance).points));

    private static string RandomNumber(Random rng) => rng.Next(8) switch {
        0 => rng.Next(-50, 50).ToString(CultureInfo.InvariantCulture),
        1 => (rng.NextDouble() * 200 - 100).ToString("R", CultureInfo.InvariantCulture),
        2 => (rng.NextDouble() * 1e-6).ToString("E3", CultureInfo.InvariantCulture),
        3 => (rng.NextDouble() * 1e20).ToString("R", CultureInfo.InvariantCulture),
        4 => new[] { "0", "-0", "+0", ".5", "5.", "-.25", "+3", "1e3", "1E-3", "2.5e+2" }[rng.Next(10)],
        5 => rng.Next(0, 4).ToString(CultureInfo.InvariantCulture),
        6 => (rng.Next(-20, 20) / 4.0).ToString(CultureInfo.InvariantCulture),
        _ => (rng.NextDouble() * 2 - 1).ToString("R", CultureInfo.InvariantCulture),
    };

    // Random instances in the plain shape: random number spellings, repeated points (the same text, and the same value
    // spelled differently), spaces here and there.
    public static TheoryData<int> Seeds => new(Enumerable.Range(0, 200));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomPlainInstances_ParseExactlyAsBefore(int seed) {
        var rng = new Random(seed);
        var points = new List<string>();
        int n = rng.Next(0, 40);
        for (int i = 0; i < n; i++) {
            if (points.Count > 0 && rng.Next(5) == 0) points.Add(points[rng.Next(points.Count)]);
            else points.Add($"({RandomNumber(rng)},{RandomNumber(rng)})");
        }
        string instance = "{" + string.Join(",", points) + "}";
        if (rng.Next(3) == 0) instance = instance.Replace(",", ", ");
        if (rng.Next(4) == 0) instance = " " + instance.Replace("(", "( ") + " ";
        AssertSameAsOld(instance);
    }

    // Shapes the one-pass reader hands back to SPADE, and numbers that do not parse: the result or the error is the same.
    [Theory]
    [InlineData("{}")]
    [InlineData("{(1,2)}")]
    [InlineData("{(1,2),(1,2),(1.0,2),(-0,0),(0,0)}")]
    [InlineData("{(1,2),(3,4),}")]
    [InlineData("{(1,2)(3,4)}")]
    [InlineData("{(1,2),\n(3,4)}")]
    [InlineData("{(1,2),\t(3,4)}")]
    [InlineData("{((1,2),3)}")]
    [InlineData("{(1,2,3)}")]
    [InlineData("{(1)}")]
    [InlineData("{(1,2),{1,2}}")]
    [InlineData("((1,2))")]
    [InlineData("((1,2),(3,4))")]
    [InlineData("{(a,b)}")]
    [InlineData("{(1,2),(3,4)}x")]
    [InlineData("x{(1,2)}")]
    [InlineData("{(1,2),(3,4)")]
    [InlineData("{(1,2),,(3,4)}")]
    [InlineData("{(,2)}")]
    [InlineData("{(1,)}")]
    [InlineData("{(1-2,3)}")]
    [InlineData("{(.,3)}")]
    [InlineData("{(e,3)}")]
    [InlineData("{(1e,3)}")]
    [InlineData("{(--1,3)}")]
    [InlineData("{(1,2),(NaN,3)}")]
    [InlineData("{(Infinity,3)}")]
    [InlineData("{(1e400,3)}")]
    [InlineData("{(1.5.2,3),(1,2,3)}")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("")]
    [InlineData(" ")]
    public void EdgeInstances_ParseExactlyAsBefore(string instance) => AssertSameAsOld(instance);

    [Fact]
    public void TheDefaultInstance_ParsesExactlyAsBefore() => AssertSameAsOld(new CONVEXHULL().defaultInstance);

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class Timing {
        // 10,000 random points used to take seconds; one pass takes milliseconds. The bound is generous so a busy machine does
        // not make it flaky.
        [Fact]
        public void TenThousandPoints_ParseInUnderOneSecond() {
            var rng = new Random(815);
            string instance = "{" + string.Join(",", Enumerable.Range(0, 10_000).Select(_ =>
                $"({(rng.NextDouble() * 2 - 1).ToString("R", CultureInfo.InvariantCulture)},{(rng.NextDouble() * 2 - 1).ToString("R", CultureInfo.InvariantCulture)})")) + "}";
            _ = new CONVEXHULL("{(0,0),(1,1)}");
            var stopwatch = Stopwatch.StartNew();
            var problem = new CONVEXHULL(instance);
            stopwatch.Stop();
            Assert.Equal(10_000, problem.points.Count);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"parsing 10,000 points took {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
