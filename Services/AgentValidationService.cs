using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using System.ClientModel;

namespace CasoD.Services;

internal sealed record ResolvedAgentIdentity(
    string AgentId,
    string AgentName,
    string AgentVersion);

internal sealed class AgentValidationService
{
    private readonly AIProjectClient _projectClient;
    private readonly ConsoleLog _log;

    public AgentValidationService(AIProjectClient projectClient, ConsoleLog log)
    {
        _projectClient = projectClient;
        _log = log;
    }

    public async Task ValidateProjectAccessAsync(CancellationToken cancellationToken)
    {
        _log.Info("VALIDATION", "Validating identity access to Foundry project.");
        await foreach (AgentRecord _ in _projectClient.Agents.GetAgentsAsync(limit: 1, cancellationToken: cancellationToken))
        {
            break;
        }
        _log.Info("VALIDATION", "Project access validated.");
    }

    public Task<ResolvedAgentIdentity> ValidateOrderAgentAsync(string orderAgentId, CancellationToken cancellationToken) =>
        ValidateAgentIdAccessibleAsync(orderAgentId, "ORDER_AGENT_ID", cancellationToken);

    public async Task<ResolvedAgentIdentity> ValidateAgentIdAccessibleAsync(
        string agentId,
        string variableName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new InvalidOperationException($"{variableName} cannot be empty.");
        }

        string trimmedAgentId = agentId.Trim();
        _log.Info("VALIDATION", $"Validating {variableName}='{trimmedAgentId}'.");

        ResolvedAgentIdentity? resolved = await TryResolveAgentIdAsync(trimmedAgentId, cancellationToken);
        if (resolved is null)
        {
            throw new InvalidOperationException(
                $"{variableName}='{trimmedAgentId}' was not found in the project or is not accessible.");
        }

        _log.Info(
            "VALIDATION",
            $"{variableName} validated. Name='{resolved.AgentName}', Version='{resolved.AgentVersion}', Id='{resolved.AgentId}'.");

        return resolved;
    }

    private async Task<ResolvedAgentIdentity?> TryResolveAgentIdAsync(string agentId, CancellationToken cancellationToken)
    {
        foreach ((string Name, string Version) candidate in GetNameVersionCandidates(agentId))
        {
            try
            {
                AgentVersion version = await _projectClient.Agents.GetAgentVersionAsync(
                    agentName: candidate.Name,
                    agentVersion: candidate.Version,
                    cancellationToken: cancellationToken);

                if (string.Equals(version.Id, agentId, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResolvedAgentIdentity(version.Id, version.Name, version.Version);
                }
            }
            catch (ClientResultException ex) when (ex.Status == 404)
            {
            }
        }

        await foreach (AgentRecord agent in _projectClient.Agents.GetAgentsAsync(
            limit: 100,
            order: AgentListOrder.Descending,
            cancellationToken: cancellationToken))
        {
            await foreach (AgentVersion version in _projectClient.Agents.GetAgentVersionsAsync(
                agentName: agent.Name,
                limit: 100,
                order: AgentListOrder.Descending,
                cancellationToken: cancellationToken))
            {
                if (string.Equals(version.Id, agentId, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResolvedAgentIdentity(version.Id, version.Name, version.Version);
                }
            }
        }

        return null;
    }

    private static IEnumerable<(string Name, string Version)> GetNameVersionCandidates(string agentId)
    {
        int colonIndex = agentId.IndexOf(':');
        if (colonIndex > 0 && colonIndex < agentId.Length - 1)
        {
            yield return (agentId[..colonIndex], agentId[(colonIndex + 1)..]);
        }

        int atIndex = agentId.IndexOf('@');
        if (atIndex > 0 && atIndex < agentId.Length - 1)
        {
            yield return (agentId[..atIndex], agentId[(atIndex + 1)..]);
        }
    }
}
