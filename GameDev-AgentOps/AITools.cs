using System.ComponentModel;

namespace GameDev_AgentOps;

public static class AITools
{
    [Description("지정된 도시의 현재 날씨를 조회한다.")]
    public static string GetWeather(string city)
    {
        Console.WriteLine($"  🌤️ GetWeather(\"{city}\")");
        var random = new Random();
        var conditions = new[] { "맑음", "흐림", "비", "눈" };
        var temp = random.Next(-5, 35);
        return $"{city}: {conditions[random.Next(conditions.Length)]}, {temp}도";
    }
    
    [Description("지정된 도시의 현재 시간을 반환한다.")]
    public static string GetCurrentTime(string city)
    {
        Console.WriteLine($"  🕐 GetCurrentTime(\"{city}\")");
        var offsets = new Dictionary<string, int>
        {
            { "서울", 9 }, { "뉴욕", -5 }, { "런던", 0 },
            { "도쿄", 9 }, { "파리", 1 }
        };
        var offset = offsets.GetValueOrDefault(city, 9);
        var localTime = DateTime.UtcNow.AddHours(offset);
        return $"{city} 현재 시간: {localTime:HH:mm:ss}";
    }

    [Description("두 도시 사이의 대략적인 거리를 계산한다.")]
    public static string CalculateDistance(string from, string to)
    {
        Console.WriteLine($"  📏 CalculateDistance(\"{from}\", \"{to}\")");
        var distances = new Dictionary<string, int>
        {
            { "서울-부산", 325 }, { "서울-제주", 450 },
            { "서울-뉴욕", 11000 }, { "서울-도쿄", 1160 }
        };
        var key = $"{from}-{to}";
        var dist = distances.GetValueOrDefault(key,
            distances.GetValueOrDefault($"{to}-{from}", 1000));
        return $"{from}→{to}: 약 {dist}km";
    }
    
    [Description("지정된 통화의 현재 환율을 조회한다 (KRW 기준).")]
    public static string GetExchangeRate(string currency)
    {
        Console.WriteLine($"  💱 GetExchangeRate(\"{currency}\")");
        var rates = new Dictionary<string, double>
        {
            { "USD", 1320.50 }, { "JPY", 9.15 },
            { "EUR", 1450.30 }, { "CNY", 182.40 }
        };
        var rate = rates.GetValueOrDefault(currency.ToUpper(), 0);
        return rate == 0
            ? $"{currency} 환율 정보 없음"
            : $"1 {currency} = {rate:N2} KRW";
    }
}