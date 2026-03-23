using Azure.AI.Projects.OpenAI;

namespace CasoD.Agents;

internal static class AgentDefinitionFactory
{
    public static PromptAgentDefinition CreateRouter(string modelDeployment)
    {
        ValidateModel(modelDeployment);
        return new PromptAgentDefinition(modelDeployment)
        {
            Instructions = AgentInstructionTemplates.Router
        };
    }

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

    private static void ValidateModel(string modelDeployment)
    {
        if (string.IsNullOrWhiteSpace(modelDeployment))
        {
            throw new InvalidOperationException("Model deployment cannot be empty.");
        }
    }
}
