using System.Text;
using System.Text.Json;
using System.ComponentModel;

namespace GameDev_AgentOps;

public class McpClientTools
{
    private readonly HttpClient _http = new();
    private readonly string _url = "http://localhost:5100";

    [Description("현재 시스템 정보를 조회한다.")]
    public Task<string> GetSystemInfo() => Call("get_system_info", new { });

    [Description("수식을 계산한다. 예: 125 * 847")]
    public Task<string> Calculate(string expression) => Call("calculate", new { expression });

    // ★ 서버에 POST하고 content[0].text 꺼내는 공통 헬퍼
    private async Task<string> Call(string name, object arguments)
    {
        var payload = JsonSerializer.Serialize(new { name, arguments });
        var body = new StringContent(payload, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"{_url}/tools/call", body);
        var json = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }
}