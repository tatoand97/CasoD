using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using CasoD.Agents;
using System.ClientModel;

namespace CasoD.Services;

internal sealed class AgentReconciliationService
{
    private readonly AIProjectClient _projectClient;
    private readonly ConsoleLog _log;

    public AgentReconciliationService(AIProjectClient projectClient, ConsoleLog log)
    {
        _projectClient = projectClient;
        _log = log;
    }

    public async Task<ReconciliationResult> ReconcileAsync(
        string agentName,
        PromptAgentDefinition desiredDefinition,
        CancellationToken cancellationToken)
    {
        AgentStructuralSignature desiredSignature = AgentStructuralSignature.FromDefinition(desiredDefinition);
        string desiredSignatureJson = desiredSignature.ToCanonicalJson();

        AgentVersion? latest = await TryGetLatestAgentVersionAsync(agentName, cancellationToken);
        if (latest is null)
        {
            AgentVersion created = await _projectClient.Agents.CreateAgentVersionAsync(
                agentName: agentName,
                options: new AgentVersionCreationOptions(desiredDefinition),
                cancellationToken: cancellationToken);

            _log.Info("RECONCILE", $"{agentName} => {ReconciliationStatus.Created}");
            return new ReconciliationResult(
                AgentName: created.Name,
                AgentId: created.Id,
                Version: created.Version,
                Status: ReconciliationStatus.Created,
                Signature: desiredSignatureJson);
        }

        AgentStructuralSignature currentSignature = AgentStructuralSignature.FromDefinition(latest.Definition);
        string currentSignatureJson = currentSignature.ToCanonicalJson();

        if (string.Equals(currentSignatureJson, desiredSignatureJson, StringComparison.Ordinal))
        {
            _log.Info("RECONCILE", $"{agentName} => {ReconciliationStatus.Unchanged}");
            return new ReconciliationResult(
                AgentName: latest.Name,
                AgentId: latest.Id,
                Version: latest.Version,
                Status: ReconciliationStatus.Unchanged,
                Signature: currentSignatureJson);
        }

        AgentVersion updated = await _projectClient.Agents.CreateAgentVersionAsync(
            agentName: agentName,
            options: new AgentVersionCreationOptions(desiredDefinition),
            cancellationToken: cancellationToken);

        _log.Info("RECONCILE", $"{agentName} => {ReconciliationStatus.Updated}");
        return new ReconciliationResult(
            AgentName: updated.Name,
            AgentId: updated.Id,
            Version: updated.Version,
            Status: ReconciliationStatus.Updated,
            Signature: desiredSignatureJson);
    }

    private async Task<AgentVersion?> TryGetLatestAgentVersionAsync(string agentName, CancellationToken cancellationToken)
    {
        try
        {
            _ = await _projectClient.Agents.GetAgentAsync(agentName, cancellationToken);
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            return null;
        }

        await foreach (AgentVersion version in _projectClient.Agents.GetAgentVersionsAsync(
            agentName: agentName,
            limit: 1,
            order: AgentListOrder.Descending,
            cancellationToken: cancellationToken))
        {
            return version;
        }

        return null;
    }
}
