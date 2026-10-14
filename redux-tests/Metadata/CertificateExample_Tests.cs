using System.Reflection;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Every certificate example the API publishes (a CertificateExample constant, shown in a problem's certificateFormat)
// must be a certificate its verifier accepts on the problem's default instance, so a reader can paste the two together
// and see it work.
public class CertificateExample_Tests {
    // Examples that only show the format, on an instance of their own, with why.
    private static readonly Dictionary<string, string> FormatOnlyExamples = new() {
        // A Huffman code table and encoding for "abc"; one for the default sentence would run to about 300 characters.
        ["LosslessDataCompressionVerifier"] = "({(97,0),(98,10),(99,11)},01011)",
    };

    private static string? ExampleOf(Type type) =>
        type.GetField("CertificateExample", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) is { IsLiteral: true } field
            ? (string?)field.GetRawConstantValue()
            : null;

    private static Type? ProblemTypeOf(Type verifier) =>
        verifier.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVerifier<>))
            ?.GetGenericArguments()[0];

    // (verifier, problem it verifies) for every verifier with an example; and every problem with an example of its own,
    // checked with its default verifier.
    public static TheoryData<string, string> ExampleCases {
        get {
            var data = new TheoryData<string, string>();
            foreach (var (name, verifier) in ProblemProvider.Verifiers.OrderBy(kv => kv.Key)) {
                Type? problem = ProblemTypeOf(verifier);
                if (ExampleOf(verifier) != null && problem != null && !problem.IsInterface && !problem.IsAbstract)
                    data.Add(name, problem.Name);
            }
            foreach (var (_, problem) in ProblemProvider.Problems.OrderBy(kv => kv.Key)) {
                if (ExampleOf(problem) != null)
                    data.Add("", problem.Name);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ExampleCases))]
    public void CertificateExample_VerifiesOnTheDefaultInstance(string verifierName, string problemName) {
        Type problemType = ProblemProvider.Problems.Values.Single(t => t.Name == problemName);
        var problem = (IProblem)Activator.CreateInstance(problemType)!;
        IVerifier verifier;
        string example;
        if (verifierName == "") {
            verifier = problem.defaultVerifier;
            example = ExampleOf(problemType)!;
        } else {
            Type verifierType = ProblemProvider.Verifiers[verifierName];
            verifier = (IVerifier)Activator.CreateInstance(verifierType)!;
            example = ExampleOf(verifierType)!;
        }
        if (FormatOnlyExamples.TryGetValue(verifier.GetType().Name, out string? formatOnly)) {
            Assert.Equal(formatOnly, example); // an allowed exception must not hide a changed example
            return;
        }
        Assert.True(verifier.verify(problem.defaultInstance, example),
            $"{verifier.GetType().Name} rejects its CertificateExample {example} on {problemName}'s default instance {problem.defaultInstance}");
    }

    [Fact]
    public void SomeExamplesAreFound() {
        Assert.True(ExampleCases.Count() >= 40);
    }
}
