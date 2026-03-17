using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects.OpenAI;

namespace CasoD.Agents;

internal sealed record AgentStructuralSignature(
    string Model,
    string Instructions,
    IReadOnlyList<string> ToolAgentIds)
{
    public string ToCanonicalJson()
    {
        List<string> ordered = [.. ToolAgentIds.Order(StringComparer.Ordinal)];
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
        List<string> toolAgentIds = [];

        if (TryGetPropertyCaseInsensitive(root, "tools", out JsonElement tools) &&
            tools.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement tool in tools.EnumerateArray())
            {
                string type = ReadString(tool, "type", "Type");
                if (!string.Equals(type, "agent", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string agentId = ReadString(tool, "agent_id", "agentId", "AgentId");
                if (!string.IsNullOrWhiteSpace(agentId))
                {
                    toolAgentIds.Add(agentId.Trim());
                }
            }
        }

        toolAgentIds.Sort(StringComparer.Ordinal);
        return new AgentStructuralSignature(model, instructions, toolAgentIds);
    }

    private static string NormalizeInstructions(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
             .Replace('\r', '\n')
             .Trim();

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
