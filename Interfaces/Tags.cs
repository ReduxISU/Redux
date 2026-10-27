using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace API.Interfaces;

/// <summary>
/// Tag sets (#914). <see cref="ProblemType"/>, <see cref="ProblemVariant"/>, <see cref="SolverType"/>,
/// <see cref="ReductionType"/> and <see cref="VisualizationType"/> are <c>[Flags]</c> enums, so an item declares
/// every tag that fits, unordered and unranked: <c>SolverType solverType =&gt; SolverType.Greedy | SolverType.Heuristic;</c>.
/// Each member is a power of two, declared in ascending order, and the zero member means "none declared".
/// <para>
/// The API writes a tag set two ways. The list (<c>solverTypes: ["Greedy", "Heuristic"]</c>, added by
/// <see cref="TagListJson"/>) holds every tag in the enum's declared order, so the same set always gives the
/// same JSON. The old single field (<c>solverType</c>) is deprecated: it holds the first tag of that list
/// (see <see cref="TagJsonConverter{T}"/>), which for a one-tag item is exactly the value it had before, and it
/// is removed once Redux_GUI and Redux_Frontend read the lists. <see cref="ProblemVariant"/> is new, so it has
/// only the list.
/// </para>
/// </summary>
internal static class Tags {
    /// <summary>The tag enums, each mapped to whether the JSON keeps its deprecated single field.</summary>
    internal static readonly IReadOnlyDictionary<Type, bool> Sets = new Dictionary<Type, bool> {
        [typeof(ProblemType)] = true,
        [typeof(ProblemVariant)] = false,
        [typeof(SolverType)] = true,
        [typeof(ReductionType)] = true,
        [typeof(VisualizationType)] = true,
    };

    // Per enum: the zero member's name and every other member as (bit, name), in declared order.
    private static readonly ConcurrentDictionary<Type, (string None, (ulong Bit, string Name)[] Members)> _members = new();

    private static (string None, (ulong Bit, string Name)[] Members) MembersOf(Type type) =>
        _members.GetOrAdd(type, t => {
            // Enum.GetValues sorts by value; the members are declared in ascending order (Tags_Tests checks),
            // so this is the declared order too.
            var values = Enum.GetValues(t).Cast<Enum>().Select(v => (Bit: Convert.ToUInt64(v), Name: v.ToString())).ToList();
            return (values.Single(v => v.Bit == 0).Name, values.Where(v => v.Bit != 0).ToArray());
        });

    /// <summary>
    /// Every tag in <paramref name="value"/>, in the enum's declared order. An empty set gives the zero
    /// member alone (e.g. <c>["Unclassified"]</c>), so the list is never empty and its first entry is
    /// always the deprecated single field's value.
    /// </summary>
    internal static List<string> Names(Enum value) {
        var (none, members) = MembersOf(value.GetType());
        ulong bits = Convert.ToUInt64(value);
        var names = members.Where(m => (bits & m.Bit) == m.Bit).Select(m => m.Name).ToList();
        if (names.Count == 0) names.Add(none);
        return names;
    }

    /// <summary>The deprecated single-field value: the first tag of <see cref="Names"/>.</summary>
    internal static string First(Enum value) => Names(value)[0];
}

/// <summary>
/// Writes a tag enum as one string, the first tag in declared order (<see cref="Tags.First"/>), never the
/// comma-joined <c>"A, B"</c> that <see cref="JsonStringEnumConverter"/> writes for a <c>[Flags]</c> value.
/// This is the deprecated single field; the full set is the list <see cref="TagListJson"/> adds beside it.
/// Reads a name, or several joined by commas.
/// </summary>
public sealed class TagJsonConverter<T> : JsonConverter<T> where T : struct, Enum {
    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text == null || !Enum.TryParse(text, ignoreCase: false, out T value))
            throw new JsonException($"Expected {typeof(T).Name} names, got {reader.TokenType} '{text}'.");
        return value;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Tags.First(value));

    /// <inheritdoc/>
    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Read(ref reader, typeToConvert, options);

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WritePropertyName(Tags.First(value));
}

/// <summary>
/// Puts the tag lists (<c>problemTypes</c>, <c>problemVariants</c>, <c>solverTypes</c>, <c>reductionTypes</c>,
/// <c>visualizationTypes</c>) in the JSON of every object with a tag, each right after its deprecated single
/// field, and drops the single field of a set that has none (<see cref="ProblemVariant"/>). A class that leaves a
/// tag to its interface's default member still gets the list: System.Text.Json writes only the members a class
/// declares, the same gap <see cref="ProblemDefaultsJson"/> fills for the default solver/verifier/visualization.
/// </summary>
internal static class TagListJson {
    // The interface members a class may leave undeclared, read through the interface.
    private static readonly (Type Owner, string Name, Func<object, Enum> Get)[] InterfaceTags = {
        (typeof(IProblem), "problemType", o => ((IProblem)o).problemType),
        (typeof(IProblem), "problemVariant", o => ((IProblem)o).problemVariant),
        (typeof(ISolver), "solverType", o => ((ISolver)o).solverType),
        (typeof(IReduction), "reductionType", o => ((IReduction)o).reductionType),
        (typeof(IVisualization), "visualizationType", o => ((IVisualization)o).visualizationType),
    };

    /// <summary>Adds the modifier to <paramref name="options"/> and returns them.</summary>
    // Added to any resolver the options already have, so other modifiers on the same options keep working.
    internal static JsonSerializerOptions Apply(JsonSerializerOptions options) {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(AddTagLists);
        return options;
    }

    private static void AddTagLists(JsonTypeInfo info) {
        if (info.Kind != JsonTypeInfoKind.Object) return;

        // Backwards, so inserting after a property and removing it leave the indexes still to visit alone.
        for (int i = info.Properties.Count - 1; i >= 0; i--) {
            JsonPropertyInfo single = info.Properties[i];
            if (single.Get is not { } get || !Tags.Sets.TryGetValue(single.PropertyType, out bool keepSingle))
                continue;
            if (!HasProperty(info, single.Name + "s"))
                info.Properties.Insert(i + 1, List(info, single.Name + "s", o => (Enum)get(o)!));
            if (!keepSingle)
                info.Properties.RemoveAt(i);
        }

        if (info.Type.IsInterface) return;
        foreach (var (owner, name, get) in InterfaceTags) {
            if (owner.IsAssignableFrom(info.Type) && !HasProperty(info, name + "s"))
                info.Properties.Add(List(info, name + "s", get));
        }
    }

    private static bool HasProperty(JsonTypeInfo info, string name) => info.Properties.Any(p => p.Name == name);

    private static JsonPropertyInfo List(JsonTypeInfo info, string name, Func<object, Enum> get) {
        var property = info.CreateJsonPropertyInfo(typeof(List<string>), name);
        property.Get = o => Tags.Names(get(o));
        return property;
    }
}
