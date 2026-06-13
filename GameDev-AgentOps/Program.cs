using DotNetEnv;
using Microsoft.Extensions.Logging;
using Serilog;
using AutomationAgent;
using GameDev_AgentOps;

Env.Load();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/agent-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSerilog(Log.Logger);
});

var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("ProductionAgent 시작");

var baseAgent = AIAgentBuilder.FromEnvironment()
    .Build("ProductionAgent", "프로덕션 레벨의 안정적인 어시스턴트다.");

var resilientAgent = new ResilientAgent(baseAgent, logger);
var rateLimiter = new RateLimiter(maxConcurrent: 3, maxPerMinute: 18);
var metrics = new AgentMetrics();

Console.WriteLine("✅ ProductionAgent 준비 완료");
Console.WriteLine("종료: quit | 메트릭: stats");
Console.WriteLine();

while (true)
{
    Console.Write("💬 입력: ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    if (input.Equals("stats", StringComparison.OrdinalIgnoreCase))
    {
        metrics.PrintReport();
        continue;
    }

    var validation = InputValidator.Validate(input);
    if (!validation.isValid)
    {
        Console.WriteLine($"⚠️ {validation.error}");
        Console.WriteLine();
        continue;
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        var (response, success) = await rateLimiter.ExecuteAsync(async () =>
            await resilientAgent.SafeRunAsync(input));

        sw.Stop();

        if (success)
            metrics.RecordSuccess(sw.ElapsedMilliseconds);
        else
            metrics.RecordFailure();

        Console.WriteLine();
        Console.WriteLine($"🤖 Agent: {response}");
        Console.WriteLine();
    }
    catch (InvalidOperationException ex)
    {
        sw.Stop();
        metrics.RecordFailure();

        logger.LogWarning(ex, "RateLimiter에서 요청 차단");
        Console.WriteLine();
        Console.WriteLine($"⚠️ {ex.Message}");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        sw.Stop();
        metrics.RecordFailure();

        logger.LogError(ex, "예상치 못한 오류");
        Console.WriteLine();
        Console.WriteLine($"❌ 오류: {ex.Message}");
        Console.WriteLine();
    }
}

metrics.PrintReport();
logger.LogInformation("ProductionAgent 종료");

Log.CloseAndFlush();