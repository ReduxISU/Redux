using System.Diagnostics;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
using Xunit;

namespace redux_tests;

#pragma warning disable CS1591

// #816: Graph Coloring Brute Force built a certificate string and had the verifier parse it for every candidate it tried
// (17,480 on the default instance). It now rules a candidate out on its color digits first and builds and verifies the
// certificate only for one that passes. These tests hold it to the old solver, verbatim, answer for answer.
public class GraphColoringBruteForceSpeed_Tests {

    // The solver as it was before #816, verbatim: what the new one must agree with.
    private static string OldSolve(GRAPHCOLORING gColor) {
        int numColors = gColor.K;
        if (gColor.K > gColor.nodes.Count) numColors = gColor.nodes.Count();
        List<int> binary = new List<int>();
        foreach (var i in gColor.nodes) binary.Add(0);
        do {
            string certificate = OldCertificate(binary, gColor.nodes, numColors);
            if (gColor.defaultVerifier.verify(gColor, certificate)) return certificate;
            OldNext(binary, numColors);
        } while (binary.Count(n => n == (numColors - 1)) < gColor.nodes.Count);
        return "{}";
    }

    private static string OldCertificate(List<int> binary, List<string> S, int K) {
        string certificate = "{";
        for (int j = 0; j < K; j++) {
            for (int i = 0; i < binary.Count; i++)
                if (binary[i] == j) certificate += S[i] + ",";
            certificate = certificate.TrimEnd(',');
            certificate += "},{";
        }
        certificate = certificate.TrimEnd('{');
        return "{" + certificate.TrimEnd(',') + "}";
    }

    private static void OldNext(List<int> binary, int K) {
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] != K) {
                binary[i] += 1;
                return;
            } else if (binary[i] == K) {
                binary[i] = 0;
            }
        }
    }

    // Seeded random graphs, 1 to 6 nodes, 0 to all edges, K from 1 to n + 1 (K <= 0 loops forever in the old solver, #758),
    // node names that are letters, numbers or pairs. Plenty have no K-coloring.
    public static TheoryData<int> Seeds => new(Enumerable.Range(0, 150));

    private static readonly string[] Names = { "a", "b", "c", "d", "e", "f", "x1", "10", "2", "(1,2)", "(2,1)", "{3}" };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomGraphs_AnswerExactlyAsBefore(int seed) {
        var rng = new Random(seed);
        int n = rng.Next(1, 7);
        var nodes = Names.OrderBy(_ => rng.Next()).Take(n).ToList();
        var edges = new List<string>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (rng.Next(100) < 55) edges.Add($"{{{nodes[i]},{nodes[j]}}}");
        int k = rng.Next(1, n + 2);
        string instance = $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),{k})";
        var problem = new GRAPHCOLORING(instance);
        Assert.Equal(OldSolve(problem), new GraphColoringBruteForce().solve(new GRAPHCOLORING(instance)));
    }

    [Theory]
    [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)")]
    [InlineData("(({1,2,3},{{1,2},{1,3},{2,3}}),2)")]
    [InlineData("(({1,2,3},{{1,2}}),1)")]
    [InlineData("(({1,2,3},{{1,2},{2,1}}),2)")]
    [InlineData("(({a},{}),1)")]
    public void KnownInstances_AnswerExactlyAsBefore(string instance) =>
        Assert.Equal(OldSolve(new GRAPHCOLORING(instance)), new GraphColoringBruteForce().solve(new GRAPHCOLORING(instance)));

    // Nine nodes holding a K4, so no 3-coloring: the search runs to the end. The old solver took about 5 s here on an idle
    // machine; checking the digits first takes about 0.1 to 0.3 s. The bound is generous so a busy machine does not make
    // it flaky.
    [Fact]
    public void ANineNodeGraphWithNoColoring_IsSearchedToTheEndInUnderThreeSeconds() {
        var problem = new GRAPHCOLORING("(({a,b,c,d,e,f,g,h,i},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d},{e,f},{g,h},{h,i}}),3)");
        var stopwatch = Stopwatch.StartNew();
        string answer = new GraphColoringBruteForce().solve(problem);
        stopwatch.Stop();
        Assert.Equal("{}", answer);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"the search took {stopwatch.ElapsedMilliseconds} ms");
    }
}
