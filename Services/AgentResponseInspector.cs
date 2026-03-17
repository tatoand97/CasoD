using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects.OpenAI;
using OpenAI.Responses;

namespace CasoD.Services;

internal sealed class AgentResponseInspector
{
    public string DetectRespondingAgent(
        ResponseResult response,
        IReadOnlyDictionary<string, string> labelsByAgentId,
        IReadOnlyDictionary<string, string> labelsByAgentName)
    {
        string detected = "unknown";

        foreach (ResponseItem item in response.OutputItems)
        {
            if (TryResolveByAgentItem(item, labelsByAgentName, out string byAgentItem))
            {
                detected = byAgentItem;
            }

            if (TryResolveByRawJson(item, labelsByAgentId, labelsByAgentName, out string byRawJson))
            {
                detected = byRawJson;
            }
        }

        return detected;
    }

    private static bool TryResolveByAgentItem(
        ResponseItem item,
        IReadOnlyDictionary<string, string> labelsByAgentName,
        out string detected)
    {
        try
        {
            AgentResponseItem agentItem = item.AsAgentResponseItem();
            string? agentName = agentItem.AgentReference?.Name;

            if (!string.IsNullOrWhiteSpace(agentName))
            {
                detected = labelsByAgentName.TryGetValue(agentName, out string? label)
                    ? label
                    : agentName;
                return true;
            }
        }
        catch
        {
        }

        detected = string.Empty;
        return false;
    }

    private static bool TryResolveByRawJson(
        ResponseItem item,
        IReadOnlyDictionary<string, string> labelsByAgentId,
        IReadOnlyDictionary<string, string> labelsByAgentName,
        out string detected)
    {
        detected = string.Empty;

        BinaryData raw;
        try
        {
            raw = ModelReaderWriter.Write(item);
        }
        catch
        {
            return false;
        }

        using JsonDocument doc = JsonDocument.Parse(raw);
        JsonElement root = doc.RootElement;

        string agentId = ReadString(root, "agent_id", "agentId", "AgentId");
        if (!string.IsNullOrWhiteSpace(agentId))
        {
            if (labelsByAgentId.TryGetValue(agentId, out string? label))
            {
                detected = label;
                return true;
            }

            detected = agentId;
            return true;
        }

        if (TryGetProperty(root, "agent_reference", out JsonElement agentReference))
        {
            string name = ReadString(agentReference, "name", "Name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                detected = labelsByAgentName.TryGetValue(name, out string? label)
                    ? label
                    : name;
                return true;
            }
        }

        return false;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (TryGetProperty(element, name, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
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
