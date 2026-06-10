# 챕터별 코드 레퍼런스 (chapter01 ~ 05)

> 나중에 다시 볼 때 헷갈리지 않도록, **각 챕터에서 직접 작성한 코드**를 챕터 단위로 모았다.
> 개념·트러블슈팅 상세는 [learning-journey.md](./learning-journey.md) 참고. 이 문서는 "챕터 + 그 코드".
>
> 공통 환경: .NET 10, `Microsoft.Agents.AI` 1.9.0, OpenAI 호환(OpenRouter). **MAF 1.9.0은 Thread→Session 개명** → `CreateSessionAsync()` / `AgentSession` 사용.

---

## 공통 — AIAgentBuilder.cs (chapter03에서 작성, 이후 계속 재사용)

LLM 제공자 추상화 + Builder 패턴. `Build()` 이후 코드는 제공자와 무관해진다.

```csharp
using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace GameDev_AgentOps;

public class AIAgentBuilder
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public string Model   => _model;
    public string BaseUrl => _baseUrl;

    // 제공자별 진입점 — 전부 (apiKey, baseUrl, model) 3요소로 수렴
    public static AIAgentBuilder WithOpenRouter(string apiKey, string model = "anthropic/claude-sonnet-4-5")
        => new(apiKey, "https://openrouter.ai/api/v1", model);

    public static AIAgentBuilder WithPoe(string apiKey, string model = "claude-sonnet-4-20250514")
        => new(apiKey, "https://api.poe.com/v1", model);

    public static AIAgentBuilder WithCustomEndpoint(string apiKey, string baseUrl, string model)
        => new(apiKey, baseUrl, model);

    // 환경 변수에서 자동 구성 (권장)
    public static AIAgentBuilder FromEnvironment()
    {
        var apiKey  = Environment.GetEnvironmentVariable("LLM_API_KEY")
                      ?? throw new InvalidOperationException("LLM_API_KEY 환경 변수가 설정되지 않았다.");
        var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "https://openrouter.ai/api/v1";
        var model   = Environment.GetEnvironmentVariable("LLM_MODEL")    ?? "anthropic/claude-sonnet-4-5";
        return new(apiKey, baseUrl, model);
    }

    public AIAgentBuilder(string apiKey, string baseUrl, string model)
    {
        _apiKey = apiKey; _baseUrl = baseUrl; _model = model;
    }

    // Build() 내부 3단계: OpenAIClient(커스텀 엔드포인트) → chat client → AIAgent 승격
    public AIAgent Build(string name, string instructions, params AIFunction[] tools)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(_apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_baseUrl) });   // ★ 엔드포인트 꽂기 (안 하면 진짜 OpenAI로 감)

        IChatClient chatClient = client.GetChatClient(_model).AsIChatClient();

        return tools.Length == 0
            ? chatClient.AsAIAgent(instructions: instructions, name: name)
            : chatClient.AsAIAgent(instructions: instructions, name: name, tools: tools);
    }

    // Delegate 배열 → AIFunction 자동 변환 (chapter04 도구 등록용)
    public AIAgent Build(string name, string instructions, IEnumerable<Delegate> tools)
    {
        var aiFunctions = new List<AIFunction>();
        foreach (var tool in tools)
            aiFunctions.Add(AIFunctionFactory.Create(tool));
        return Build(name, instructions, aiFunctions.ToArray());
    }

    public void PrintConfig()
    {
        var maskedKey = _apiKey.Length > 8 ? _apiKey[..8] + "..." : "****";
        Console.WriteLine($"🔧 Base URL : {_baseUrl}");
        Console.WriteLine($"   Model    : {_model}");
        Console.WriteLine($"   API Key  : {maskedKey}");
    }
}
```

**핵심 포인트**
- `WithOpenRouter`/`WithPoe`/`FromEnvironment` → 모두 `(apiKey, baseUrl, model)`로 정규화 → 제공자 전환이 한 줄.
- `Build()` = `OpenAIClient(엔드포인트)` → `GetChatClient(model)` → `AsAIAgent(name, instructions)`.
- `AIFunctionFactory.Create(delegate)`가 C# 함수를 LLM 도구 스키마로 변환.

---

## Chapter 01 — 환경 준비

