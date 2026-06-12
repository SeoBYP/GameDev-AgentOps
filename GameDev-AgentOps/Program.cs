using System.Diagnostics;
using System.Text;
using DotNetEnv;
using GameDev_AgentOps;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;

Env.Load();

Console.WriteLine("📚 문서 질의응답 시스템");
Console.WriteLine(new string('━', 60));

var docManager = new DocumentManager();
var qaAgent = new DocumentQAAgent(docManager);

// 테스트 문서 자동 생성
CreateSampleDocument();

while (true)
{
    Console.WriteLine("\n📋 메뉴:");
    Console.WriteLine("  1. 문서 로드");
    Console.WriteLine("  2. 로드된 문서 목록");
    Console.WriteLine("  3. 질문하기");
    Console.WriteLine("  4. 모든 문서 제거");
    Console.WriteLine("  5. 종료");
    Console.Write("선택: ");

    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            Console.Write("\n📁 문서 경로: ");
            var path = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                try { docManager.LoadDocument(path); }
                catch (Exception ex) { Console.WriteLine($"❌ {ex.Message}"); }
            }
            break;

        case "2":
            if (docManager.Documents.Count == 0)
            {
                Console.WriteLine("\n로드된 문서가 없다.");
            }
            else
            {
                Console.WriteLine($"\n📚 로드된 문서 ({docManager.Documents.Count}개):");
                foreach (var doc in docManager.Documents)
                    Console.WriteLine($"  • {doc.FileName} ({doc.TotalChunks}청크, {doc.TotalChars:N0}자)");
            }
            break;

        case "3":
            if (docManager.Documents.Count == 0)
            {
                Console.WriteLine("\n⚠️ 먼저 문서를 로드하라.");
                break;
            }
            Console.Write("\n❓ 질문: ");
            var question = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(question))
            {
                var result = await qaAgent.AskAsync(question);
                if (result.Sources.Count > 0)
                {
                    Console.WriteLine("\n📖 출처:");
                    foreach (var src in result.Sources)
                        Console.WriteLine($"  • {src}");
                }
            }
            break;

        case "4":
            docManager.RemoveAll();
            Console.WriteLine("✅ 모든 문서가 제거되었다.");
            break;

        case "5":
            Console.WriteLine("프로그램을 종료한다.");
            return;
    }
}

void CreateSampleDocument()
{
    var sampleDir = "TestDocuments";
    Directory.CreateDirectory(sampleDir);
    var samplePath = Path.Combine(sampleDir, "sample.txt");

    if (!File.Exists(samplePath))
    {
        File.WriteAllText(samplePath, """
# C# 프로그래밍 가이드

## 비동기 프로그래밍

C#은 async와 await 키워드를 통해 비동기 프로그래밍을 지원한다.
비동기 코드를 동기 코드처럼 작성할 수 있게 해준다.

주요 장점:
- 응답성 개선: 오래 걸리는 작업 중에도 UI가 반응한다
- 리소스 효율성: 스레드를 블록하지 않아 리소스를 절약한다
- 확장성: 더 많은 동시 요청을 처리할 수 있다

예제 코드:
public async Task<string> FetchDataAsync()
{
    using var client = new HttpClient();
    return await client.GetStringAsync("https://api.example.com/data");
}

## LINQ

LINQ(Language Integrated Query)는 데이터 쿼리를 언어 수준에서 지원한다.
컬렉션, 데이터베이스, XML 등 다양한 데이터 소스를 동일한 방식으로 다룬다.

기본 사용법:
var result = numbers.Where(n => n > 5).Select(n => n * 2).ToList();

## 의존성 주입

ASP.NET Core는 내장 DI 컨테이너를 제공한다.
서비스의 생성과 수명 주기를 프레임워크가 관리한다.

Singleton: 애플리케이션 수명 동안 하나의 인스턴스
Scoped: HTTP 요청당 하나의 인스턴스
Transient: 요청할 때마다 새 인스턴스
""");
        Console.WriteLine("📝 샘플 문서가 생성되었다: TestDocuments/sample.txt");
        docManager.LoadDocument(samplePath);
    }
}