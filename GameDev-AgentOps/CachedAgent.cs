using Microsoft.Agents.AI;

namespace GameDev_AgentOps;

public class CachedAgent
{
    private readonly AIAgent _agent;
    private readonly Dictionary<string, (string response, DateTime expires)> _cache = new();
    private readonly TimeSpan _cacheDuration;

    public CachedAgent(AIAgent agent, TimeSpan? cacheDuration = null)
    {
        _agent = agent;
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(10);
    }

    public async Task<string> RunAsync(string input)
    {
        var key = ComputeHash(input);

        if (_cache.TryGetValue(key, out var cached) && cached.expires > DateTime.Now)
        {
            Console.WriteLine("  [캐시 히트]");
            return cached.response;
        }

        var result = await _agent.RunAsync(input);
        _cache[key] = (result.Text, DateTime.Now.Add(_cacheDuration));
        return result.Text;
    }

    private string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}