**목표:** `.env` 로딩 + 3개 환경변수 확인.

**강의 핵심:** MAF는 Azure 전용이 아니다 — OpenRouter/Poe 등 **OpenAI 호환 API** 사용 가능. .NET 10 권장. 환경변수 `LLM_API_KEY`(필수)/`LLM_BASE_URL`/`LLM_MODEL`. 패키지 `Microsoft.Agents.AI`·`.OpenAI`·`DotNetEnv`. `.gitignore`에 `.env`/`bin`/`obj`.

```csharp
using DotNetEnv;

Env.Load();   // ★ .env → 프로세스 환경변수로 주입 (MAF는 자동 로드 안 함)

var apiKey  = Environment.GetEnvironmentVariable("LLM_API_KEY");
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "https://openrouter.ai/api/v1";
var model   = Environment.GetEnvironmentVariable("LLM_MODEL")    ?? "anthropic/claude-sonnet-4-5";

if (string.IsNullOrEmpty(apiKey))
    Console.WriteLine("❌ LLM_API_KEY 미설정 — .env 확인");
else
    Console.WriteLine($"✅ 준비 완료: {baseUrl} / {model}");
```

**csproj — .env를 출력 폴더로 복사 (작업디렉터리 함정 해결)**
```xml
<ItemGroup>
  <None Update=".env">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**핵심 포인트 / 함정**
- `Env.Load()`는 **현재 작업디렉터리(CWD)** 만 본다. 실행은 `bin/Debug/net10.0/`에서 → `.env`를 거기로 복사해야 함.
- `.env`만 수정 시 증분 빌드가 복사를 스킵 → `dotnet clean` 후 재빌드.
- `.env`는 `.gitignore`에 추가, `git check-ignore .env`로 실제 제외 확인.

---

## Chapter 02 — 핵심 개념 (코드 없음)

| 개념 | 한 줄 정리 |
|---|---|
| Agent Loop | LLM은 판단만, 실제 도구 실행·반복은 **프레임워크(내 코드)**. |
| Tool Calling | C# 함수 → JSON 스키마로 LLM에 전달. LLM은 `tool_call` JSON을 *요청*만 함. |
| Stateless LLM | API는 무상태. 멀티턴은 **매 호출 전체 history 재전송**으로 성립. |
| Session(Thread) | 그 history를 담는 그릇. |
| Builder + 제공자 추상화 | OpenAI 호환이라 차이는 `baseUrl+model+key`뿐 → 정규화. |
| Agent vs Workflow | 순서를 LLM이 정하면 Agent(비결정적), 개발자가 그래프로 못 박으면 Workflow(결정적·감사 가능). |

---

## Chapter 03 — 첫 멀티턴 대화 에이전트

**목표:** `CreateSessionAsync` + `RunAsync(input, session)`로 대화 맥락 유지.

**강의 핵심:** `AIAgentBuilder` 패턴으로 LLM 제공자 전환을 한 줄로. `Build()` 이후 코드는 제공자 무관. `RunAsync`(완성 후 반환) vs `RunStreamAsync`(SSE 실시간). 멀티턴은 같은 thread를 계속 넘기면 프레임워크가 히스토리 자동 관리. 에러는 키(`InvalidOperationException`)·HTTP(401/429/404) 분기.

```csharp
using DotNetEnv;
using GameDev_AgentOps;

Env.Load();

