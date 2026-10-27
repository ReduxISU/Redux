using API.Interfaces;

namespace redux_tests.Fixtures;
#pragma warning disable CS1591

// Two problems written the #919 way: plain IProblem, with no solver, verifier or visualization named in
// the class. ProblemProvider scans every loaded assembly, so while the tests run these are served by
// the real endpoints exactly like a problem under Problems/. Their namespace is not
// API.Problems.<Folder>.<Problem>, so the problem listings (allProblems, ALL_ProblemsRefactor, the
// complexity-class lists) leave them out, and the metadata tests scoped to top-level problems skip them.
// They are complete otherwise, so the sweeps over every problem/verifier (parse errors, naming,
// contributors, sourceFile) check them too.

// Declares no defaults at all: every kind falls back to its placeholder.
internal class NODEFAULTSFIXTURE : IProblem {
    public string problemName { get; } = "No Defaults Fixture";
    public string problemLink { get; } = "";
    public string formalDefinition { get; } = "{S | S is a set of integers}";
    public string problemDefinition { get; } = "Test fixture: a problem with no solver, verifier or visualization.";
    public string inputDescription { get; } = "S, a set of integers";
    public string outputDescription { get; } = "Nothing; the fixture has no solver";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "";
    public string defaultInstance { get; } = "{1,2,3}";
    public string instance { get; }
    public string instanceFormat { get; } = "Comma-separated integers in braces. Example: {1,2,3}";
    public string certificateFormat { get; } = "One integer. Example: 2";
    public string[] contributors { get; } = { "" };
    public List<int> numbers { get; }

    public NODEFAULTSFIXTURE() : this("{1,2,3}") { }

    public NODEFAULTSFIXTURE(string instance) {
        this.instance = instance;
        numbers = FixtureParsing.ParseSet(nameof(NODEFAULTSFIXTURE), instance);
    }
}

// Declares only a verifier, through [DefaultFor] on AttributedFixtureVerifier.
internal class ATTRIBUTEDFIXTURE : IProblem {
    public string problemName { get; } = "Attributed Fixture";
    public string problemLink { get; } = "";
    public string formalDefinition { get; } = "{<S, x> | S is a set of integers containing x}";
    public string problemDefinition { get; } = "Test fixture: a problem whose only default is a [DefaultFor] verifier.";
    public string inputDescription { get; } = "S, a set of integers";
    public string outputDescription { get; } = "An element of S";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "";
    public string defaultInstance { get; } = "{1,2,3}";
    public string instance { get; }
    public string instanceFormat { get; } = "Comma-separated integers in braces. Example: {1,2,3}";
    public string certificateFormat { get; } = "One integer of the set. Example: 2";
    public string[] contributors { get; } = { "" };
    public List<int> numbers { get; }

    public ATTRIBUTEDFIXTURE() : this("{1,2,3}") { }

    public ATTRIBUTEDFIXTURE(string instance) {
        this.instance = instance;
        numbers = FixtureParsing.ParseSet(nameof(ATTRIBUTEDFIXTURE), instance);
    }
}

[DefaultFor(typeof(ATTRIBUTEDFIXTURE))]
internal class AttributedFixtureVerifier : IVerifier<ATTRIBUTEDFIXTURE> {
    public string verifierName { get; } = "Attributed Fixture Solution Verifier";
    public string verifierDefinition { get; } = "Accepts a certificate that is an element of the set.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "" };
    public string certificate { get; private set; } = "";

    public bool verify(ATTRIBUTEDFIXTURE problem, string certificate) {
        this.certificate = certificate;
        return problem.numbers.Contains(int.Parse(certificate.Trim()));
    }
}

internal static class FixtureParsing {
    // "{1,2,3}" -> [1, 2, 3]. Anything else throws, which ParseGuard reports as a 400.
    internal static List<int> ParseSet(string problem, string instance) {
        string s = instance.Trim();
        if (!s.StartsWith('{') || !s.EndsWith('}'))
            throw new ProblemParseException(problem, instance, "expected a set in braces, e.g. {1,2,3}");
        string inner = s[1..^1].Trim();
        return inner.Length == 0
            ? new List<int>()
            : inner.Split(',').Select(n => int.Parse(n.Trim())).ToList();
    }
}
