using Azure.AI.Projects.OpenAI;
using OpenAI.Responses;

namespace CasoD.Services;

internal sealed class ResponsePollingService
{
    private readonly ConsoleLog _log;

    public ResponsePollingService(ConsoleLog log) => _log = log;

    public async Task<ResponseResult> WaitForTerminalAsync(
        ProjectResponsesClient responsesClient,
        ResponseResult initialResponse,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (IsTerminal(initialResponse))
        {
            return initialResponse;
        }

        using CancellationTokenSource timeoutCts = new(timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        ResponseResult latest = initialResponse;
        int delayMilliseconds = 500;

        while (true)
        {
            try
            {
                int jitterMs = Random.Shared.Next(0, 251);
                await Task.Delay(delayMilliseconds + jitterMs, linkedCts.Token);

                latest = await responsesClient.GetResponseAsync(latest.Id, linkedCts.Token);

                if (IsTerminal(latest))
                {
                    return latest;
                }

                delayMilliseconds = Math.Min(delayMilliseconds * 2, 8000);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Response polling timed out after {timeout.TotalSeconds:0} seconds for ResponseId='{latest.Id}'.");
            }
        }
    }

    private static bool IsTerminal(ResponseResult response)
    {
        if (response.Status is null)
        {
            return true;
        }

        return response.Status.Value is
            ResponseStatus.Completed or
            ResponseStatus.Cancelled or
            ResponseStatus.Failed or
            ResponseStatus.Incomplete;
    }
}
