namespace CasoD.Agents;

internal enum ReconciliationStatus
{
    Created,
    Updated,
    Unchanged,
    ExternalValidated
}

internal sealed record ReconciliationResult(
    string AgentName,
    string AgentId,
    string Version,
    ReconciliationStatus Status,
    string Signature)
{
    public static ReconciliationResult CreateExternalValidated(
        string agentName,
        string agentId,
        string version) =>
        new(
            AgentName: agentName,
            AgentId: agentId,
            Version: version,
            Status: ReconciliationStatus.ExternalValidated,
            Signature: "external");
}
