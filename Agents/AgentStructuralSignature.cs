using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects.OpenAI;

namespace CasoD.Agents;

internal sealed record AgentStructuralSignature(
    string Model,
    string Instructions,
    IReadOnlyList<string> ToolPayloads)
{
    public string ToCanonicalJson()
    {
        List<string> ordered = [.. ToolPayloads.Order(StringComparer.Ordinal)];
        return JsonSerializer.Serialize(new
        {
            model = Model,
            instructions = Instructions,
            tools = ordered
        });
    }

    public static AgentStructuralSignature FromDefinition(AgentDefinition definition)
    {
        BinaryData data = ModelReaderWriter.Write(definition);
        using JsonDocument doc = JsonDocument.Parse(data);

        JsonElement root = doc.RootElement;
        string model = ReadString(root, "model", "Model");
        string instructions = NormalizeInstructions(ReadString(root, "instructions", "Instructions"));
        List<string> toolPayloads = [];

        if (TryGetPropertyCaseInsensitive(root, "tools", out JsonElement tools) &&
            tools.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement tool in tools.EnumerateArray())
            {
                toolPayloads.Add(SerializeCanonicalJson(tool));
            }
        }

        toolPayloads.Sort(StringComparer.Ordinal);
        return new AgentStructuralSignature(model, instructions, toolPayloads);
    }

    private static string NormalizeInstructions(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
             .Replace('\r', '\n')
             .Trim();

    private static string SerializeCanonicalJson(JsonElement value)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream);
        WriteCanonicalValue(writer, value);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (TryGetPropertyCaseInsensitive(element, name, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
