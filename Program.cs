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

    DefaultAzureCredential credential = new(new DefaultAzureCredentialOptions
    {
        ExcludeInteractiveBrowserCredential = true
    });

    AIProjectClient projectClient = new(config.ProjectEndpoint, credential);

    AgentValidationService validationService = new(projectClient, log);
    AgentReconciliationService reconciliationService = new(projectClient, log);

    await validationService.ValidateProjectAccessAsync(cancellationTokenSource.Token);

    ResolvedAgentIdentity orderIdentity = await validationService.ValidateOrderAgentAsync(
        config.OrderAgentId,
        cancellationTokenSource.Token);

    ReconciliationResult orderStatus = ReconciliationResult.CreateExternalValidated(
        agentName: orderIdentity.AgentName,
        agentId: orderIdentity.AgentId,
        version: orderIdentity.AgentVersion);

    PromptAgentDefinition routerDefinition = AgentDefinitionFactory.CreateRouter(config.ModelDeploymentName);
    ReconciliationResult routerStatus = await reconciliationService.ReconcileAsync(
        AgentNames.Router,
        routerDefinition,
        cancellationTokenSource.Token);

    PromptAgentDefinition refundDefinition = AgentDefinitionFactory.CreateRefund(config.ModelDeploymentName);
    ReconciliationResult refundStatus = await reconciliationService.ReconcileAsync(
        AgentNames.Refund,
        refundDefinition,
        cancellationTokenSource.Token);

    PromptAgentDefinition clarifierDefinition = AgentDefinitionFactory.CreateClarifier(config.ModelDeploymentName);
    ReconciliationResult clarifierStatus = await reconciliationService.ReconcileAsync(
        AgentNames.Clarifier,
        clarifierDefinition,
        cancellationTokenSource.Token);

    PrintReconciliationSummary(orderStatus, routerStatus, refundStatus, clarifierStatus);
    PrintWorkflowBindings(orderIdentity, routerStatus, refundStatus, clarifierStatus);
    log.Info("BOOTSTRAP", "Workflow-first D.2 bootstrap completed.");
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

static void PrintWorkflowBindings(
    ResolvedAgentIdentity orderIdentity,
    ReconciliationResult routerStatus,
    ReconciliationResult refundStatus,
    ReconciliationResult clarifierStatus)
{
    Console.WriteLine("Workflow Binding Summary");
    Console.WriteLine("Authoritative workflow asset: workflows/caso-d-router.workflow.yaml");
    Console.WriteLine($"FOUNDRY_AGENT_ROUTER={routerStatus.AgentName} | AgentId={routerStatus.AgentId}");
    Console.WriteLine($"FOUNDRY_AGENT_ORDER={orderIdentity.AgentName} | AgentId={orderIdentity.AgentId}");
    Console.WriteLine($"FOUNDRY_AGENT_REFUND={refundStatus.AgentName} | AgentId={refundStatus.AgentId}");
    Console.WriteLine($"FOUNDRY_AGENT_CLARIFIER={clarifierStatus.AgentName} | AgentId={clarifierStatus.AgentId}");
    Console.WriteLine();
    Console.WriteLine("Use agent names above when wiring the workflow in Foundry or VS Code.");
    Console.WriteLine("This program validates agents and prints bindings only; it is not the D.2 runtime router.");
    Console.WriteLine();
}
