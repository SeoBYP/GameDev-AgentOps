using System.Text.Json;

var app = WebApplication.Create(args);

// ★ MCP 핵심 1: 도구 목록(스키마)
app.MapGet("/tools", () => new
{
    tools = new[]
    {
        new { name = "get_system_info", description = "시스템 정보를 반환한다." },
        new { name = "calculate",       description = "수식을 계산한다. 예: 2+3*4" }
    }
});

// ★ MCP 핵심 2: 도구를 "실행"하는 엔드포인트
app.MapPost("/tools/call", async (HttpContext ctx) =>
{
    var req = await JsonDocument.ParseAsync(ctx.Request.Body);
    var name = req.RootElement.GetProperty("name").GetString();

    string result = name switch
    {
        "get_system_info" => $"OS: {Environment.OSVersion}, CPU: {Environment.ProcessorCount}코어",
        "calculate"       => DoCalculate(req.RootElement),
        _                 => "알 수 없는 도구"
    };

    // ★ 약속된 응답 형태 (클라이언트가 이 구조로 파싱함)
    return Results.Json(new { content = new[] { new { type = "text", text = result } } });
});

app.Run("http://localhost:5100");

// 도구 본체 (순수 함수, LLM 없음)
static string DoCalculate(JsonElement root)
{
    var expr = root.GetProperty("arguments").GetProperty("expression").GetString() ?? "";
    var dt = new System.Data.DataTable();
    return $"{expr} = {dt.Compute(expr, "")}";
}
