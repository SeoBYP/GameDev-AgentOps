using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetEnv;
using GameDev_AgentOps;
using Microsoft.Agents.AI;
using AIAgentBuilder = GameDev_AgentOps.AIAgentBuilder;

// .env 파일 로드
Env.Load();

var memory = new SimpleMemoryProvider("user001");
var profileContext = memory.Profile.ToContextString();

var agent = AIAgentBuilder
    .FromEnvironment()
    .Build(
        name: "MemoryAgent",
        instructions: $@"당신은 기억력이 좋은 개인 비서다.
{(string.IsNullOrEmpty(profileContext) ? "" : $"\n사용자 정보:\n{profileContext}\n")}
대화 중 사용자에 대한 새로운 정보를 파악하면 자연스럽게 기억한다."
    );

var sessionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "session.json");
AgentSession thread;
if (File.Exists(sessionPath))
{
    var text = File.ReadAllText(sessionPath);          // 파일 → 문자열
    using var doc = JsonDocument.Parse(text);          // 문자열 → JsonDocument
    thread = await agent.DeserializeSessionAsync(doc.RootElement); // → 세션 복원
}
else
{
    thread = await agent.CreateSessionAsync();          // 없으면 새 세션
}

Console.WriteLine("💾 장기 메모리 Agent");
Console.WriteLine(string.IsNullOrEmpty(profileContext)
    ? "  (저장된 사용자 정보 없음)"
    : $"  {profileContext}");
Console.WriteLine();

async Task<string> SummarizeThread(AIAgent agent, AgentSession thread)
{
    var summaryPrompt = "지금까지 나눈 대화의 핵심 내용을 3-5줄로 요약해줘.";
    var result = await agent.RunAsync(summaryPrompt, thread);
    return result.Text;
}

int turnCount = 0;

while (true)
{
    Console.Write("👤 당신: ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(input) || input == "quit") break;

    var result = await agent.RunAsync(input, thread);
    Console.WriteLine($"\n🤖 Agent: {result.Text}\n");
    turnCount++;

    // 20턴마다 Thread 압축
    if (turnCount % 20 == 0)
    {
        Console.WriteLine("📝 대화 요약 중...");
        var summary = await SummarizeThread(agent, thread);
        
        var jsonElement = await agent.SerializeSessionAsync(thread);
        var json = jsonElement.GetRawText();
        File.WriteAllText(sessionPath, json);
        
        // 새 Thread에 요약을 첫 메시지로 추가
        thread = await agent.CreateSessionAsync();
        await agent.RunAsync($"이전 대화 요약: {summary}\n이 맥락을 기억하고 대화를 계속한다.", thread);
        Console.WriteLine("✅ 대화 컨텍스트 압축 완료");
    }
}

// 세션 종료 시 대화 내용 저장
Console.WriteLine("대화 내용을 메모리에 저장하고 있다...");
var element = await agent.SerializeSessionAsync(thread);
var jsonData = element.GetRawText();
File.WriteAllText(sessionPath, jsonData);
memory.Save();