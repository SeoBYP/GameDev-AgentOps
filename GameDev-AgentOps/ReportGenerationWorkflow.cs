using GameDev_AgentOps;
using Microsoft.Agents.AI;

namespace MultiAgent;

/// <summary>순차 실행 보고서 생성 Workflow</summary>
public class ReportGenerationWorkflow
{
    private readonly AIAgent _researcher;
    private readonly AIAgent _analyst;
    private readonly AIAgent _writer;
    private readonly AIAgent _reviewer;

    public ReportGenerationWorkflow()
    {
        _researcher = AgentFactory.CreateResearchAgent();
        _analyst    = AgentFactory.CreateAnalystAgent();
        _writer     = AgentFactory.CreateWriterAgent();
        _reviewer   = AgentFactory.CreateReviewerAgent();
    }

    public async Task<string> GenerateReportAsync(string topic,
        bool streamOutput = true)
    {
        Console.WriteLine($"\n🚀 보고서 생성 시작: {topic}");
        Console.WriteLine(new string('=', 60));

        // 1단계: 연구
        Console.WriteLine("\n📚 [1/4] 연구 Agent - 정보 수집 중...");
        var research = await RunAgent(_researcher,
            $"다음 주제에 대해 심층 연구해줘: {topic}", streamOutput);

        // 2단계: 분석
        Console.WriteLine("\n\n🔍 [2/4] 분석 Agent - 인사이트 도출 중...");
        var analysis = await RunAgent(_analyst,
            $"다음 연구 내용을 분석하고 핵심 인사이트를 도출해줘:\n\n{research}",
            streamOutput);

        // 3단계: 보고서 작성
        Console.WriteLine("\n\n✍️ [3/4] 작성 Agent - 보고서 작성 중...");
        var report = await RunAgent(_writer,
            $"다음 연구와 분석을 바탕으로 전문적인 마크다운 보고서를 작성해줘:\n\n" +
            $"## 연구 내용\n{research}\n\n## 분석 결과\n{analysis}",
            streamOutput);

        // 4단계: 품질 검토 (반복 가능)
        Console.WriteLine("\n\n🔎 [4/4] 검토 Agent - 품질 검증 중...");
        var review = await RunAgent(_reviewer,
            $"다음 보고서를 검토하고 평가해줘:\n\n{report}", streamOutput);

        // APPROVED/NEEDS_REVISION 판정
        if (review.Contains("NEEDS_REVISION"))
        {
            Console.WriteLine("\n\n⚠️ 개선이 필요하다. 재작성 중...");
            report = await RunAgent(_writer,
                $"다음 보고서를 검토자의 피드백을 반영하여 개선해줘:\n\n" +
                $"원본:\n{report}\n\n검토 의견:\n{review}",
                streamOutput);
        }

        Console.WriteLine("\n\n✅ 보고서 생성 완료!");
        return report;
    }

    private async Task<string> RunAgent(AIAgent agent, string prompt,
        bool stream = true)
    {
        var result = new System.Text.StringBuilder();

        if (stream)
        {
            await foreach (var chunk in agent.RunStreamingAsync(prompt))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    Console.Write(chunk.Text);
                    result.Append(chunk.Text);
                }
            }
        }
        else
        {
            var r = await agent.RunAsync(prompt);
            Console.Write(r.Text);
            result.Append(r.Text);
        }

        return result.ToString();
    }
}