using DotNetEnv;
using GameDev_AgentOps;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using MultiAgent;
using OpenAI;
using AIAgentBuilder = GameDev_AgentOps.AIAgentBuilder;

Env.Load();

Console.WriteLine("🤖 Multi-Agent Workflow 데모");
Console.WriteLine(new string('=', 60));
Console.WriteLine("1. 순차 보고서 생성 Workflow");
Console.WriteLine("2. 병렬 분석 Workflow");
Console.WriteLine("3. 조건부 라우팅 Workflow");
Console.Write("\n선택 (1/2/3): ");


var choice = Console.ReadLine()?.Trim();

switch (choice)
{
    case "1":
        Console.Write("\n보고서 주제를 입력하라: ");
        var topic = Console.ReadLine() ?? "AI 에이전트 기술 동향";

        var reportWorkflow = new ReportGenerationWorkflow();
        var report = await reportWorkflow.GenerateReportAsync(topic);

        // 보고서 저장
        var fileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.md";
        File.WriteAllText(fileName, report);
        Console.WriteLine($"\n\n💾 보고서 저장됨: {fileName}");
        break;
    case "2":
        Console.Write("\n분석 주제를 입력하라: ");
        var analysisTopic = Console.ReadLine() ?? "C#과 Go의 게임 서버 적합성 비교";

        var parallelWorkflow = new ParallelAnalysisWorkflow();
        await parallelWorkflow.AnalyzeAsync(analysisTopic);
        break;

    case "3":
        Console.WriteLine("\n조건부 라우팅 테스트 (종료: quit)");
        var routingWorkflow = new ConditionalRoutingWorkflow();

        while (true)
        {
            Console.Write("\n❓ 질문: ");
            var question = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(question) || question == "quit") break;
            await routingWorkflow.ProcessAsync(question);
        }
        break;
    default:
        Console.WriteLine("잘못된 선택이다.");
        break;
}