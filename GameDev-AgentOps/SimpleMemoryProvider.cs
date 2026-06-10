using System.Text.Json;

namespace GameDev_AgentOps;

public class UserProfile
{
    public string? Name { get; set; }
    public string? Occupation { get; set; }
    public List<string> Interests { get; set; } = new();
    public Dictionary<string, string> Preferences { get; set; } = new();

    public string ToContextString()
    {
        var facts = new List<string>();
        if (!string.IsNullOrEmpty(Name))
            facts.Add($"이름: {Name}");
        if (!string.IsNullOrEmpty(Occupation))
            facts.Add($"직업: {Occupation}");
        if (Interests.Count > 0)
            facts.Add($"관심사: {string.Join(", ", Interests)}");
        return facts.Count > 0
            ? $"[사용자 정보: {string.Join(", ", facts)}]"
            : "";
    }
}

public class SimpleMemoryProvider
{
    private readonly string _filePath;
    private UserProfile _profile = new();

    public SimpleMemoryProvider(string userId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentMemory");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, $"{userId}.json");
        Load();
    }

    public UserProfile Profile => _profile;

    public void UpdateFromConversation(string? conversationSummary)
    {
        // 실제 구현: LLM으로 대화에서 정보를 추출하여 프로필 업데이트
        // 여기서는 간단한 키워드 파싱으로 구현
        if (conversationSummary.Contains("이름은"))
        {
            var start = conversationSummary.IndexOf("이름은") + 3;
            var end = conversationSummary.IndexOf(' ', start);
            if (end > start)
                _profile.Name = conversationSummary[start..end].Trim('이', '다', '야', '.');
        }
        Save();
    }
    

    private void Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _profile = JsonSerializer.Deserialize<UserProfile>(json) ?? new();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_profile, new JsonSerializerOptions
            { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}