using DotNetEnv;

// 19,26,30,35

Env.Load();

var apiKey  = Environment.GetEnvironmentVariable("LLM_API_KEY");
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") 
              ?? "https://openrouter.ai/api/v1";
var model   = Environment.GetEnvironmentVariable("LLM_MODEL") 
              ?? "anthropic/claude-sonnet-4-5";


if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("❌ LLM_API_KEY 환경 변수가 설정되지 않았다.");
    Console.WriteLine("   .env 파일 또는 시스템 환경 변수를 확인하라.");
}
else
{
    Console.WriteLine("✅ API 키 로드 성공");
    Console.WriteLine($"   키 앞 8자리: {apiKey[..Math.Min(8, apiKey.Length)]}...");
    Console.WriteLine($"✅ Base URL: {baseUrl}");
    Console.WriteLine($"✅ 모델: {model}");
    Console.WriteLine($"✅ .NET 버전: {Environment.Version}");
    Console.WriteLine("환경 준비 완료!");
}