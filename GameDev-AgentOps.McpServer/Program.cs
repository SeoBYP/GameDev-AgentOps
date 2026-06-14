using System.Text.Json;
using Microsoft.Agents.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();


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

// 도구 본체 — 여기는 네가 채워봐
static string DoCalculate(JsonElement root)
{
    var expr = root.GetProperty("arguments").GetProperty("expression").GetString() ?? "";
    var dt = new System.Data.DataTable();
    return $"{expr} = {dt.Compute(expr, "")}";   // ← 이게 전부. LLM 없음.
}
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run("http://localhost:5100");