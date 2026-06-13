using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace AutomationAgent;

public class LoggedAgent
{
    private readonly AIAgent _agent;
    private readonly ILogger _logger;
    private int _requestCount;

    public LoggedAgent(AIAgent agent, ILogger logger)
    {
        _agent = agent;
        _logger = logger;
    }

    public async Task<string> RunAsync(string input)
    {
        var requestId = Interlocked.Increment(ref _requestCount);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "Agent 요청 시작 | RequestId={RequestId} | InputLength={InputLength}",
            requestId,
            input.Length);

        try
        {
            AgentResponse result = await _agent.RunAsync(input);

            sw.Stop();

            _logger.LogInformation(
                "Agent 요청 완료 | RequestId={RequestId} | DurationMs={DurationMs} | OutputLength={OutputLength}",
                requestId,
                sw.ElapsedMilliseconds,
                result.Text.Length);

            return result.Text;
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "Agent 요청 실패 | RequestId={RequestId} | DurationMs={DurationMs}",
                requestId,
                sw.ElapsedMilliseconds);

            throw;
        }
    }
}