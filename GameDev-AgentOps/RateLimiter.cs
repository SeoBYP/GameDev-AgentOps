namespace AutomationAgent;

public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly int _maxRequestsPerMinute;
    private readonly object _lock = new();

    public RateLimiter(int maxConcurrent = 3, int maxPerMinute = 18)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent);
        _maxRequestsPerMinute = maxPerMinute;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        await _semaphore.WaitAsync();

        try
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var oneMinuteAgo = now.AddMinutes(-1);

                while (_requestTimes.Count > 0 &&
                       _requestTimes.Peek() < oneMinuteAgo)
                {
                    _requestTimes.Dequeue();
                }

                if (_requestTimes.Count >= _maxRequestsPerMinute)
                {
                    throw new InvalidOperationException(
                        "Rate limit 초과. 잠시 후 재시도하세요.");
                }

                _requestTimes.Enqueue(now);
            }

            return await action();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}