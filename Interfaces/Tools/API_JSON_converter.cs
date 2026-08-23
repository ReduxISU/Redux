using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.Tools;

/// <summary>
/// A write-only <see cref="JsonConverter{T}"/> that gives <see cref="API_JSON"/> values
/// polymorphic serialization. By default System.Text.Json serializes by the <em>declared</em>
/// type, so a value typed as the member-less <see cref="API_JSON"/> interface (e.g. an element
/// of a <c>List&lt;API_JSON&gt;</c>) would emit an empty <c>{}</c>. This converter instead
/// serializes by the <em>runtime</em> concrete type so the actual payload is written.
/// </summary>
/// <remarks>
/// Register it on the <see cref="JsonSerializerOptions.Converters"/> used to serialize a
/// visualization list (see <c>ProblemProvider.getVisualize</c>). Because the converter only
/// matches <see cref="API_JSON"/> exactly (the default <c>CanConvert</c> of
/// <c>JsonConverter&lt;T&gt;</c>), the <see cref="Write"/> call below re-enters the serializer
/// with the concrete type and is <em>not</em> intercepted again, so there is no infinite recursion.
/// </remarks>
public class API_JSON_Converter : JsonConverter<API_JSON> {
    /// <summary>
    /// Not supported. Deserialization would require a type discriminator to recover the concrete
    /// type from the JSON; these payloads are only ever serialized outward to clients.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    public override API_JSON Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        throw new NotImplementedException("Deserialization requires knowing the concrete type.");
    }

    /// <summary>
    /// Writes <paramref name="value"/> using its runtime concrete type (or a JSON null when it is null).
    /// </summary>
    public override void Write(Utf8JsonWriter writer, API_JSON value, JsonSerializerOptions options) {
        if (value == null) {
            writer.WriteNullValue();
            return;
        }

        // Serialize using the runtime concrete type
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}