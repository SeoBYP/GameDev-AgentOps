using Microsoft.Agents.AI;

namespace GameDev_AgentOps;

public class BatchProcessor
{
    private readonly AIAgent _agent;
    private readonly int _batchSize;
    private readonly int _delayBetweenBatches;

    public BatchProcessor(AIAgent agent, int batchSize = 5,
        int delayMs = 1000)
    {
        _agent = agent;
        _batchSize = batchSize;
        _delayBetweenBatches = delayMs;
    }

    public async Task<List<string>> ProcessBatchAsync(
        IEnumerable<string> inputs)
    {
        var results = new List<string>();
        var batches = inputs.Chunk(_batchSize);

        int batchNum = 0;
        foreach (var batch in batches)
        {
            batchNum++;
            Console.WriteLine($"배치 {batchNum} 처리 중 ({batch.Length}개)...");

            var tasks = batch.Select(input => _agent.RunAsync(input));
            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults.Select(r => r.Text));

            if (batchNum < batches.Count())
                await Task.Delay(_delayBetweenBatches);
        }

        return results;
    }
}