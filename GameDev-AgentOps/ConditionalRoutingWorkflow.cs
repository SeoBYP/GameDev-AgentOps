using Microsoft.Agents.AI;

namespace GameDev_AgentOps;

/// <summary>입력 복잡도에 따라 다른 Agent 경로로 라우팅하는 Workflow</summary>
public class ConditionalRoutingWorkflow
{
    private readonly AIAgent _router;
    private readonly AIAgent _simpleAgent;
    private readonly AIAgent _complexAgent;

    public ConditionalRoutingWorkflow()
    {
        _router = AIAgentBuilder.FromEnvironment().Build(
            name: "RouterAgent",
            instructions: @"입력 질문의 복잡도를 판단한다.
단순한 질문이면 'SIMPLE', 복잡한 질문이면 'COMPLEX'만 출력한다.
복잡한 기준: 여러 단계 추론, 전문 지식, 다각도 분석이 필요한 경우");

        _simpleAgent = AIAgentBuilder.FromEnvironment().Build(
            name: "SimpleAgent",
            instructions: "간단하고 빠르게 핵심만 답변하는 어시스턴트다.");

        _complexAgent = AIAgentBuilder.FromEnvironment().Build(
            name: "ComplexAgent",
            instructions: "복잡한 문제를 단계별로 깊이 분석하는 전문가다. 모든 관련 측면을 고려한다.");
    }

    public async Task<string> ProcessAsync(string question)
    {
        // 1. 복잡도 판단
        var routingResult = await _router.RunAsync(
            $"이 질문의 복잡도를 판단해줘: {question}");
        var isComplex = routingResult.Text.Contains("COMPLEX");

        Console.WriteLine($"\n🔀 라우팅: {(isComplex ? "복잡 경로" : "단순 경로")}");

        // 2. 적절한 Agent로 라우팅
        AgentResponse result;
        if (isComplex)
        {
            Console.Write("\n🧠 [복잡 Agent] ");
            result = await _complexAgent.RunAsync(question);
        }
        else
        {
            Console.Write("\n⚡ [단순 Agent] ");
            result = await _simpleAgent.RunAsync(question);
        }

        Console.WriteLine(result.Text);
        return result.Text;
    }
}