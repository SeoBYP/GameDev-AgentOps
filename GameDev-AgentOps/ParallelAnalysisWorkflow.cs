namespace GameDev_AgentOps;

/// <summary>여러 Agent가 병렬로 분석하고 결과를 합산하는 Workflow</summary>
public class ParallelAnalysisWorkflow
{
    public async Task<string> AnalyzeAsync(string topic)
    {
        Console.WriteLine($"\n⚡ 병렬 분석 시작: {topic}");

        // 3개 Agent가 동시에 다른 관점에서 분석
        var techAgent = AIAgentBuilder.FromEnvironment()
            .Build("TechAgent", "기술적 관점에서만 분석하는 전문가다.");
        var bizAgent  = AIAgentBuilder.FromEnvironment()
            .Build("BizAgent",  "비즈니스 관점에서만 분석하는 전문가다.");
        var riskAgent = AIAgentBuilder.FromEnvironment()
            .Build("RiskAgent", "리스크와 도전과제 관점에서만 분석하는 전문가다.");
        Console.WriteLine("🔄 3개 Agent 병렬 실행 중...");

        // 병렬 실행
        var tasks = new[]
        {
            techAgent.RunAsync($"기술적 관점에서 분석해줘: {topic}"),
            bizAgent.RunAsync($"비즈니스 관점에서 분석해줘: {topic}"),
            riskAgent.RunAsync($"리스크 관점에서 분석해줘: {topic}")
        };
        
        var results = await Task.WhenAll(tasks);

        // 결과 통합
        var synthesizer = AIAgentBuilder.FromEnvironment()
            .Build("SynthAgent", "여러 관점의 분석을 통합하여 종합적인 인사이트를 제공한다.");

        Console.WriteLine("\n🔀 결과 통합 중...\n");
        
        var combined =
            $"기술 분석:\n{results[0].Text}\n\n" +
            $"비즈니스 분석:\n{results[1].Text}\n\n" +
            $"리스크 분석:\n{results[2].Text}";

        var final = await synthesizer.RunAsync(
            $"다음 세 가지 관점의 분석을 통합하여 종합 보고서를 작성해줘:\n\n{combined}");

        Console.WriteLine(final.Text);
        return final.Text;
    }
}