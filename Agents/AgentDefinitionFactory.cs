using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects.OpenAI;
using OpenAI.Responses;

namespace CasoD.Agents;

internal static class AgentDefinitionFactory
{
    public static PromptAgentDefinition CreateClarifier(string modelDeployment)
    {
        ValidateModel(modelDeployment);
        return new PromptAgentDefinition(modelDeployment)
        {
            Instructions = AgentInstructionTemplates.Clarifier
        };
    }

    public static PromptAgentDefinition CreateRefund(string modelDeployment)
    {
        ValidateModel(modelDeployment);
        return new PromptAgentDefinition(modelDeployment)
        {
            Instructions = AgentInstructionTemplates.Refund
        };
    }

    public static PromptAgentDefinition CreateManager(
        string modelDeployment,
        string orderAgentId,
        string refundAgentId,
        string clarifierAgentId)
    {
        ValidateModel(modelDeployment);
        ValidateAgentId(orderAgentId, nameof(orderAgentId));
        ValidateAgentId(refundAgentId, nameof(refundAgentId));
        ValidateAgentId(clarifierAgentId, nameof(clarifierAgentId));

        PromptAgentDefinition definition = new(modelDeployment)
        {
            Instructions = AgentInstructionTemplates.Manager
        };

        definition.Tools.Add(CreateAgentTool(orderAgentId));
        definition.Tools.Add(CreateAgentTool(refundAgentId));
        definition.Tools.Add(CreateAgentTool(clarifierAgentId));

        return definition;
    }

    private static ResponseTool CreateAgentTool(string agentId)
    {
        string safeAgentId = agentId.Trim();
        string agentIdJson = JsonSerializer.Serialize(safeAgentId);
        BinaryData payload = BinaryData.FromString($$"""{"type":"agent","agent_id":{{agentIdJson}}}""");

        return ModelReaderWriter.Read<ResponseTool>(payload)
               ?? throw new InvalidOperationException($"Unable to create agent tool for agent_id '{safeAgentId}'.");
    }

    private static void ValidateModel(string modelDeployment)
    {
        if (string.IsNullOrWhiteSpace(modelDeployment))
        {
            throw new InvalidOperationException("Model deployment cannot be empty.");
        }
    }

    private static void ValidateAgentId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Agent id cannot be empty.", parameterName);
        }
    }
}
