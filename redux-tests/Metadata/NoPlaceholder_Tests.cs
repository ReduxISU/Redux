using System.Reflection;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The templates mark every decision with "TODO" and start with a constructor that parses nothing.
// Both are fine while someone is working, and neither may reach a merge: the other metadata tests
// only check the declared tags, so a class could otherwise pass with everything else unfilled.
[Trait("Category", "Metadata")]
public class NoPlaceholder_Tests {
    // Older text nobody has written yet, one field at a time, so a new TODO in these classes still
    // fails. Unlike the ratchet lists in the other metadata tests, an entry that is no longer needed
    // does not fail: whoever fills in the real text shouldn't have to edit this list as well.
    private static readonly HashSet<string> OlderTodoFields = new(StringComparer.Ordinal) {
        "ConvexHullVisualization.visualizationDefinition",
        "ConvexHullVisualization.sourceLink",
        "ConvexHullVisualization.contributors",
        "LosslessDataCompressionVisualization.visualizationDefinition",
        "LosslessDataCompressionVisualization.sourceLink",
        "LosslessDataCompressionVisualization.contributors",
        "SudokuVisualization.visualizationDefinition",
        "SudokuVisualization.source",
        "KarpDirectedHamiltonianToUndirectedHamiltonian.reductionDefinition",
        "KarpDirectedHamiltonianToUndirectedHamiltonian.source",
    };

    // Its instance is any raw text, so there is no such thing as a malformed one.
    private static readonly HashSet<string> AcceptsAnyText = new(StringComparer.Ordinal) {
        "LOSSLESSDATACOMPRESSION",
    };

    private static readonly string[] Garbage = { "", "{{{", "not an instance" };

    [Fact]
    public void NoFieldStillSaysTodo() {
        var maps = new[] { ProblemProvider.Problems, ProblemProvider.Solvers, ProblemProvider.Verifiers, ProblemProvider.Visualizers, ProblemProvider.Reductions };
        var found = maps.SelectMany(map => map.Values)
            .Distinct()
            .SelectMany(TodoFields)
            .Where(field => !OlderTodoFields.Contains(field))
            .Distinct()
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();

        Assert.True(found.Count == 0,
            $"Still says TODO: {string.Join(", ", found)}. Replace each one with the real text. " +
            "If you aren't finishing a solver, verifier or visualization yet, delete its file: " +
            "the problem uses a placeholder until one is added.");
    }

    [Fact]
    public void EveryProblemRefusesGarbage() {
        var found = ProblemProvider.Problems.Values
            .Where(type => type.GetConstructor(new[] { typeof(string) }) != null)
            .Where(type => !AcceptsAnyText.Contains(type.Name))
            .Where(type => Garbage.All(garbage => Builds(type, garbage)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(found.Count == 0,
            $"These problems accept any instance, even garbage: {string.Join(", ", found)}. " +
            "Parse the instance in the constructor and throw ProblemParseException when it is malformed " +
            "(see Step 3 of Documentation/guides/adding-a-problem.md).");
    }

    private static bool Builds(Type type, string instance) {
        try {
            Activator.CreateInstance(type, instance);
            return true;
        } catch (TargetInvocationException) {
            return false;
        }
    }

    private static IEnumerable<string> TodoFields(Type type) {
        object? instance;
        try {
            instance = Activator.CreateInstance(type);
        } catch (Exception) {
            yield break; // ProblemInstantiation_Tests reports a class that can't be built.
        }
        var properties = new[] { type }.Concat(type.GetInterfaces())
            .SelectMany(owner => owner.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod != null)
            .Where(property => property.PropertyType == typeof(string) || property.PropertyType == typeof(string[]));
        foreach (var property in properties) {
            if (ReadText(property, instance).Any(text => text.Contains("TODO", StringComparison.Ordinal)))
                yield return $"{type.Name}.{property.Name}";
        }
    }

    private static IEnumerable<string> ReadText(PropertyInfo property, object? instance) {
        object? value;
        try {
            value = property.GetValue(instance);
        } catch (TargetInvocationException) {
            return Array.Empty<string>();
        }
        return value switch {
            string text => new[] { text },
            string[] texts => texts.Where(text => text != null),
            _ => Array.Empty<string>(),
        };
    }
}
