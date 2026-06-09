using DotNetEnv;
using GameDev_AgentOps;

// .env 파일 로드
Env.Load();

Console.WriteLine("🤖 대화형 Agent 시작 (종료: 'quit' 입력)");
Console.WriteLine("────────────────────────────────────────");
Console.WriteLine();

// AIAgentBuilder로 에이전트 생성 (LLM 제공자를 환경 변수로 결정)
var agent = AIAgentBuilder
    .FromEnvironment()
    .Build(
        name: "ChatAgent",
        instructions: @"당신은 친절하고 도움이 되는 AI 어시스턴트다.
사용자와 자연스럽게 대화한다.
답변은 간결하고 명확하게 한다."
    );

// Thread를 사용하여 대화 컨텍스트 유지
var session = await agent.CreateSessionAsync();
while (true)
{
    Console.Write("💬 사용자: ");
    var input = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Trim().ToLower() == "quit") break;
    
    Console.WriteLine();
    Console.Write("🤖 Agent: ");

    try
    {
        var result = await agent.RunAsync(input,session);
        Console.WriteLine(result.Text);
        Console.WriteLine();
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"❌ 네트워크 오류: {ex.Message}");
        Console.WriteLine("   LLM_BASE_URL과 인터넷 연결을 확인하라.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 오류: {ex.Message}");
    }

}

Console.WriteLine("대화를 종료한다.");