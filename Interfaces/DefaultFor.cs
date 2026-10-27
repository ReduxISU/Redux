using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using API.DummyClasses;

namespace API.Interfaces;

/// <summary>
/// Marks a solver, verifier or visualization as the default one for a problem, so the problem class
/// does not have to name it (#919). A problem that implements the plain <see cref="IProblem"/> gets its
/// <c>defaultSolver</c>, <c>defaultVerifier</c> and <c>defaultVisualization</c> from these attributes,
/// and a placeholder (<see cref="DummySolver"/>, <see cref="DummyVerifier"/>, <see cref="DummyVisualization"/>)
/// for any kind that has none. The kind is whichever of <see cref="ISolver"/>, <see cref="IVerifier"/>
/// and <see cref="IVisualization"/> the marked class implements.
/// </summary>
/// <example><c>[DefaultFor(typeof(CLIQUE))] class CliqueBruteForce : ISolver&lt;CLIQUE&gt; { ... }</c></example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DefaultForAttribute : Attribute {
    /// <summary>The problem class this is the default for.</summary>
    public Type Problem { get; }

    /// <summary>Marks the class as the default for <paramref name="problem"/>.</summary>
    public DefaultForAttribute(Type problem) {
        Problem = problem;
    }
}

/// <summary>
/// Problem type -> default solver/verifier/visualization type, read once from the
/// <see cref="DefaultForAttribute"/>s on the types ProblemProvider already scanned. Problems that
/// implement <see cref="IProblem{T, U, V}"/> name their defaults themselves and never come here.
/// </summary>
internal static class ProblemDefaults {
    internal sealed record Declaration(Type Problem, Type Kind, Type Class);

    // The three kinds a default can be, in a fixed order so lookups and tests agree.
    internal static readonly Type[] Kinds = { typeof(ISolver), typeof(IVerifier), typeof(IVisualization) };

    private static readonly Lazy<List<Declaration>> _declarations = new(() => Scan(
        ProblemProvider.Solvers.Values
            .Concat(ProblemProvider.Verifiers.Values)
            .Concat(ProblemProvider.Visualizers.Values)));

    // Several declarations for one (problem, kind) are a mistake that DefaultFor_Tests reports;
    // until it is fixed the first by full name wins, so the choice at least doesn't vary by run.
    private static readonly Lazy<Dictionary<(Type Problem, Type Kind), Type>> _byProblem = new(() =>
        _declarations.Value
            .GroupBy(d => (d.Problem, d.Kind))
            .ToDictionary(g => g.Key, g => g.OrderBy(d => d.Class.FullName, StringComparer.Ordinal).First().Class));

    /// <summary>Every [DefaultFor] declaration, one per (problem, kind) the class implements.</summary>
    internal static IReadOnlyList<Declaration> Declarations => _declarations.Value;

    internal static List<Declaration> Scan(IEnumerable<Type> types) =>
        types.Distinct()
            .SelectMany(type => type.GetCustomAttributes(typeof(DefaultForAttribute), false)
                .Cast<DefaultForAttribute>()
                .SelectMany(attribute => Kinds
                    .Where(kind => kind.IsAssignableFrom(type))
                    .Select(kind => new Declaration(attribute.Problem, kind, type))))
            .ToList();

    /// <summary>
    /// The class declared as <paramref name="problemType"/>'s default of <paramref name="kind"/>, or null.
    /// A problem that subclasses another inherits the base problem's defaults unless it declares its own.
    /// </summary>
    internal static Type? Find(Type problemType, Type kind) {
        for (Type? t = problemType; t != null; t = t.BaseType) {
            if (_byProblem.Value.TryGetValue((t, kind), out Type? found))
                return found;
        }
        return null;
    }

    // A new instance on every call, like the `= new XSolver()` initializers it stands in for give each
    // problem object its own.
    internal static ISolver Solver(Type problemType) =>
        Create<ISolver>(problemType) ?? new DummySolver();

    internal static IVerifier Verifier(Type problemType) =>
        Create<IVerifier>(problemType) ?? new DummyVerifier();

    internal static IVisualization Visualization(Type problemType) =>
        Create<IVisualization>(problemType) ?? new DummyVisualization();

    private static T? Create<T>(Type problemType) where T : class =>
        Find(problemType, typeof(T)) is Type type ? (T)Activator.CreateInstance(type)! : null;
}

/// <summary>
/// Puts <c>defaultSolver</c>, <c>defaultVerifier</c> and <c>defaultVisualization</c> in the JSON of a
/// problem whose class leaves them to <see cref="IProblem"/>. System.Text.Json writes only the members a
/// class declares, so without this /info would drop them, and both front ends read them from there.
/// A problem class that declares them (every IProblem&lt;T, U, V&gt;) is untouched.
/// </summary>
internal static class ProblemDefaultsJson {
    private static readonly (string Name, Func<IProblem, object> Get)[] Members = {
        ("defaultSolver", p => p.defaultSolver),
        ("defaultVerifier", p => p.defaultVerifier),
        ("defaultVisualization", p => p.defaultVisualization),
    };

    /// <summary>Adds the modifier to <paramref name="options"/> and returns them.</summary>
    internal static JsonSerializerOptions Apply(JsonSerializerOptions options) {
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver {
            Modifiers = { AddInheritedDefaults },
        };
        return options;
    }

    private static void AddInheritedDefaults(JsonTypeInfo info) {
        if (info.Kind != JsonTypeInfoKind.Object
            || info.Type.IsInterface || !typeof(IProblem).IsAssignableFrom(info.Type))
            return;
        foreach (var (name, get) in Members) {
            if (info.Properties.Any(p => p.Name == name)) continue;
            // Typed as object so the value is written as its runtime class (all of its members), the way
            // a declared `public CliqueBruteForce defaultSolver` is, rather than as the bare interface.
            var property = info.CreateJsonPropertyInfo(typeof(object), name);
            property.Get = obj => get((IProblem)obj);
            info.Properties.Add(property);
        }
    }
}
