using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using OpenAI.Responses;

namespace CasoD.Services;

internal sealed class SmokeTestRunner
{
    private static readonly string[] SmokePrompts =
    [
        "Dame el estado de ORD-0001",
        "Quiero reembolso de ORD-0001",
        "Tengo un problema con mi compra"
    ];

    private readonly AIProjectClient _projectClient;
    private readonly ResponsePollingService _pollingService;
    private readonly AgentResponseInspector _inspector;
    private readonly ConsoleLog _log;

    public SmokeTestRunner(
        AIProjectClient projectClient,
        ResponsePollingService pollingService,
        AgentResponseInspector inspector,
        ConsoleLog log)
    {
        _projectClient = projectClient;
        _pollingService = pollingService;
        _inspector = inspector;
        _log = log;
    }

    public async Task RunAsync(
        string managerAgentName,
        IReadOnlyDictionary<string, string> labelsByAgentId,
        IReadOnlyDictionary<string, string> labelsByAgentName,
        CancellationToken cancellationToken)
    {
        ProjectResponsesClient responseClient = _projectClient.OpenAI.GetProjectResponsesClientForAgent(managerAgentName);

        for (int i = 0; i < SmokePrompts.Length; i++)
        {
            string prompt = SmokePrompts[i];
            _log.Info("SMOKE_TEST", $"Prompt[{i + 1}] => {prompt}");

            CreateResponseOptions options = new(
                [ResponseItem.CreateUserMessageItem(prompt)]);

            ResponseResult initialResponse = await responseClient.CreateResponseAsync(options, cancellationToken);
            ResponseResult finalResponse = await _pollingService.WaitForTerminalAsync(
                responseClient,
                initialResponse,
                timeout: TimeSpan.FromSeconds(60),
                cancellationToken: cancellationToken);

            string detectedAgent = _inspector.DetectRespondingAgent(
                finalResponse,
                labelsByAgentId,
                labelsByAgentName);

            Console.WriteLine($"ResponseId: {finalResponse.Id}");
            Console.WriteLine($"ResponderAgent: {detectedAgent}");
            Console.WriteLine($"FinalText: {finalResponse.GetOutputText()}");
            Console.WriteLine();
        }
    }
}
