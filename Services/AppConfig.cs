namespace CasoD.Services;

internal sealed record AppConfig(
    Uri ProjectEndpoint,
    string ModelDeploymentName,
    string OrderAgentId,
    string? RefundAgentId);
