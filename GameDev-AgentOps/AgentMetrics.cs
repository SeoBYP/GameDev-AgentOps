namespace AutomationAgent;

public class AgentMetrics
{
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _totalLatencyMs;

    public void RecordSuccess(long latencyMs)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _successfulRequests);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _failedRequests);
    }

    public void PrintReport()
    {
        var total = Interlocked.Read(ref _totalRequests);
        var success = Interlocked.Read(ref _successfulRequests);
        var failed = Interlocked.Read(ref _failedRequests);
        var totalLatency = Interlocked.Read(ref _totalLatencyMs);

        var avgLatency = total > 0 ? totalLatency / total : 0;
        var successRate = total > 0 ? 100.0 * success / total : 0;

        Console.WriteLine($"""
                           📊 Agent 성능 보고서
                             총 요청: {total:N0}
                             성공: {success:N0} ({successRate:F1}%)
                             실패: {failed:N0}
                             평균 응답 시간: {avgLatency}ms
                           """);
    }
}