using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using CasoD.Agents;
using CasoD.Services;
using System.ClientModel;

ConsoleLog log = new();
using CancellationTokenSource cancellationTokenSource = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

try
{
    AppConfig config = AppConfigLoader.Load();
    log.Info("CONFIG", $"Endpoint: {config.ProjectEndpoint}");
    log.Info("CONFIG", $"Deployment: {config.ModelDeploymentName}");
    log.Info("CONFIG", $"ORDER_AGENT_ID provided: {!string.IsNullOrWhiteSpace(config.OrderAgentId)}");
    log.Info("CONFIG", $"REFUND_AGENT_ID provided: {!string.IsNullOrWhiteSpace(config.RefundAgentId)}");

    DefaultAzureCredential credential = new(new DefaultAzureCredentialOptions
    {
        ExcludeInteractiveBrowserCredential = true
    });

    AIProjectClient projectClient = new(config.ProjectEndpoint, credential);

    AgentValidationService validationService = new(projectClient, log);
    AgentReconciliationService reconciliationService = new(projectClient, log);
    ResponsePollingService pollingService = new(log);
    AgentResponseInspector inspector = new();
    SmokeTestRunner smokeTestRunner = new(projectClient, pollingService, inspector, log);

    await validationService.ValidateProjectAccessAsync(cancellationTokenSource.Token);

    ResolvedAgentIdentity orderIdentity = await validationService.ValidateOrderAgentAsync(
        config.OrderAgentId,
        cancellationTokenSource.Token);

    ReconciliationResult orderStatus = ReconciliationResult.CreateExternalValidated(
        agentName: orderIdentity.AgentName,
        agentId: orderIdentity.AgentId,
        version: orderIdentity.AgentVersion);

    ResolvedAgentIdentity refundIdentity;
    ReconciliationResult refundStatus;

    if (!string.IsNullOrWhiteSpace(config.RefundAgentId))
    {
        refundIdentity = await validationService.ValidateAgentIdAccessibleAsync(
            config.RefundAgentId,
            "REFUND_AGENT_ID",
            cancellationTokenSource.Token);

        refundStatus = ReconciliationResult.CreateExternalValidated(
            agentName: refundIdentity.AgentName,
            agentId: refundIdentity.AgentId,
            version: refundIdentity.AgentVersion);
    }
    else
    {
        PromptAgentDefinition refundDefinition = AgentDefinitionFactory.CreateRefund(config.ModelDeploymentName);
        refundStatus = await reconciliationService.ReconcileAsync(
            AgentNames.Refund,
            refundDefinition,
            cancellationTokenSource.Token);

        refundIdentity = new ResolvedAgentIdentity(
            AgentId: refundStatus.AgentId,
            AgentName: refundStatus.AgentName,
            AgentVersion: refundStatus.Version);
    }

    PromptAgentDefinition clarifierDefinition = AgentDefinitionFactory.CreateClarifier(config.ModelDeploymentName);
    ReconciliationResult clarifierStatus = await reconciliationService.ReconcileAsync(
        AgentNames.Clarifier,
        clarifierDefinition,
        cancellationTokenSource.Token);

    PromptAgentDefinition managerDefinition = AgentDefinitionFactory.CreateManager(
        modelDeployment: config.ModelDeploymentName,
        orderAgentId: orderIdentity.AgentId,
        refundAgentId: refundIdentity.AgentId,
        clarifierAgentId: clarifierStatus.AgentId);

    ReconciliationResult managerStatus = await reconciliationService.ReconcileAsync(
        AgentNames.Manager,
        managerDefinition,
        cancellationTokenSource.Token);

    PrintReconciliationSummary(orderStatus, refundStatus, clarifierStatus, managerStatus);

    Dictionary<string, string> labelsByAgentId = new(StringComparer.OrdinalIgnoreCase)
    {
        [orderIdentity.AgentId] = "OrderAgent",
        [refundIdentity.AgentId] = "RefundAgent",
        [clarifierStatus.AgentId] = "ClarifierAgent",
        [managerStatus.AgentId] = "ManagerAgent"
    };

    Dictionary<string, string> labelsByAgentName = new(StringComparer.OrdinalIgnoreCase)
    {
        [orderIdentity.AgentName] = "OrderAgent",
        [refundIdentity.AgentName] = "RefundAgent",
        [clarifierStatus.AgentName] = "ClarifierAgent",
        [managerStatus.AgentName] = "ManagerAgent"
    };

    await smokeTestRunner.RunAsync(
        managerAgentName: managerStatus.AgentName,
        labelsByAgentId: labelsByAgentId,
        labelsByAgentName: labelsByAgentName,
        cancellationToken: cancellationTokenSource.Token);

    log.Info("SMOKE_TEST", "Completed.");
}
catch (TimeoutException ex)
{
    log.Error("SMOKE_TEST", ex.Message);
    Environment.ExitCode = 1;
}
catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
{
    log.Warn("RUNTIME", "Operation cancelled by user.");
    Environment.ExitCode = 1;
}
catch (ClientResultException ex)
{
    WriteClientError(ex, log);
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    log.Error("RUNTIME", "Unexpected failure.");
    log.Error("RUNTIME", ex.ToString());
    Environment.ExitCode = 1;
}

static void PrintReconciliationSummary(params ReconciliationResult[] results)
{
    Console.WriteLine("Reconciliation Summary");
    foreach (ReconciliationResult result in results)
    {
        Console.WriteLine(
            $"{result.AgentName} | ReconciliationStatus={result.Status} | AgentId={result.AgentId} | Version={result.Version}");
    }
    Console.WriteLine();
}

static void WriteClientError(ClientResultException ex, ConsoleLog log)
{
    log.Error("AZURE", "Azure request failed.");
    log.Error("AZURE", $"Status: {ex.Status}");
    log.Error("AZURE", $"Message: {ex.Message}");

    if (ex.Status is 401 or 403)
    {
        log.Error("AZURE", "Authorization failed. Ensure the principal has Azure AI Developer (or equivalent) role.");
    }

    if (ex.Status == 404)
    {
        log.Error("AZURE", "Resource not found. Validate agent names/IDs and model deployment.");
    }

    if (ex.GetRawResponse() is { } rawResponse)
    {
        string requestId = rawResponse.Headers.TryGetValue("x-request-id", out string? rid)
            ? rid ?? "(unavailable)"
            : "(unavailable)";
        string clientRequestId = rawResponse.Headers.TryGetValue("x-ms-client-request-id", out string? crid)
            ? crid ?? "(unavailable)"
            : "(unavailable)";

        log.Error("AZURE", $"RequestId: {requestId}");
        log.Error("AZURE", $"ClientRequestId: {clientRequestId}");
    }
}
