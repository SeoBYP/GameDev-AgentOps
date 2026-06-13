using System.Diagnostics;
using System.Text;
using AutomationAgent;
using DotNetEnv;
using GameDev_AgentOps;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using AIAgentBuilder = GameDev_AgentOps.AIAgentBuilder;

Env.Load();

Console.WriteLine("🤖 업무 자동화 Agent");
Console.WriteLine(new string('=', 60));
Console.WriteLine("자연어로 명령하면 Agent가 파일/데이터를 처리한다.");
Console.WriteLine("종료: 'quit' | 도움말: 'help'");
Console.WriteLine();

// 도구 초기화
var fileTools = new FileTools();
var dataTools = new DataTools(fileTools.GetWorkDirectory().Replace("작업 디렉토리: ", ""));
var sysTools = new SystemTools();

// 샘플 데이터 생성
CreateSampleData(fileTools);

// Agent 생성 - 모든 Tool 등록
var agent = AIAgentBuilder
    .FromEnvironment()
    .Build(
        name: "AutomationAgent",
        instructions: @"당신은 업무 자동화 전문가다.
FileTools, DataTools, SystemTools를 사용하여 사용자의 요청을 수행한다.

사용 가능한 도구:
- ListFiles: 파일 목록 조회
- ReadFile: 파일 내용 읽기
- WriteFile: 파일 쓰기
- SearchFiles: 파일 검색
- AnalyzeCsv: CSV 파일 분석
- AnalyzeText: 텍스트 파일 분석
- Calculate: 숫자 계산
- GetCurrentTime: 현재 시간
- GetDiskUsage: 디스크 사용량
- GetSystemInfo: 시스템 정보

안전 규칙:
- 파일 삭제는 수행하지 않는다
- 작업 디렉토리 외부 파일은 접근하지 않는다
- 결과를 명확하게 설명한다",
        tools: new Delegate[]
        {
            fileTools.ListFiles,
            fileTools.ReadFile,
            fileTools.WriteFile,
            fileTools.SearchFiles,
            fileTools.GetWorkDirectory,
            dataTools.AnalyzeCsv,
            dataTools.AnalyzeText,
            dataTools.Calculate,
            sysTools.GetCurrentTime,
            sysTools.GetDiskUsage,
            sysTools.GetSystemInfo
        }
    );

var thread = await agent.CreateSessionAsync();

Console.WriteLine("✅ Agent 준비 완료. 명령을 입력하라.\n");

while (true)
{
    Console.Write("🧑 명령: ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(input)) continue;

    if (input.ToLower() == "quit") break;

    if (input.ToLower() == "help")
    {
        Console.WriteLine("""

                          예시 명령:
                            - 파일 목록 보여줘
                            - sales.csv 파일 분석해줘
                            - 현재 시간 알려줘
                            - 디스크 사용량은?
                            - 1, 5, 8, 3, 9 의 평균과 합계 계산해줘
                            - 오늘 날짜로 일일 보고서 파일 만들어줘
                          """);
        continue;
    }

    Console.WriteLine();
    Console.Write("🤖 Agent: ");

    try
    {
        await foreach (var chunk in agent.RunStreamingAsync(input, thread))
        {
            if (!string.IsNullOrEmpty(chunk.Text))
                Console.Write(chunk.Text);
        }
    }
    catch (Exception ex)
    {
        Console.Write($"❌ 오류: {ex.Message}");
    }

    Console.WriteLine("\n");
}

Console.WriteLine("업무 자동화 Agent를 종료한다.");

void CreateSampleData(FileTools ft)
{
    // 샘플 CSV 생성
    ft.WriteFile("sales.csv", """
                              월,매출,비용,이익
                              1월,12500000,8000000,4500000
                              2월,15800000,9200000,6600000
                              3월,11200000,7800000,3400000
                              4월,18900000,10500000,8400000
                              5월,16700000,9800000,6900000
                              6월,21300000,11200000,10100000
                              """);

    // 샘플 보고서 생성
    ft.WriteFile("template.txt", $"""
                                  일일 업무 보고서
                                  생성 일시: {DateTime.Now:yyyy년 MM월 dd일}

                                  1. 주요 업무
                                     - [내용 입력]

                                  2. 완료 사항
                                     - [내용 입력]

                                  3. 이슈 및 특이사항
                                     - [내용 입력]

                                  4. 내일 계획
                                     - [내용 입력]
                                  """);

    Console.WriteLine("✅ 샘플 데이터 생성 완료 (sales.csv, template.txt)\n");
}