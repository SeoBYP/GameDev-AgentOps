using Microsoft.Agents.AI;

namespace GameDev_AgentOps;

/// <summary>역할별 전문화된 Agent를 생성하는 팩토리</summary>
public static class AgentFactory
{
    /// <summary>주제 연구 전문 Agent</summary>
    public static AIAgent CreateResearchAgent() =>
        AIAgentBuilder.FromEnvironment().Build(
            name: "ResearchAgent",
            instructions: @"당신은 전문 연구원이다.
주어진 주제에 대해 핵심 개념, 관련 사실, 다양한 관점을 조사한다.
출력: 서론/본론/결론 구조로 명확하게 정리한 연구 내용"
        );
    /// <summary>데이터 분석 전문 Agent</summary>
    public static AIAgent CreateAnalystAgent() =>
        AIAgentBuilder.FromEnvironment().Build(
            name: "AnalystAgent",
            instructions: @"당신은 전문 분석가다.
주어진 정보에서 패턴, 인과관계, 트렌드를 찾아 실행 가능한 인사이트를 도출한다.
출력: 장단점 분석, 핵심 인사이트, 제언"
        );

    /// <summary>문서 작성 전문 Agent</summary>
    public static AIAgent CreateWriterAgent() =>
        AIAgentBuilder.FromEnvironment().Build(
            name: "WriterAgent",
            instructions: @"당신은 전문 작가다.
주어진 연구 내용과 분석을 바탕으로 읽기 쉬운 마크다운 보고서를 작성한다.
출력: 잘 구조화된 마크다운 형식 보고서"
        );
    /// <summary>품질 검토 전문 Agent</summary>
    public static AIAgent CreateReviewerAgent() =>
        AIAgentBuilder.FromEnvironment().Build(
            name: "ReviewerAgent",
            instructions: @"당신은 엄격한 검토자다.
제출된 문서를 정확성, 완전성, 명확성, 일관성 기준으로 평가한다.
출력: 1-10점 평가, 강점 3가지, 개선 사항 3가지, 수정 제안
'APPROVED' 또는 'NEEDS_REVISION' 중 하나로 최종 판정한다."
        );

    /// <summary>번역 전문 Agent</summary>
    public static AIAgent CreateTranslatorAgent() =>
        AIAgentBuilder.FromEnvironment().Build(
            name: "TranslatorAgent",
            instructions: @"당신은 전문 번역가다.
원문의 의미와 뉘앙스를 유지하며 자연스럽게 번역한다.
번역 대상 언어가 명시되면 해당 언어로 번역한다."
        );
}