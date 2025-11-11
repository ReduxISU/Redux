using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Interfaces.Tools;

public class API_JSON_Converter<TInterface> : JsonConverter<TInterface>
{
    public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization requires knowing the concrete type.");
    }

    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Serialize using the runtime concrete type
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}