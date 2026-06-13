using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AutomationAgent;

public class ResilientAgent
{
    private readonly AIAgent _agent;
    private readonly AsyncRetryPolicy<string> _retryPolicy;
    private readonly ILogger _logger;

    public ResilientAgent(AIAgent agent, ILogger logger)
    {
        _agent = agent;
        _logger = logger;

        _retryPolicy = Policy<string>
            .Handle<ClientResultException>(IsRetryableClientError)
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timeSpan, attempt, context) =>
                {
                    _logger.LogWarning(
                        outcome.Exception,
                        "Agent 요청 실패, 재시도 {Attempt}/3 | 대기 {WaitSeconds}s",
                        attempt,
                        timeSpan.TotalSeconds);
                });
    }

    public async Task<string> RunWithRetryAsync(string input)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            AgentResponse result = await _agent.RunAsync(input);
            return result.Text;
        });
    }

    public async Task<(string response, bool success)> SafeRunAsync(string input)
    {
        try
        {
            var result = await RunWithRetryAsync(input);
            return (result, true);
        }
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            _logger.LogError(ex, "API 인증 실패");
            return ("API 인증에 실패했습니다. API 키를 확인하세요.", false);
        }
        catch (ClientResultException ex) when (ex.Status == 402)
        {
            _logger.LogError(ex, "API 크레딧 또는 결제 문제");
            return ("API 크레딧 또는 결제 상태를 확인하세요.", false);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning(ex, "Rate Limit 초과");
            return ("요청이 너무 많습니다. 잠시 후 다시 시도하세요.", false);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Agent 응답 시간 초과");
            return ("응답 시간이 초과되었습니다. 더 간단한 질문으로 다시 시도하세요.", false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "예상치 못한 Agent 오류");
            return ("일시적인 오류가 발생했습니다. 잠시 후 다시 시도하세요.", false);
        }
    }

    private static bool IsRetryableClientError(ClientResultException ex)
    {
        return ex.Status == 429 ||
               ex.Status == 500 ||
               ex.Status == 502 ||
               ex.Status == 503 ||
               ex.Status == 504;
    }
}