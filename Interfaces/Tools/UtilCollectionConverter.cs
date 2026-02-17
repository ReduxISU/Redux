using System.Text.Json;
using System.Text.Json.Serialization;
using SPADE;
using Xunit.Sdk;

namespace API.Interfaces.Tools;

public class UtilCollectionConverter : JsonConverter<UtilCollection>
{
    public override UtilCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException("UtilCollectionReading not implemented");
    }

    public override void Write(Utf8JsonWriter writer, UtilCollection uc, JsonSerializerOptions options)
    {
        WriteHelper(writer, uc, options, "r"); // r for root, since the root node needs an id too
    }
    private void WriteHelper(Utf8JsonWriter writer, UtilCollection uc, JsonSerializerOptions options, string id)
    {
        writer.WriteStartObject();

        writer.WriteBoolean("isOrdered", uc.IsOrdered());
        writer.WriteBoolean("isValue", uc.IsValue());
        writer.WriteString("id", id);

        if (uc.IsValue())
        {
            writer.WriteString("value", uc.ToString());
        }
        else
        {
            writer.WritePropertyName("list");

            writer.WriteStartArray();
            if (uc.IsOrdered())
            {
                for (int i = 0; i < uc.Count(); i ++)
                {
                    WriteHelper(writer, uc[i], options, id + "-" + i);
                }
            } else
            {
                foreach (UtilCollection u in uc)
                {
                    WriteHelper(writer, u, options, id + "-" + u.ToString());
                }
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}