using Microsoft.Agents.AI;

namespace GameDev_AgentOps;

/// <summary>질의응답 결과</summary>
public record QAResult(string Answer, List<string> Sources);

/// <summary>문서 기반 질의응답 에이전트</summary>
public class DocumentQAAgent
{
    private DocumentManager _docManager;
    private AIAgent _agent;
    private AgentSession _session;

    public DocumentQAAgent(DocumentManager docManager)
    {
        _docManager = docManager;

        _agent = AIAgentBuilder
            .FromEnvironment()
            .Build(
                name: "DocumentQA",
                instructions: @"당신은 제공된 문서를 기반으로 질문에 답변하는 전문가다.

규칙:
1. 반드시 제공된 문서 내용만을 근거로 답변한다
2. 문서에 없는 내용은 '문서에서 해당 정보를 찾을 수 없습니다'라고 답변한다
3. 답변 시 어느 부분에서 찾았는지 명확히 한다
4. 문서 내용을 인용할 때는 따옴표를 사용한다"
            );
    }
    
    public async Task<QAResult> AskAsync(string question)
    {
        Console.WriteLine($"\n❓ 질문 분석 중: {question}");
        _session ??= await _agent.CreateSessionAsync();
        
        // 관련 문서 청크 검색
        var relevantChunks = _docManager.SearchChunks(question, topK: 5);

        if (relevantChunks.Count == 0)
        {
            return new QAResult(
                "로드된 문서가 없거나 관련 내용을 찾을 수 없습니다.",
                new List<string>());
        }

        Console.WriteLine($"📚 관련 청크 {relevantChunks.Count}개 발견");

        // 컨텍스트 구성
        var context = new System.Text.StringBuilder();
        context.AppendLine("=== 관련 문서 내용 ===");
        foreach (var chunk in relevantChunks)
        {
            context.AppendLine($"\n[출처: {chunk.FileName}, 청크 #{chunk.ChunkIndex}]");
            context.AppendLine(chunk.Text);
        }
        context.AppendLine("\n=== 질문 ===");
        context.AppendLine(question);
        context.AppendLine("\n위 문서 내용을 바탕으로 질문에 답변해줘.");

        Console.Write("\n💭 답변 생성 중...\n\n🤖 답변: ");

        // 스트리밍으로 답변 생성
        var answer = new System.Text.StringBuilder();
        await foreach (var chunk in _agent.RunStreamingAsync(context.ToString(), _session))
        {
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                Console.Write(chunk.Text);
                answer.Append(chunk.Text);
            }
        }
        Console.WriteLine();

        // 출처 목록 생성
        var sources = relevantChunks
            .Select(c => $"{c.FileName} (청크 #{c.ChunkIndex})")
            .Distinct()
            .ToList();

        return new QAResult(answer.ToString(), sources);
    }
}