var agent = AIAgentBuilder
    .FromEnvironment()
    .Build(
        name: "ChatAgent",
        instructions: @"당신은 친절하고 도움이 되는 AI 어시스턴트다.
사용자와 자연스럽게 대화한다. 답변은 간결하고 명확하게 한다.");

var session = await agent.CreateSessionAsync();   // ★ 대화 그릇 (1.9.0: Thread→Session, async)

while (true)
{
    Console.Write("💬 사용자: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Trim().ToLower() == "quit") break;

    try
    {
        var result = await agent.RunAsync(input, session);  // ★ session을 넘겨야 멀티턴 기억
        Console.WriteLine($"🤖 {result.Text}\n");
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"❌ 네트워크 오류: {ex.Message}");
    }
}
```

**핵심 포인트 / 함정**
- `CreateThread()`(튜토리얼)는 1.9.0에 없음 → **`await CreateSessionAsync()`**.
- `RunAsync(input, session)` — 입력은 `input`(리터럴 금지), session 반드시 전달.
- 대화기억은 session이 자동관리. `StateBag`은 별개(커스텀 상태용).

---

## Chapter 04 — Tools (Tool Calling)

**목표:** C# 함수를 도구로 등록해 LLM이 호출하게 함.

**강의 핵심:** 개발자는 함수만 제공, 호출 시점은 LLM이 결정. 함수 설명이 있어야 올바르게 사용(강의는 XML 주석 제시 → 실무는 `[Description]`). `AIAgentBuilder`가 `Delegate[]`를 받아 `AIFunctionFactory.Create()` 자동 처리. 단일/멀티 도구, 외부 API 연동, 캐싱, 실행 로그.

```csharp
using System.ComponentModel;
using System.Net.Http.Json;
using DotNetEnv;
using GameDev_AgentOps;

Env.Load();

var httpClient = new HttpClient();
var weatherApiKey = Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY");

[Description("OpenWeatherMap API를 사용하여 실제 날씨를 조회한다.")]  // ★ 설명은 [Description]로 (XML /// 는 미전달 가능)
async Task<string> GetRealWeather(string city)
{
    var cityMap = new Dictionary<string, string> { { "서울", "Seoul" }, { "부산", "Busan" }, { "제주", "Jeju" } };
    var cityName = cityMap.GetValueOrDefault(city, city);
    var url = $"https://api.openweathermap.org/data/2.5/weather?q={cityName}&appid={weatherApiKey}&units=metric&lang=kr";
    try
    {
        var response = await httpClient.GetFromJsonAsync<WeatherResponse>(url);
        if (response == null) return $"{city} 날씨 정보를 가져올 수 없다.";
        return $"{response.name}: {response.weather[0].description}, {response.main.temp:F1}°C";
    }
    catch (Exception ex) { return $"날씨 조회 오류: {ex.Message}"; }
}

var agent = AIAgentBuilder
    .FromEnvironment()
    .Build("WeatherAgent", "실시간 날씨를 제공한다.",
        [GetRealWeather]);                       // ★ Delegate 배열 → AIFunctionFactory가 스키마 변환

var result = await agent.RunAsync("서울 날씨 어때?");
Console.WriteLine(result.Text);

// 응답 모델 (OpenWeatherMap JSON 역직렬화용)
record WeatherResponse(string name, WeatherMain main, WeatherDescription[] weather);
record WeatherMain(double temp, double feels_like, int humidity);
record WeatherDescription(string main, string description);
```

**핵심 포인트 / 함정**
- LLM은 함수 본문을 못 본다 → **이름 + 설명 + 파라미터**만으로 도구 선택. 설명 = 도구용 프롬프트.
- `[Description]`은 **실제 등록되는 함수**에 붙여야 LLM에 전달됨(캐시 래퍼 등록 시 원본에 붙이면 무효).
- 도구가 또 다른 외부 API(OpenWeatherMap, 별도 키) 호출. **401=키 미활성화**(신규 키 ~2h).
- 도구 `catch`가 진짜 에러(상태코드)를 뭉개지 않게 — 개발 중엔 노출.
- 도구 안에 실행 로그(`Console.WriteLine`)를 넣으면 "실제 호출됐는지" 확인 가능.

---

## Chapter 05 — 대화 흐름 관리 (Session 영속화)

**목표:** 프로그램 종료 후에도 대화를 보존. 시작=복원, 종료=저장.

**강의 핵심:** "거기/그때" 대명사 이해엔 이전 대화 기억 필요 → Thread가 메시지·발화자·순서 담는 컨테이너. 단기(Thread, 세션 종료 시 소멸) vs 장기(Context Provider, 영구 저장 → 프롬프트 주입). 긴 대화는 요약으로 압축. 세션 직렬화 저장/복원으로 이어가기.

### Program.cs

```csharp
using System.Text.Json;
using DotNetEnv;
using GameDev_AgentOps;
using Microsoft.Agents.AI;

Env.Load();

var memory = new SimpleMemoryProvider("user001");           // 장기: 사용자 사실(별도 파일)
var profileContext = memory.Profile.ToContextString();

var agent = AIAgentBuilder
    .FromEnvironment()
    .Build(
        name: "MemoryAgent",
        instructions: $@"당신은 기억력이 좋은 개인 비서다.
{(string.IsNullOrEmpty(profileContext) ? "" : $"\n사용자 정보:\n{profileContext}\n")}
대화 중 새로운 정보를 파악하면 자연스럽게 기억한다.");

// 단기: 세션(대화) — 사실과 별도 파일!
var sessionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "session.json");

AgentSession thread;
if (File.Exists(sessionPath))                                // ★ 복원 조건은 "파일 존재"
{
    var text = File.ReadAllText(sessionPath);                // 파일 → 문자열
    using var doc = JsonDocument.Parse(text);                // 문자열 → JsonDocument
    thread = await agent.DeserializeSessionAsync(doc.RootElement);  // → 세션 복원
}
else
{
    thread = await agent.CreateSessionAsync();               // 없으면 새 세션
}

while (true)
{
    Console.Write("👤 당신: ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(input) || input == "quit") break;

    var result = await agent.RunAsync(input, thread);
    Console.WriteLine($"\n🤖 {result.Text}\n");
}

// ★ 종료 시 저장: Serialize(변환) → WriteAllText(디스크 쓰기) = 2단계
var element = await agent.SerializeSessionAsync(thread);
File.WriteAllText(sessionPath, element.GetRawText());
memory.Save();
```

### SimpleMemoryProvider.cs (장기 기억 — 사용자 사실 영속)

```csharp
using System.Text.Json;
namespace GameDev_AgentOps;

public class UserProfile
{
    public string? Name { get; set; }
    public string? Occupation { get; set; }
    public List<string> Interests { get; set; } = new();

    // 사실 → 시스템 프롬프트에 주입할 문자열 ("장기 기억"의 정체)
    public string ToContextString()
    {
        var facts = new List<string>();
        if (!string.IsNullOrEmpty(Name))       facts.Add($"이름: {Name}");
        if (!string.IsNullOrEmpty(Occupation)) facts.Add($"직업: {Occupation}");
        if (Interests.Count > 0)               facts.Add($"관심사: {string.Join(", ", Interests)}");
        return facts.Count > 0 ? $"[사용자 정보: {string.Join(", ", facts)}]" : "";
    }
}

public class SimpleMemoryProvider
{
    private readonly string _filePath;
    private UserProfile _profile = new();
    public UserProfile Profile => _profile;

    public SimpleMemoryProvider(string userId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentMemory");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, $"{userId}.json");
        Load();
    }

    private void Load()
    {
        if (File.Exists(_filePath))
            _profile = JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(_filePath)) ?? new();
    }

    public void Save()
        => File.WriteAllText(_filePath,
            JsonSerializer.Serialize(_profile, new JsonSerializerOptions { WriteIndented = true }));

    // 주의: 키워드 파싱은 데모용(취약). 실제로는 LLM으로 사실 추출.
    public void UpdateFromConversation(string? text) { /* ... */ }
}
```

**핵심 포인트 / 함정**
- **단기(세션) ≠ 장기(프로필)**: 다른 파일·다른 목적. 세션을 프로필에 넣지 말 것.
- **Serialize ≠ Save**: `SerializeSessionAsync`는 `JsonElement` 변환만 → `File.WriteAllText`까지 해야 저장.
- 복원 조건은 `File.Exists(sessionPath)` (Profile null 체크 ❌ → 첫 실행 크래시).
- 세션 저장은 **루프 종료 직후**에. (압축 블록 안에만 두면 quit 시 미보존)
- `RunAsync`의 2번째 인자는 `AgentSession` — 헬퍼 파라미터를 `object`로 두면 오버로드 미스.

---

## 한 장 요약

| 챕터 | 한 일 | 핵심 API/개념 |
|---|---|---|
| 01 | 환경·.env | `Env.Load()`, CopyToOutputDirectory |
| 02 | 개념 | Agent Loop, Tool Calling, Session, Agent vs Workflow |
| 03 | 첫 멀티턴 | `CreateSessionAsync`, `RunAsync(input, session)` |
| 04 | 도구 | `[Description]`, Delegate→`AIFunctionFactory.Create` |
| 05 | 영속화 | `Serialize/DeserializeSessionAsync`, 단기≠장기 |
