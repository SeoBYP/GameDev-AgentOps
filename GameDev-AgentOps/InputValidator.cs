namespace AutomationAgent;

public static class InputValidator
{
    private static readonly string[] BlockedPatterns =
    {
        "ignore previous instructions",
        "프롬프트를 무시해",
        "시스템 지시를 변경해"
    };

    public static (bool isValid, string? error) Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, "입력이 비어있습니다.");

        if (input.Length > 10_000)
            return (false, "입력이 너무 깁니다. 최대 10,000자까지 허용됩니다.");

        foreach (var pattern in BlockedPatterns)
        {
            if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return (false, "허용되지 않는 입력입니다.");
        }

        return (true, null);
    }
}